using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Contracts.Admin;

public sealed record RedemptionInspectRequest(
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("barcodeValue")] string? BarcodeValue);
