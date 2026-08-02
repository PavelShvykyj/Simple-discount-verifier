namespace SimpleDiscountVerifier.Api.Infrastructure.Options;

public sealed class TurnstileOptions
{
    public const string StartRedemptionAction = "start_redemption";

    /// <summary>
    /// Kill switch: set to <c>false</c> to skip verification entirely (e.g. during a Cloudflare
    /// Turnstile outage) without a redeploy. Verification is fail-closed while enabled.
    /// </summary>
    public bool Enabled { get; set; }

    public string SecretKey { get; set; } = string.Empty;

    public string SiteKey { get; set; } = string.Empty;

    public string ExpectedHostname { get; set; } = string.Empty;
}
