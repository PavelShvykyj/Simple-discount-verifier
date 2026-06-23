using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Contracts.Admin;

public sealed record RedemptionInspectResponse(
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("redemption")] RedemptionInspectSummaryResponse Redemption,
    [property: JsonPropertyName("barcode")] BarcodeInspectSummaryResponse Barcode,
    [property: JsonPropertyName("scan")] ScanInspectSummaryResponse? Scan,
    [property: JsonPropertyName("profile")] CustomerProfileResponse? Profile,
    [property: JsonPropertyName("auditEvents")] IReadOnlyList<AuditEventResponse> AuditEvents);

public sealed record RedemptionInspectSummaryResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("startedAt")] DateTimeOffset? StartedAt,
    [property: JsonPropertyName("lastEventAt")] DateTimeOffset? LastEventAt);

public sealed record BarcodeInspectSummaryResponse(
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("formatValid")] bool FormatValid,
    [property: JsonPropertyName("phoneRuntimeKey")] string? PhoneRuntimeKey,
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset? ExpiresAt,
    [property: JsonPropertyName("consumedAt")] DateTimeOffset? ConsumedAt,
    [property: JsonPropertyName("consumedByScanId")] string? ConsumedByScanId);

public sealed record ScanInspectSummaryResponse(
    [property: JsonPropertyName("scanId")] string? ScanId,
    [property: JsonPropertyName("parsed")] IReadOnlyDictionary<string, string?> Parsed);
