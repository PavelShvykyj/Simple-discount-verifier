using System.Net;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Application.Audit;
using SimpleDiscountVerifier.Api.Application.Common;
using SimpleDiscountVerifier.Api.Application.CustomerProfiles;
using SimpleDiscountVerifier.Api.Application.Pos;
using SimpleDiscountVerifier.Api.Application.Redemptions;
using SimpleDiscountVerifier.Api.Contracts.Admin;
using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;
using SimpleDiscountVerifier.Api.Domain.Redemptions;
using SimpleDiscountVerifier.Api.Domain.Security;
using SimpleDiscountVerifier.Api.Domain.Shared;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Application.Admin;

public sealed class AdminSupportService
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;
    private const string AuditPhoneHashSecretSettingName = "AuditPhoneHashSecret";

    private readonly IAuditEventRepository _auditEvents;
    private readonly IDiscountRuntimeRepository _runtime;
    private readonly ICustomerProfileRepository _profiles;
    private readonly HashingOptions _hashingOptions;
    private readonly IClock _clock;

    public AdminSupportService(
        IAuditEventRepository auditEvents,
        IDiscountRuntimeRepository runtime,
        ICustomerProfileRepository profiles,
        IOptions<HashingOptions> hashingOptions,
        IClock clock)
    {
        _auditEvents = auditEvents;
        _runtime = runtime;
        _profiles = profiles;
        _hashingOptions = hashingOptions.Value;
        _clock = clock;
    }

    public async Task<ApplicationResult<AdminAuditEventsResult>> ListAuditEventsAsync(
        AdminAuditEventsQuery query,
        CancellationToken cancellationToken)
    {
        var pageSize = NormalizePageSize(query.PageSize);
        var correlationId = NormalizeCorrelationId(query.CorrelationId);

        if (correlationId is not null && !CorrelationIdGenerator.IsValid(correlationId))
        {
            return Failure<AdminAuditEventsResult>(
                AdminSupportErrorCodes.InvalidRequest,
                "Correlation id is malformed.",
                HttpStatusCode.BadRequest);
        }

        var phoneHash = TryHashPhone(query.Phone, out var hashError);

        if (hashError is not null)
        {
            return ApplicationResult<AdminAuditEventsResult>.Failure(hashError);
        }

        var page = await _auditEvents.ListAsync(
            correlationId,
            phoneHash,
            pageSize,
            query.ContinuationToken,
            cancellationToken);

        return ApplicationResult<AdminAuditEventsResult>.Success(
            new AdminAuditEventsResult(
                page.Items.Select(ToAuditEventResponse).ToArray(),
                page.ContinuationToken));
    }

    public async Task<ApplicationResult<AdminInspectResult>> InspectAsync(
        AdminInspectCommand command,
        CancellationToken cancellationToken)
    {
        var suppliedCorrelationId = NormalizeCorrelationId(command.CorrelationId);

        if (suppliedCorrelationId is not null && !CorrelationIdGenerator.IsValid(suppliedCorrelationId))
        {
            return Failure<AdminInspectResult>(
                AdminSupportErrorCodes.InvalidRequest,
                "Correlation id is malformed.",
                HttpStatusCode.BadRequest);
        }

        var hasBarcodeValue = !string.IsNullOrWhiteSpace(command.BarcodeValue);
        var barcode = TryParseBarcode(command.BarcodeValue);
        var hasInvalidBarcodeValue = hasBarcodeValue && barcode is null;

        if (suppliedCorrelationId is null && !hasBarcodeValue)
        {
            return Failure<AdminInspectResult>(
                AdminSupportErrorCodes.InvalidRequest,
                "Correlation id or barcode value is required.",
                HttpStatusCode.BadRequest);
        }

        if (suppliedCorrelationId is null && hasInvalidBarcodeValue)
        {
            return Failure<AdminInspectResult>(
                AdminSupportErrorCodes.InvalidRequest,
                "Barcode value is malformed.",
                HttpStatusCode.BadRequest);
        }

        if (suppliedCorrelationId is not null
            && barcode is not null
            && !string.Equals(suppliedCorrelationId, barcode.CorrelationId, StringComparison.Ordinal))
        {
            return Failure<AdminInspectResult>(
                AdminSupportErrorCodes.InvalidRequest,
                "Correlation id and barcode value refer to different redemption attempts.",
                HttpStatusCode.BadRequest);
        }

        var correlationId = suppliedCorrelationId ?? barcode!.CorrelationId;
        var auditPage = await _auditEvents.ListByCorrelationIdAsync(
            correlationId,
            MaxPageSize,
            continuationToken: null,
            cancellationToken);
        var auditEvents = auditPage.Items.OrderBy(item => item.OccurredAtUtc).ToArray();
        var runtime = barcode is not null
            ? await _runtime.GetCurrentAsync(barcode.PhoneRuntimeKey, cancellationToken)
            : await _runtime.GetByCorrelationIdAsync(correlationId, cancellationToken);
        var profile = runtime is null
            ? null
            : await _profiles.GetByPhoneAsync(runtime.Phone, cancellationToken);
        var lastEvent = auditEvents.LastOrDefault();

        return ApplicationResult<AdminInspectResult>.Success(
            new AdminInspectResult(
                correlationId,
                new RedemptionInspectSummary(
                    ResolveRedemptionStatus(auditEvents, runtime, _clock.UtcNow),
                    auditEvents.FirstOrDefault(item => item.EventType == RedemptionAuditEventTypes.RedemptionStarted)?.OccurredAtUtc,
                    lastEvent?.OccurredAtUtc),
                BuildBarcodeSummary(command.BarcodeValue, barcode, correlationId, runtime, _clock.UtcNow),
                BuildScanSummary(runtime),
                profile is null ? null : ToCustomerProfileResponse(profile),
                auditEvents.Select(ToAuditEventResponse).ToArray()));
    }

    private static BarcodeInspectSummary BuildBarcodeSummary(
        string? barcodeValue,
        WebBarcode? barcode,
        string correlationId,
        DiscountRuntimeRecord? runtime,
        DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(barcodeValue) && barcode is null)
        {
            return new BarcodeInspectSummary(
                barcodeValue,
                FormatValid: false,
                PhoneRuntimeKey: null,
                CorrelationId: null,
                Status: AdminSupportStatuses.InvalidFormat,
                ExpiresAt: null,
                ConsumedAt: null,
                ConsumedByScanId: null);
        }

        if (runtime is null)
        {
            return new BarcodeInspectSummary(
                barcodeValue,
                barcode is not null,
                barcode?.PhoneRuntimeKey,
                barcode?.CorrelationId ?? correlationId,
                barcode is null ? AdminSupportStatuses.NotIssued : AdminSupportStatuses.Unknown,
                ExpiresAt: null,
                ConsumedAt: null,
                ConsumedByScanId: null);
        }

        if (barcode is not null
            && !string.Equals(runtime.CorrelationId, barcode.CorrelationId, StringComparison.Ordinal))
        {
            return new BarcodeInspectSummary(
                barcodeValue,
                FormatValid: true,
                barcode.PhoneRuntimeKey,
                barcode.CorrelationId,
                AdminSupportStatuses.ReplacedByNewFlow,
                runtime.BarcodeExpiresAtUtc,
                runtime.BarcodeConsumedAtUtc,
                runtime.ConsumedByScanId);
        }

        return new BarcodeInspectSummary(
            barcodeValue,
            FormatValid: barcode is not null,
            barcode?.PhoneRuntimeKey ?? runtime.PhoneRuntimeKey,
            barcode?.CorrelationId ?? runtime.CorrelationId,
            ResolveBarcodeStatus(runtime, now),
            runtime.BarcodeExpiresAtUtc,
            runtime.BarcodeConsumedAtUtc,
            runtime.ConsumedByScanId);
    }

    private static ScanInspectSummary? BuildScanSummary(DiscountRuntimeRecord? runtime)
    {
        if (string.IsNullOrWhiteSpace(runtime?.ConsumedByScanId))
        {
            return null;
        }

        return new ScanInspectSummary(
            runtime.ConsumedByScanId,
            new Dictionary<string, string?> { ["raw"] = runtime.ConsumedByScanId });
    }

    private static string ResolveRedemptionStatus(
        IReadOnlyList<AuditEventRecord> auditEvents,
        DiscountRuntimeRecord? runtime,
        DateTimeOffset now)
    {
        if (HasEvent(auditEvents, PosAuditEventTypes.BarcodeConsumed)
            || runtime?.BarcodeConsumedAtUtc is not null)
        {
            return AdminSupportStatuses.BarcodeConsumed;
        }

        if (runtime?.BarcodeExpiresAtUtc is not null && runtime.BarcodeExpiresAtUtc < now)
        {
            return AdminSupportStatuses.BarcodeExpired;
        }

        if (HasEvent(auditEvents, RedemptionAuditEventTypes.BarcodeIssued))
        {
            return AdminSupportStatuses.BarcodeIssued;
        }

        if (HasEvent(auditEvents, RedemptionAuditEventTypes.SmsAttemptsExceeded)
            || HasEvent(auditEvents, RedemptionAuditEventTypes.SmsValidationFailed))
        {
            return AdminSupportStatuses.SmsFailed;
        }

        if (HasEvent(auditEvents, RedemptionAuditEventTypes.SmsSendFailed))
        {
            return AdminSupportStatuses.SmsSendFailed;
        }

        if (HasEvent(auditEvents, RedemptionAuditEventTypes.SmsSent))
        {
            return AdminSupportStatuses.SmsSent;
        }

        if (HasEvent(auditEvents, RedemptionAuditEventTypes.ProfileNotFound))
        {
            return AdminSupportStatuses.ProfileNotFound;
        }

        return HasEvent(auditEvents, RedemptionAuditEventTypes.RedemptionStarted)
            ? AdminSupportStatuses.Started
            : AdminSupportStatuses.Unknown;
    }

    private static string ResolveBarcodeStatus(DiscountRuntimeRecord runtime, DateTimeOffset now)
    {
        if (runtime.BarcodeConsumedAtUtc is not null)
        {
            return AdminSupportStatuses.Consumed;
        }

        if (runtime.BarcodeHash is null || runtime.BarcodeExpiresAtUtc is null)
        {
            return AdminSupportStatuses.NotIssued;
        }

        return runtime.BarcodeExpiresAtUtc < now
            ? AdminSupportStatuses.Expired
            : AdminSupportStatuses.Active;
    }

    private static bool HasEvent(IReadOnlyList<AuditEventRecord> auditEvents, string eventType) =>
        auditEvents.Any(item => string.Equals(item.EventType, eventType, StringComparison.Ordinal));

    private string? TryHashPhone(string? phoneValue, out ApplicationError? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(phoneValue))
        {
            return null;
        }

        if (!NormalizedPhoneNumber.TryCreate(phoneValue, out var phone))
        {
            error = new ApplicationError(
                AdminSupportErrorCodes.InvalidPhone,
                "Phone is missing or invalid.",
                HttpStatusCode.BadRequest);
            return null;
        }

        return SecretHasher.HashPhone(
            phone.Value,
            RequireSetting(_hashingOptions.AuditPhoneHashSecret, AuditPhoneHashSecretSettingName));
    }

    private static WebBarcode? TryParseBarcode(string? barcodeValue)
    {
        if (string.IsNullOrWhiteSpace(barcodeValue))
        {
            return null;
        }

        if (WebBarcode.TryParse(barcodeValue, out var barcode))
        {
            return barcode;
        }

        return null;
    }

    private static AuditEventResponse ToAuditEventResponse(AuditEventRecord auditEvent) =>
        new(
            auditEvent.Id ?? string.Empty,
            auditEvent.CorrelationId,
            auditEvent.EventType,
            auditEvent.PhoneHash,
            auditEvent.OccurredAtUtc,
            auditEvent.ActorType,
            auditEvent.ActorId,
            auditEvent.Metadata);

    private static CustomerProfileResponse ToCustomerProfileResponse(CustomerProfileRecord profile) =>
        new(
            profile.Phone.Value,
            profile.PhysicalCardNumber.Value,
            profile.Answers
                .Select(answer => new CustomerProfileAnswerDto(answer.Code, answer.Name, answer.Value))
                .ToArray(),
            profile.CreatedAtUtc,
            profile.UpdatedAtUtc);

    private static string? NormalizeCorrelationId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int NormalizePageSize(int? pageSize)
    {
        if (pageSize is null or <= 0)
        {
            return DefaultPageSize;
        }

        return Math.Min(pageSize.Value, MaxPageSize);
    }

    private static ApplicationResult<T> Failure<T>(
        string code,
        string message,
        HttpStatusCode statusCode) =>
        ApplicationResult<T>.Failure(new ApplicationError(code, message, statusCode));

    private static string RequireSetting(string value, string settingName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Application setting '{settingName}' is required.");
        }

        return value;
    }
}
