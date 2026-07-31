using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Contracts.Admin;

public sealed record CustomerProfileListResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<CustomerProfileResponse> Items,
    [property: JsonPropertyName("continuationToken")] string? ContinuationToken);
