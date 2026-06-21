using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

namespace SimpleDiscountVerifier.Api.Application.Redemptions;

public sealed record DiscountRuntimeRecord(
    string PhoneRuntimeKey,
    NormalizedPhoneNumber Phone,
    string CorrelationId,
    string SmsCodeHash,
    int SmsAttempts,
    int SmsMaxAttempts,
    DateTimeOffset SmsSentAtUtc,
    DateTimeOffset SmsExpiresAtUtc,
    DateTimeOffset? PhoneVerifiedAtUtc,
    string? BarcodeHash,
    DateTimeOffset? BarcodeExpiresAtUtc,
    DateTimeOffset? BarcodeConsumedAtUtc,
    string? ConsumedByScanId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? ConcurrencyToken = null);
