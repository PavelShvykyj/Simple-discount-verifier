using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

namespace SimpleDiscountVerifier.Api.Application.CustomerProfiles;

public sealed record CustomerProfileRecord(
    NormalizedPhoneNumber Phone,
    PhysicalCardNumber PhysicalCardNumber,
    IReadOnlyList<QuestionnaireAnswer> Answers,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? ConcurrencyToken = null);
