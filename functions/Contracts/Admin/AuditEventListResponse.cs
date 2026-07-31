using System.Text.Json.Serialization;

namespace SimpleDiscountVerifier.Api.Contracts.Admin;

public sealed record AuditEventListResponse(
    [property: JsonPropertyName("items")] IReadOnlyList<AuditEventResponse> Items,
    [property: JsonPropertyName("continuationToken")] string? ContinuationToken);

public sealed record AuditEventResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("phoneHash")] string? PhoneHash,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("actorType")] string ActorType,
    [property: JsonPropertyName("actorId")] string? ActorId,
    [property: JsonPropertyName("metadata")] IReadOnlyDictionary<string, string?> Metadata);
