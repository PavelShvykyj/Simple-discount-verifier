namespace SimpleDiscountVerifier.Api.Application.Audit;

public sealed record AuditEventRecord(
    string CorrelationId,
    string EventType,
    DateTimeOffset OccurredAtUtc,
    string ActorType,
    string? ActorId,
    string? PhoneHash,
    IReadOnlyDictionary<string, string?> Metadata);
