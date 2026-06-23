using System.Net;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Application.Audit;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.Redemptions;
using SimpleDiscountVerifier.Api.Domain.Redemptions;
using SimpleDiscountVerifier.Api.Domain.Security;
using SimpleDiscountVerifier.Api.Domain.Shared;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Application.Pos;

public sealed class PosBarcodeValidationService
{
    private const string BarcodeHashSecretSettingName = "BarcodeHashSecret";

    private readonly IDiscountRuntimeRepository _runtime;
    private readonly IAuditWriter _auditWriter;
    private readonly HashingOptions _hashingOptions;
    private readonly IClock _clock;

    public PosBarcodeValidationService(
        IDiscountRuntimeRepository runtime,
        IAuditWriter auditWriter,
        IOptions<HashingOptions> hashingOptions,
        IClock clock)
    {
        _runtime = runtime;
        _auditWriter = auditWriter;
        _hashingOptions = hashingOptions.Value;
        _clock = clock;
    }

    public async Task<ApplicationResult<PosBarcodeValidationResult>> ValidateAsync(
        ValidatePosBarcodeCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.BarcodeValue)
            || string.IsNullOrWhiteSpace(command.ScanId))
        {
            return Failure(
                PosErrorCodes.InvalidRequest,
                "Barcode value and scanId are required.",
                HttpStatusCode.BadRequest);
        }

        var scanId = command.ScanId.Trim();
        var now = _clock.UtcNow;

        if (!WebBarcode.TryParse(command.BarcodeValue, out var barcode))
        {
            return BusinessFailure(PosBarcodeValidationReasons.InvalidFormat, correlationId: null, now);
        }

        var runtime = await _runtime.GetCurrentAsync(barcode.PhoneRuntimeKey, cancellationToken);

        if (runtime is null)
        {
            return BusinessFailure(PosBarcodeValidationReasons.Unknown, barcode.CorrelationId, now);
        }

        await WriteAuditAsync(
            runtime,
            PosAuditEventTypes.BarcodeValidationRequested,
            command,
            cancellationToken);

        if (!string.Equals(runtime.CorrelationId, barcode.CorrelationId, StringComparison.Ordinal))
        {
            return await FailureWithAuditAsync(
                runtime,
                command,
                PosBarcodeValidationReasons.Unknown,
                now,
                cancellationToken);
        }

        if (runtime.BarcodeConsumedAtUtc is not null)
        {
            if (string.Equals(runtime.ConsumedByScanId, scanId, StringComparison.Ordinal))
            {
                await WriteAuditAsync(
                    runtime,
                    PosAuditEventTypes.BarcodeValidationIdempotentReplay,
                    command,
                    cancellationToken);

                return ApplicationResult<PosBarcodeValidationResult>.Success(
                    new PosBarcodeValidationSuccessResult(
                        runtime.Phone.Value,
                        runtime.CorrelationId,
                        runtime.BarcodeConsumedAtUtc.Value,
                        IdempotentReplay: true));
            }

            return await FailureWithAuditAsync(
                runtime,
                command,
                PosBarcodeValidationReasons.AlreadyUsed,
                now,
                cancellationToken);
        }

        if (runtime.BarcodeExpiresAtUtc is null || now > runtime.BarcodeExpiresAtUtc.Value)
        {
            await WriteAuditAsync(
                runtime,
                PosAuditEventTypes.BarcodeExpired,
                command,
                cancellationToken);

            return await FailureWithAuditAsync(
                runtime,
                command,
                PosBarcodeValidationReasons.Expired,
                now,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(runtime.BarcodeHash)
            || !SecretHasher.FixedTimeEquals(
                runtime.BarcodeHash,
                barcode.Value,
                RequireSetting(_hashingOptions.BarcodeHashSecret, BarcodeHashSecretSettingName)))
        {
            return await FailureWithAuditAsync(
                runtime,
                command,
                PosBarcodeValidationReasons.Unknown,
                now,
                cancellationToken);
        }

        var consumed = runtime with
        {
            BarcodeHash = null,
            BarcodeConsumedAtUtc = now,
            ConsumedByScanId = scanId,
            UpdatedAtUtc = now
        };

        var writeResult = await ReplaceRuntimeAsync(consumed, scanId, cancellationToken);

        if (writeResult is not null)
        {
            return writeResult;
        }

        await WriteAuditAsync(
            consumed,
            PosAuditEventTypes.BarcodeValidationSucceeded,
            command,
            cancellationToken);
        await WriteAuditAsync(
            consumed,
            PosAuditEventTypes.BarcodeConsumed,
            command,
            cancellationToken);

        return ApplicationResult<PosBarcodeValidationResult>.Success(
            new PosBarcodeValidationSuccessResult(
                runtime.Phone.Value,
                runtime.CorrelationId,
                now,
                IdempotentReplay: false));
    }

    private async Task<ApplicationResult<PosBarcodeValidationResult>?> ReplaceRuntimeAsync(
        DiscountRuntimeRecord consumed,
        string scanId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(consumed.ConcurrencyToken))
        {
            return Failure(
                PosErrorCodes.InternalError,
                "Stored redemption concurrency token is missing.",
                HttpStatusCode.InternalServerError);
        }

        var result = await _runtime.ReplaceCurrentAsync(
            consumed,
            consumed.ConcurrencyToken,
            cancellationToken);

        return result.Status switch
        {
            StorageWriteStatus.Updated => null,
            StorageWriteStatus.NotFound => ApplicationResult<PosBarcodeValidationResult>.Success(
                new PosBarcodeValidationFailureResult(
                    PosBarcodeValidationReasons.Unknown,
                    consumed.CorrelationId,
                    _clock.UtcNow)),
            StorageWriteStatus.PreconditionFailed => await ResolveConcurrentConsumptionAsync(
                consumed.PhoneRuntimeKey,
                consumed.CorrelationId,
                scanId,
                cancellationToken),
            _ => Failure(
                PosErrorCodes.InternalError,
                "Barcode validation state could not be updated.",
                HttpStatusCode.InternalServerError)
        };
    }

    private async Task<ApplicationResult<PosBarcodeValidationResult>> ResolveConcurrentConsumptionAsync(
        string phoneRuntimeKey,
        string correlationId,
        string scanId,
        CancellationToken cancellationToken)
    {
        var latest = await _runtime.GetCurrentAsync(phoneRuntimeKey, cancellationToken);
        var now = _clock.UtcNow;

        if (latest is not null
            && string.Equals(latest.CorrelationId, correlationId, StringComparison.Ordinal)
            && latest.BarcodeConsumedAtUtc is not null
            && string.Equals(latest.ConsumedByScanId, scanId, StringComparison.Ordinal))
        {
            return ApplicationResult<PosBarcodeValidationResult>.Success(
                new PosBarcodeValidationSuccessResult(
                    latest.Phone.Value,
                    latest.CorrelationId,
                    latest.BarcodeConsumedAtUtc.Value,
                    IdempotentReplay: true));
        }

        return ApplicationResult<PosBarcodeValidationResult>.Success(
            new PosBarcodeValidationFailureResult(
                PosBarcodeValidationReasons.AlreadyUsed,
                correlationId,
                now));
    }

    private async Task<ApplicationResult<PosBarcodeValidationResult>> FailureWithAuditAsync(
        DiscountRuntimeRecord runtime,
        ValidatePosBarcodeCommand command,
        string reason,
        DateTimeOffset validatedAt,
        CancellationToken cancellationToken)
    {
        await WriteAuditAsync(
            runtime,
            PosAuditEventTypes.BarcodeValidationFailed,
            command,
            cancellationToken,
            new Dictionary<string, string?> { ["reason"] = reason });

        return BusinessFailure(reason, runtime.CorrelationId, validatedAt);
    }

    private static ApplicationResult<PosBarcodeValidationResult> BusinessFailure(
        string reason,
        string? correlationId,
        DateTimeOffset validatedAt) =>
        ApplicationResult<PosBarcodeValidationResult>.Success(
            new PosBarcodeValidationFailureResult(reason, correlationId, validatedAt));

    private static ApplicationResult<PosBarcodeValidationResult> Failure(
        string code,
        string message,
        HttpStatusCode statusCode) =>
        ApplicationResult<PosBarcodeValidationResult>.Failure(
            new ApplicationError(code, message, statusCode));

    private async Task WriteAuditAsync(
        DiscountRuntimeRecord runtime,
        string eventType,
        ValidatePosBarcodeCommand command,
        CancellationToken cancellationToken,
        IReadOnlyDictionary<string, string?>? metadata = null)
    {
        await _auditWriter.WriteAsync(
            new AuditWriteRequest(
                runtime.CorrelationId,
                eventType,
                AuditActorTypes.Pos,
                command.Client.ClientId,
                runtime.Phone,
                MergeMetadata(command, metadata)),
            cancellationToken);
    }

    private static IReadOnlyDictionary<string, string?> MergeMetadata(
        ValidatePosBarcodeCommand command,
        IReadOnlyDictionary<string, string?>? metadata)
    {
        var merged = new Dictionary<string, string?>
        {
            ["scanId"] = command.ScanId,
            ["terminalId"] = command.TerminalId,
            ["branchId"] = command.BranchId
        };

        if (metadata is not null)
        {
            foreach (var item in metadata)
            {
                merged[item.Key] = item.Value;
            }
        }

        return merged;
    }

    private static string RequireSetting(string value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Application setting '{settingName}' is required.");
        }

        return value;
    }
}
