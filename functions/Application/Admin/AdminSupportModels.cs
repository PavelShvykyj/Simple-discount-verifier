using SimpleDiscountVerifier.Api.Contracts.Admin;

namespace SimpleDiscountVerifier.Api.Application.Admin;

public sealed record AdminAuditEventsQuery(
    string? CorrelationId,
    string? Phone,
    int? PageSize,
    string? ContinuationToken);

public sealed record AdminAuditEventsResult(
    IReadOnlyList<AuditEventResponse> Items,
    string? ContinuationToken);

public sealed record AdminInspectCommand(
    string? CorrelationId,
    string? BarcodeValue);

public sealed record AdminInspectResult(
    string CorrelationId,
    RedemptionInspectSummary Redemption,
    BarcodeInspectSummary Barcode,
    ScanInspectSummary? Scan,
    CustomerProfileResponse? Profile,
    IReadOnlyList<AuditEventResponse> AuditEvents);

public sealed record RedemptionInspectSummary(
    string Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? LastEventAt);

public sealed record BarcodeInspectSummary(
    string? Value,
    bool FormatValid,
    string? PhoneRuntimeKey,
    string? CorrelationId,
    string Status,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? ConsumedAt,
    string? ConsumedByScanId);

public sealed record ScanInspectSummary(
    string? ScanId,
    IReadOnlyDictionary<string, string?> Parsed);
