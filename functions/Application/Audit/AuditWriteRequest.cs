using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

namespace SimpleDiscountVerifier.Api.Application.Audit;

public sealed record AuditWriteRequest(
    string CorrelationId,
    string EventType,
    string ActorType,
    string? ActorId = null,
    NormalizedPhoneNumber? Phone = null,
    IReadOnlyDictionary<string, string?>? Metadata = null);
