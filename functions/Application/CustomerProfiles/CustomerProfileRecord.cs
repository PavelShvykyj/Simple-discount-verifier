using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

namespace SimpleDiscountVerifier.Api.Application.CustomerProfiles;

public sealed record CustomerProfileRecord(
    NormalizedPhoneNumber Phone,
    IReadOnlyList<QuestionnaireAnswer> Answers,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? ConcurrencyToken = null);
