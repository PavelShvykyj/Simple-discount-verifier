namespace SimpleDiscountVerifier.Api.Domain.Redemptions;

/// <summary>
/// Rolling counters used to decide whether another SMS send should be allowed for a given
/// <c>phoneRuntimeKey</c>. Persisted alongside the discount runtime row so the decision can be
/// evaluated atomically (via optimistic concurrency) on every <c>StartRedemption</c> call,
/// regardless of whether the phone number belongs to a known customer profile.
/// </summary>
public sealed record SmsRateLimitState(
    DateTimeOffset LastSentAtUtc,
    DateTimeOffset HourWindowStartUtc,
    int HourWindowCount,
    DateTimeOffset DayWindowStartUtc,
    int DayWindowCount);

public sealed record SmsRateLimitDecision(bool Allowed, SmsRateLimitState State);

/// <summary>
/// Pure domain logic that enforces a minimum interval between sends plus fixed hourly/daily
/// send caps for a single target (phone runtime key). Has no storage or transport dependency so
/// it can be evaluated repeatedly inside an optimistic-concurrency retry loop.
/// </summary>
public static class SmsSendRateLimiter
{
    public static SmsRateLimitDecision Evaluate(
        SmsRateLimitState? existing,
        DateTimeOffset now,
        int minIntervalSeconds,
        int maxPerHour,
        int maxPerDay)
    {
        if (existing is null)
        {
            return Allow(now, now, 1, now, 1);
        }

        if (now < existing.LastSentAtUtc.AddSeconds(minIntervalSeconds))
        {
            return Deny(existing);
        }

        var hourWindowExpired = now - existing.HourWindowStartUtc >= TimeSpan.FromHours(1);
        var hourWindowStart = hourWindowExpired ? now : existing.HourWindowStartUtc;
        var hourWindowCount = hourWindowExpired ? 0 : existing.HourWindowCount;

        if (hourWindowCount >= maxPerHour)
        {
            return Deny(existing with { HourWindowStartUtc = hourWindowStart, HourWindowCount = hourWindowCount });
        }

        var dayWindowExpired = now - existing.DayWindowStartUtc >= TimeSpan.FromDays(1);
        var dayWindowStart = dayWindowExpired ? now : existing.DayWindowStartUtc;
        var dayWindowCount = dayWindowExpired ? 0 : existing.DayWindowCount;

        if (dayWindowCount >= maxPerDay)
        {
            return Deny(existing with { DayWindowStartUtc = dayWindowStart, DayWindowCount = dayWindowCount });
        }

        return Allow(now, hourWindowStart, hourWindowCount + 1, dayWindowStart, dayWindowCount + 1);
    }

    private static SmsRateLimitDecision Allow(
        DateTimeOffset lastSentAtUtc,
        DateTimeOffset hourWindowStartUtc,
        int hourWindowCount,
        DateTimeOffset dayWindowStartUtc,
        int dayWindowCount) =>
        new(true, new SmsRateLimitState(lastSentAtUtc, hourWindowStartUtc, hourWindowCount, dayWindowStartUtc, dayWindowCount));

    private static SmsRateLimitDecision Deny(SmsRateLimitState state) => new(false, state);
}
