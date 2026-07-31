namespace SimpleDiscountVerifier.Api.Contracts.Public;

public sealed record StartRedemptionResponse(
    string RedemptionKey,
    string CorrelationId,
    bool SmsSent,
    int RetryAfterSeconds,
    DateTimeOffset SmsExpiresAt);
