namespace SimpleDiscountVerifier.Api.Infrastructure.Options;

public sealed class PosOptions
{
    public string MainClientId { get; set; } = string.Empty;

    public string MainClientHmacSecret { get; set; } = string.Empty;

    public int RequestFreshnessToleranceSeconds { get; set; }
}
