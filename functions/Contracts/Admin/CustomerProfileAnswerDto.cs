using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Contracts.Admin;

public sealed record CustomerProfileAnswerDto(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string? Value);
