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
    private const string SmsMaxPerHourSettingName = "SmsMaxPerHour";
    private const string SmsMaxPerDaySettingName = "SmsMaxPerDay";
    private const string SmsResponseFloorMillisecondsSettingName = "SmsResponseFloorMilliseconds";
    private const string BarcodeTtlSecondsSettingName = "BarcodeTtlSeconds";
    private const string SmsMessageTemplate = "Ваш код підтвердження: {0}";

    private readonly ICustomerProfileRepository _profiles;
    private readonly IDiscountRuntimeRepository _runtime;
    private readonly IAuditWriter _auditWriter;
    private readonly ISmsSender _smsSender;
    private readonly ITurnstileVerifier _turnstileVerifier;
    private readonly HashingOptions _hashingOptions;
    private readonly SmsOptions _smsOptions;
    private readonly RuntimeOptions _runtimeOptions;
    private readonly IClock _clock;

    public PublicRedemptionService(
        ICustomerProfileRepository profiles,
        IDiscountRuntimeRepository runtime,
        IAuditWriter auditWriter,
        ISmsSender smsSender,
        ITurnstileVerifier turnstileVerifier,
        IOptions<HashingOptions> hashingOptions,
        IOptions<SmsOptions> smsOptions,
        IOptions<RuntimeOptions> runtimeOptions,
        IClock clock)
    {
        _profiles = profiles;
        _runtime = runtime;
        _auditWriter = auditWriter;
        _smsSender = smsSender;
        _turnstileVerifier = turnstileVerifier;
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

        if (!await _turnstileVerifier.VerifyAsync(command.TurnstileToken, cancellationToken))
        {
            return Failure<StartRedemptionResult>(
                RedemptionErrorCodes.TurnstileVerificationFailed,
                "Bot verification failed.",
                HttpStatusCode.BadRequest,
                correlationId);
        }

        // Invalid bot-challenge traffic is intentionally not persisted: otherwise this anonymous
        // endpoint turns every rejected token into an AuditEvents write-amplification primitive.
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

        // Phone validity and the rate-limit decision below must not depend on whether a customer
        // profile exists for this number: doing the profile lookup first (or branching the HTTP
        // response on it) would let an attacker enumerate registered phone numbers by observing
        // different status codes/timing. The phoneRuntimeKey is deterministic for ANY well-formed
        // phone, so the reservation below is applied uniformly before we ever look at the profile.
        var phoneRuntimeKey = PhoneRuntimeKeyGenerator.Derive(
            phone,
            RequireSetting(_hashingOptions.PhoneRuntimeKeySecret, PhoneRuntimeKeySecretSettingName));
        var now = _clock.UtcNow;
        var minIntervalSeconds = RequirePositiveValue(_smsOptions.RetryAfterSeconds, SmsRetryAfterSecondsSettingName);
        var maxPerHour = RequirePositiveValue(_smsOptions.MaxPerHour, SmsMaxPerHourSettingName);
        var maxPerDay = RequirePositiveValue(_smsOptions.MaxPerDay, SmsMaxPerDaySettingName);
        var codeTtl = RequirePositiveValue(_smsOptions.CodeTtlSeconds, SmsCodeTtlSecondsSettingName);
        var responseFloorMilliseconds = RequirePositiveValue(
            _smsOptions.ResponseFloorMilliseconds,
            SmsResponseFloorMillisecondsSettingName);
        var smsExpiresAt = now.AddSeconds(codeTtl);
        var responseNotBefore = now.AddMilliseconds(responseFloorMilliseconds);

        var reservation = await ReserveSendSlotAsync(
            phoneRuntimeKey,
            phone,
            correlationId,
            now,
            smsExpiresAt,
            minIntervalSeconds,
            maxPerHour,
            maxPerDay,
            cancellationToken);

        if (reservation is null)
        {
            await WriteAuditAsync(correlationId, RedemptionAuditEventTypes.SmsRetryTooSoon, phone, cancellationToken);
            return Failure<StartRedemptionResult>(
                RedemptionErrorCodes.SmsRetryTooSoon,
                "SMS request was attempted too soon.",
                HttpStatusCode.TooManyRequests,
                correlationId);
        }

        var profile = await _profiles.GetByPhoneAsync(phone, cancellationToken);

        var smsCodeHashSecret = RequireSetting(
            _hashingOptions.SmsCodeHashSecret,
            SmsCodeHashSecretSettingName);
        string finalCodeHash;
        SmsSendResult? smsResult = null;

        if (profile is null)
        {
            await WriteAuditAsync(correlationId, RedemptionAuditEventTypes.ProfileNotFound, phone, cancellationToken);
            finalCodeHash = SecretHasher.HashSmsCode(SmsCodeGenerator.Create(), smsCodeHashSecret);
        }
        else
        {
            await WriteAuditAsync(correlationId, RedemptionAuditEventTypes.ProfileFound, phone, cancellationToken);

            var smsCode = SmsCodeGenerator.Create();
            smsResult = await _smsSender.SendAsync(
                new SmsSendRequest(phone, string.Format(SmsMessageTemplate, smsCode)),
                cancellationToken);

            finalCodeHash = SecretHasher.HashSmsCode(
                smsResult.Accepted ? smsCode : SmsCodeGenerator.Create(),
                smsCodeHashSecret);

            if (!smsResult.Accepted)
            {
                await WriteAuditAsync(
                    correlationId,
                    RedemptionAuditEventTypes.SmsSendFailed,
                    phone,
                    cancellationToken);
            }
        }

        if (!await FinalizeReservationAsync(reservation, finalCodeHash, cancellationToken))
        {
            return Failure<StartRedemptionResult>(
                RedemptionErrorCodes.RedemptionConflict,
                "Redemption could not be finalized.",
                HttpStatusCode.Conflict,
                correlationId);
        }

        if (smsResult?.Accepted == true)
        {
            await WriteAuditAsync(
                correlationId,
                RedemptionAuditEventTypes.SmsSent,
                phone,
                cancellationToken,
                smsResult.ProviderMessageId is null
                    ? null
                    : new Dictionary<string, string?> { ["providerMessageId"] = smsResult.ProviderMessageId });
        }

        await DelayUntilAsync(responseNotBefore, cancellationToken);

        return ApplicationResult<StartRedemptionResult>.Success(
            new StartRedemptionResult(phoneRuntimeKey, correlationId, SmsSent: true, minIntervalSeconds, smsExpiresAt));
    }

    /// <summary>
    /// Atomically claims an SMS send slot for <paramref name="phoneRuntimeKey"/> by evaluating the
    /// fixed-window rate-limit counters and writing them back with optimistic concurrency (insert for a
    /// brand-new row, ETag-conditional replace otherwise). Only the request that wins the write
    /// proceeds to look up the profile / call the SMS provider; concurrent duplicates re-read the
    /// freshly-committed counters and correctly observe themselves as throttled. Returns null when
    /// throttled, or the committed placeholder row (still needing <see cref="FinalizeReservationAsync"/>)
    /// when a slot was claimed.
    /// </summary>
    private async Task<DiscountRuntimeRecord?> ReserveSendSlotAsync(
        string phoneRuntimeKey,
        NormalizedPhoneNumber phone,
        string correlationId,
        DateTimeOffset now,
        DateTimeOffset smsExpiresAt,
        int minIntervalSeconds,
        int maxPerHour,
        int maxPerDay,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < RedemptionDefaults.RateLimitMaxAttempts; attempt++)
        {
            var existing = await _runtime.GetCurrentAsync(phoneRuntimeKey, cancellationToken);
            var rateLimitState = existing is null
                ? null
                : new SmsRateLimitState(
                    existing.SmsSentAtUtc,
                    existing.HourWindowStartUtc,
                    existing.HourWindowCount,
                    existing.DayWindowStartUtc,
                    existing.DayWindowCount);

            var decision = SmsSendRateLimiter.Evaluate(rateLimitState, now, minIntervalSeconds, maxPerHour, maxPerDay);

            if (!decision.Allowed)
            {
                return null;
            }

            var claim = new DiscountRuntimeRecord(
                phoneRuntimeKey,
                phone,
                correlationId,
                SmsCodeHash: string.Empty,
                SmsAttempts: 0,
                RedemptionDefaults.SmsMaxAttempts,
                decision.State.LastSentAtUtc,
                smsExpiresAt,
                PhoneVerifiedAtUtc: null,
                BarcodeHash: null,
                BarcodeExpiresAtUtc: null,
                BarcodeConsumedAtUtc: null,
                ConsumedByScanId: null,
                decision.State.HourWindowStartUtc,
                decision.State.HourWindowCount,
                decision.State.DayWindowStartUtc,
                decision.State.DayWindowCount,
                existing?.CreatedAtUtc ?? now,
                now,
                existing?.ConcurrencyToken);

            var writeResult = existing is null
                ? await _runtime.InsertCurrentAsync(claim, cancellationToken)
                : await _runtime.ReplaceCurrentAsync(claim, existing.ConcurrencyToken!, cancellationToken);

            if (writeResult.Succeeded)
            {
                return await _runtime.GetCurrentAsync(phoneRuntimeKey, cancellationToken);
            }

            // Conflict/PreconditionFailed: another concurrent request won this attempt. Loop and
            // re-read the freshly-committed counters; a genuine burst will correctly resolve to
            // "throttled" on the next iteration instead of allowing a second real SMS send.
        }

        // Could not safely claim a slot under heavy contention: fail closed (throttled).
        return null;
    }

    /// <summary>
    /// Fills in the final SMS code hash for a claimed reservation. A short bounded retry re-reads
    /// the row on conflict, since only benign, non-throttled writers (e.g. this same request) are
    /// expected to touch the row again this soon.
    /// </summary>
    private async Task<bool> FinalizeReservationAsync(
        DiscountRuntimeRecord reservation,
        string smsCodeHash,
        CancellationToken cancellationToken)
    {
        var current = reservation;

        for (var attempt = 0; attempt < RedemptionDefaults.RateLimitMaxAttempts; attempt++)
        {
            if (!string.Equals(current.CorrelationId, reservation.CorrelationId, StringComparison.Ordinal))
            {
                return false;
            }

            var updated = current with
            {
                SmsCodeHash = smsCodeHash,
                UpdatedAtUtc = _clock.UtcNow
            };

            var result = await _runtime.ReplaceCurrentAsync(updated, current.ConcurrencyToken!, cancellationToken);

            if (result.Succeeded)
            {
                return true;
            }

            if (result.Status != StorageWriteStatus.PreconditionFailed)
            {
                return false;
            }

            var refreshed = await _runtime.GetCurrentAsync(current.PhoneRuntimeKey, cancellationToken);

            if (refreshed is null)
            {
                return false;
            }

            current = refreshed;
        }

        return false;
    }

    public async Task<ApplicationResult<SmsVerificationResult>> VerifySmsAsync(
        VerifySmsCommand command,
        CancellationToken cancellationToken)
    {
        var redemptionKey = command.RedemptionKey?.Trim();

        if (!PhoneRuntimeKeyGenerator.IsValid(redemptionKey))
        {
            return Failure<SmsVerificationResult>(
                RedemptionErrorCodes.InvalidRequest,
                "Redemption key is missing or malformed.",
                HttpStatusCode.BadRequest);
        }

        var runtime = await _runtime.GetCurrentAsync(redemptionKey!, cancellationToken);

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

        if (string.IsNullOrEmpty(runtime.SmsCodeHash))
        {
            return Failure<SmsVerificationResult>(
                RedemptionErrorCodes.RedemptionConflict,
                "SMS challenge is not ready yet.",
                HttpStatusCode.Conflict,
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
        var barcodeTtl = RequirePositiveValue(_runtimeOptions.BarcodeTtlSeconds, BarcodeTtlSecondsSettingName);
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

    private async Task DelayUntilAsync(DateTimeOffset notBefore, CancellationToken cancellationToken)
    {
        var remaining = notBefore - _clock.UtcNow;

        if (remaining > TimeSpan.Zero)
        {
            // ponytail: a configurable response floor masks normal provider latency; use a queue
            // if measurements show the provider regularly exceeds it.
            await Task.Delay(remaining, cancellationToken);
        }
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

    private static int RequirePositiveValue(int value, string settingName)
    {
        if (value <= 0)
        {
            throw new InvalidOperationException($"Application setting '{settingName}' must be a positive integer.");
        }

        return value;
    }
}
