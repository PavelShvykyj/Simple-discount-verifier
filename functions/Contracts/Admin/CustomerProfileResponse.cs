using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Contracts.Admin;

public sealed record CustomerProfileResponse(
    [property: JsonPropertyName("phone")] string Phone,
    [property: JsonPropertyName("physicalCardNumber")] string PhysicalCardNumber,
    [property: JsonPropertyName("answers")] IReadOnlyList<CustomerProfileAnswerDto> Answers,
    [property: JsonPropertyName("createdAt")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt);
