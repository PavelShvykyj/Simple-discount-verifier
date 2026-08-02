namespace SimpleDiscountVerifier.Api.Application.Redemptions;

public static class RedemptionDefaults
{
    public const int SmsMaxAttempts = 3;

    /// <summary>
    /// Bounded number of optimistic-concurrency retries when atomically reserving an SMS send
    /// slot for a phone runtime key. Exceeding this under contention fails closed (throttled).
    /// </summary>
    public const int RateLimitMaxAttempts = 5;
}
