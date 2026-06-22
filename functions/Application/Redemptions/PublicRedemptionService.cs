using System.Net;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Application.Audit;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.CustomerProfiles;
using SimpleDiscountVerifier.Api.Application.Sms;
using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;
using SimpleDiscountVerifier.Api.Domain.Redemptions;
using SimpleDiscountVerifier.Api.Domain.Security;
using SimpleDiscountVerifier.Api.Domain.Shared;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Application.Redemptions;

public sealed class PublicRedemptionService
{
    private const string PhoneRuntimeKeySecretSettingName = "PhoneRuntimeKeySecret";
    private const string SmsCodeHashSecretSettingName = "SmsCodeHashSecret";
    private const string BarcodeHashSecretSettingName = "BarcodeHashSecret";
    private const string SmsCodeTtlSecondsSettingName = "SmsCodeTtlSeconds";
    private const string SmsRetryAfterSecondsSettingName = "SmsRetryAfterSeconds";
    private const string BarcodeTtlSecondsSettingName = "BarcodeTtlSeconds";
    private const string SmsMessageTemplate = "Ваш код підтвердження: {0}";

    private readonly ICustomerProfileRepository _profiles;
    private readonly IDiscountRuntimeRepository _runtime;
    private readonly IAuditWriter _auditWriter;
    private readonly ISmsSender _smsSender;
    private readonly HashingOptions _hashingOptions;
    private readonly SmsOptions _smsOptions;
    private readonly RuntimeOptions _runtimeOptions;
    private readonly IClock _clock;

    public PublicRedemptionService(
        ICustomerProfileRepository profiles,
        IDiscountRuntimeRepository runtime,
        IAuditWriter auditWriter,
        ISmsSender smsSender,
        IOptions<HashingOptions> hashingOptions,
        IOptions<SmsOptions> smsOptions,
        IOptions<RuntimeOptions> runtimeOptions,
        IClock clock)
    {
        _profiles = profiles;
        _runtime = runtime;
        _auditWriter = auditWriter;
        _smsSender = smsSender;
        _hashingOptions = hashingOptions.Value;
        _smsOptions = smsOptions.Value;
        _runtimeOptions = runtimeOptions.Value;
        _clock = clock;
    }

    public async Task<ApplicationResult<StartRedemptionResult>> StartAsync(
        StartRedemptionCommand command,
        CancellationToken cancellationToken)
    {
        var correlationId = CorrelationIdGenerator.Create();
        await WriteAuditAsync(correlationId, RedemptionAuditEventTypes.RedemptionStarted, null, cancellationToken);

        if (!NormalizedPhoneNumber.TryCreate(command.Phone, out var phone))
        {
            await WriteAuditAsync(correlationId, RedemptionAuditEventTypes.InvalidPhone, null, cancellationToken);
            return Failure<StartRedemptionResult>(
                RedemptionErrorCodes.InvalidPhone,
                "Phone is missing or invalid.",
                HttpStatusCode.BadRequest,
                correlationId);
        }

        var profile = await _profiles.GetByPhoneAsync(phone, cancellationToken);

        if (profile is null)
        {
            await WriteAuditAsync(correlationId, RedemptionAuditEventTypes.ProfileNotFound, phone, cancellationToken);
            return Failure<StartRedemptionResult>(
                RedemptionErrorCodes.ProfileNotFound,
                "Profile was not found for this phone.",
                HttpStatusCode.NotFound,
                correlationId);
        }

        await WriteAuditAsync(correlationId, RedemptionAuditEventTypes.ProfileFound, phone, cancellationToken);

        var phoneRuntimeKey = PhoneRuntimeKeyGenerator.Derive(
            phone,
            RequireSetting(_hashingOptions.PhoneRuntimeKeySecret, PhoneRuntimeKeySecretSettingName));
        var now = _clock.UtcNow;
        var retryAfter = RequirePositiveSeconds(_smsOptions.RetryAfterSeconds, SmsRetryAfterSecondsSettingName);
        var existingRuntime = await _runtime.GetCurrentAsync(phoneRuntimeKey, cancellationToken);

        if (existingRuntime is not null && now < existingRuntime.SmsSentAtUtc.AddSeconds(retryAfter))
        {
            await WriteAuditAsync(correlationId, RedemptionAuditEventTypes.SmsRetryTooSoon, phone, cancellationToken);
            return Failure<StartRedemptionResult>(
                RedemptionErrorCodes.SmsRetryTooSoon,
                "SMS request was attempted too soon.",
                HttpStatusCode.TooManyRequests,
                correlationId);
        }

        var codeTtl = RequirePositiveSeconds(_smsOptions.CodeTtlSeconds, SmsCodeTtlSecondsSettingName);
        var smsCode = SmsCodeGenerator.Create();
        var smsResult = await _smsSender.SendAsync(
            new SmsSendRequest(phone, string.Format(SmsMessageTemplate, smsCode)),
            cancellationToken);

        if (!smsResult.Accepted)
        {
            await WriteAuditAsync(
                correlationId,
                RedemptionAuditEventTypes.SmsSendFailed,
                phone,
                cancellationToken);

            return Failure<StartRedemptionResult>(
                RedemptionErrorCodes.SmsSendFailed,
                "SMS provider failed or did not accept the message.",
                HttpStatusCode.BadGateway,
                correlationId);
        }

        var smsExpiresAt = now.AddSeconds(codeTtl);
        var runtime = new DiscountRuntimeRecord(
            phoneRuntimeKey,
            phone,
            correlationId,
            SecretHasher.HashSmsCode(
                smsCode,
                RequireSetting(_hashingOptions.SmsCodeHashSecret, SmsCodeHashSecretSettingName)),
            SmsAttempts: 0,
            RedemptionDefaults.SmsMaxAttempts,
            now,
            smsExpiresAt,
            PhoneVerifiedAtUtc: null,
            BarcodeHash: null,
            BarcodeExpiresAtUtc: null,
            BarcodeConsumedAtUtc: null,
            ConsumedByScanId: null,
            now,
            now);

        await _runtime.UpsertCurrentAsync(runtime, cancellationToken);
        await WriteAuditAsync(
            correlationId,
            RedemptionAuditEventTypes.SmsSent,
            phone,
            cancellationToken,
            smsResult.ProviderMessageId is null
                ? null
                : new Dictionary<string, string?> { ["providerMessageId"] = smsResult.ProviderMessageId });

        return ApplicationResult<StartRedemptionResult>.Success(
            new StartRedemptionResult(phoneRuntimeKey, correlationId, SmsSent: true, retryAfter, smsExpiresAt));
    }

    public async Task<ApplicationResult<SmsVerificationResult>> VerifySmsAsync(
        VerifySmsCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RedemptionKey))
        {
            return Failure<SmsVerificationResult>(
                RedemptionErrorCodes.InvalidRequest,
                "Redemption key is required.",
                HttpStatusCode.BadRequest);
        }

        var runtime = await _runtime.GetCurrentAsync(command.RedemptionKey.Trim(), cancellationToken);

        if (runtime is null)
        {
            return Failure<SmsVerificationResult>(
                RedemptionErrorCodes.RedemptionNotFound,
                "Redemption was not found.",
                HttpStatusCode.NotFound);
        }

        if (!SmsCodeGenerator.IsValidFormat(command.Code))
        {
            return Failure<SmsVerificationResult>(
                RedemptionErrorCodes.InvalidRequest,
                "SMS code is missing or malformed.",
                HttpStatusCode.BadRequest,
                runtime.CorrelationId);
        }

        var now = _clock.UtcNow;

        if (now > runtime.SmsExpiresAtUtc)
        {
            await WriteAuditAsync(
                runtime.CorrelationId,
                RedemptionAuditEventTypes.SmsExpired,
                runtime.Phone,
                cancellationToken);

            return Failure<SmsVerificationResult>(
                RedemptionErrorCodes.SmsExpired,
                "SMS code expired.",
                HttpStatusCode.Gone,
                runtime.CorrelationId);
        }

        if (runtime.SmsAttempts >= runtime.SmsMaxAttempts)
        {
            await WriteAuditAsync(
                runtime.CorrelationId,
                RedemptionAuditEventTypes.SmsAttemptsExceeded,
                runtime.Phone,
                cancellationToken);

            return Failure<SmsVerificationResult>(
                RedemptionErrorCodes.SmsAttemptsExceeded,
                "SMS attempts were exceeded.",
                HttpStatusCode.Locked,
                runtime.CorrelationId);
        }

        if (runtime.PhoneVerifiedAtUtc is not null || runtime.BarcodeHash is not null)
        {
            return Failure<SmsVerificationResult>(
                RedemptionErrorCodes.RedemptionAlreadyCompleted,
                "The current redemption already produced a barcode.",
                HttpStatusCode.Conflict,
                runtime.CorrelationId);
        }

        if (!SecretHasher.FixedTimeEquals(
                runtime.SmsCodeHash,
                command.Code!,
                RequireSetting(_hashingOptions.SmsCodeHashSecret, SmsCodeHashSecretSettingName)))
        {
            return await HandleInvalidSmsCodeAsync(runtime, now, cancellationToken);
        }

        return await IssueBarcodeAsync(runtime, now, cancellationToken);
    }

    private async Task<ApplicationResult<SmsVerificationResult>> HandleInvalidSmsCodeAsync(
        DiscountRuntimeRecord runtime,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updatedAttempts = runtime.SmsAttempts + 1;
        var updated = runtime with
        {
            SmsAttempts = updatedAttempts,
            UpdatedAtUtc = now
        };

        var writeError = await ReplaceRuntimeAsync(updated, cancellationToken);

        if (writeError is not null)
        {
            return ApplicationResult<SmsVerificationResult>.Failure(writeError);
        }

        await WriteAuditAsync(
            runtime.CorrelationId,
            updatedAttempts >= runtime.SmsMaxAttempts
                ? RedemptionAuditEventTypes.SmsAttemptsExceeded
                : RedemptionAuditEventTypes.SmsValidationFailed,
            runtime.Phone,
            cancellationToken,
            new Dictionary<string, string?> { ["attempts"] = updatedAttempts.ToString() });

        return updatedAttempts >= runtime.SmsMaxAttempts
            ? Failure<SmsVerificationResult>(
                RedemptionErrorCodes.SmsAttemptsExceeded,
                "SMS attempts were exceeded.",
                HttpStatusCode.Locked,
                runtime.CorrelationId)
            : Failure<SmsVerificationResult>(
                RedemptionErrorCodes.InvalidSmsCode,
                "SMS code is incorrect.",
                HttpStatusCode.UnprocessableEntity,
                runtime.CorrelationId);
    }

    private async Task<ApplicationResult<SmsVerificationResult>> IssueBarcodeAsync(
        DiscountRuntimeRecord runtime,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var barcodeValue = WebBarcode.Create(runtime.PhoneRuntimeKey, runtime.CorrelationId);
        var barcodeTtl = RequirePositiveSeconds(_runtimeOptions.BarcodeTtlSeconds, BarcodeTtlSecondsSettingName);
        var expiresAt = now.AddSeconds(barcodeTtl);
        var updated = runtime with
        {
            PhoneVerifiedAtUtc = now,
            BarcodeHash = SecretHasher.HashBarcode(
                barcodeValue,
                RequireSetting(_hashingOptions.BarcodeHashSecret, BarcodeHashSecretSettingName)),
            BarcodeExpiresAtUtc = expiresAt,
            BarcodeConsumedAtUtc = null,
            ConsumedByScanId = null,
            UpdatedAtUtc = now
        };

        var writeError = await ReplaceRuntimeAsync(updated, cancellationToken);

        if (writeError is not null)
        {
            return ApplicationResult<SmsVerificationResult>.Failure(writeError);
        }

        await WriteAuditAsync(runtime.CorrelationId, RedemptionAuditEventTypes.PhoneVerified, runtime.Phone, cancellationToken);
        await WriteAuditAsync(runtime.CorrelationId, RedemptionAuditEventTypes.BarcodeIssued, runtime.Phone, cancellationToken);

        return ApplicationResult<SmsVerificationResult>.Success(
            new SmsVerificationResult(
                runtime.CorrelationId,
                barcodeValue,
                WebBarcode.Format,
                expiresAt,
                barcodeTtl));
    }

    private async Task<ApplicationError?> ReplaceRuntimeAsync(
        DiscountRuntimeRecord runtime,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(runtime.ConcurrencyToken))
        {
            return new ApplicationError(
                RedemptionErrorCodes.RedemptionConflict,
                "Stored redemption concurrency token is missing.",
                HttpStatusCode.Conflict,
                runtime.CorrelationId);
        }

        var result = await _runtime.ReplaceCurrentAsync(
            runtime,
            runtime.ConcurrencyToken,
            cancellationToken);

        return result.Status switch
        {
            StorageWriteStatus.Updated => null,
            StorageWriteStatus.NotFound => new ApplicationError(
                RedemptionErrorCodes.RedemptionNotFound,
                "Redemption was not found.",
                HttpStatusCode.NotFound,
                runtime.CorrelationId),
            StorageWriteStatus.PreconditionFailed => new ApplicationError(
                RedemptionErrorCodes.RedemptionConflict,
                "Redemption was changed by another request.",
                HttpStatusCode.Conflict,
                runtime.CorrelationId),
            _ => new ApplicationError(
                RedemptionErrorCodes.RedemptionConflict,
                "Redemption could not be updated.",
                HttpStatusCode.Conflict,
                runtime.CorrelationId)
        };
    }

    private async Task WriteAuditAsync(
        string correlationId,
        string eventType,
        NormalizedPhoneNumber? phone,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string?>? metadata = null)
    {
        await _auditWriter.WriteAsync(
            new AuditWriteRequest(
                correlationId,
                eventType,
                AuditActorTypes.Customer,
                ActorId: null,
                phone,
                metadata),
            cancellationToken);
    }

    private static ApplicationResult<T> Failure<T>(
        string code,
        string message,
        HttpStatusCode statusCode,
        string? correlationId = null) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, statusCode, correlationId));

    private static string RequireSetting(string value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Application setting '{settingName}' is required.");
        }

        return value;
    }

    private static int RequirePositiveSeconds(int value, string settingName)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"Application setting '{settingName}' must be a positive integer.");
        }

        return value;
    }
}
