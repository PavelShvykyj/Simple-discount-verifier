namespace SimpleDiscountVerifier.Api.Infrastructure.Options;

public sealed class SmsOptions
{
    public string SmsFlyApiKey { get; set; } = string.Empty;

    public string SmsFlySender { get; set; } = string.Empty;

    public int CodeTtlSeconds { get; set; }

    public int RetryAfterSeconds { get; set; }

    public int MaxPerHour { get; set; }

    public int MaxPerDay { get; set; }

    public int ResponseFloorMilliseconds { get; set; }
}
