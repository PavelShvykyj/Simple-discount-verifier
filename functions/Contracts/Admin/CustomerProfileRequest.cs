using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Contracts.Admin;

public sealed record CustomerProfileRequest(
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("answers")] IReadOnlyList<CustomerProfileAnswerRequest>? Answers);

public sealed record CustomerProfileAnswerRequest(
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("value")] string? Value);
