namespace SimpleDiscountVerifier.Api.Contracts.Public;

public sealed record SmsVerificationResponse(
    string CorrelationId,
    string BarcodeValue,
    string BarcodeFormat,
    DateTimeOffset ExpiresAt,
    int TtlSeconds);
