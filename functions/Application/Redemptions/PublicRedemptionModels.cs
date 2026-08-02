namespace SimpleDiscountVerifier.Api.Application.Redemptions;

public sealed record StartRedemptionCommand(string? Phone, string? TurnstileToken);

public sealed record StartRedemptionResult(
    string RedemptionKey,
    string CorrelationId,
    bool SmsSent,
    int RetryAfterSeconds,
    DateTimeOffset SmsExpiresAt);

public sealed record VerifySmsCommand(string? RedemptionKey, string? Code);

public sealed record SmsVerificationResult(
    string CorrelationId,
    string BarcodeValue,
    string BarcodeFormat,
    DateTimeOffset ExpiresAt,
    int TtlSeconds);
