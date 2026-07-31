namespace SimpleDiscountVerifier.Api.Infrastructure.Options;

public sealed class TelemetryOptions
{
    public string ApplicationInsightsConnectionString { get; set; } = string.Empty;
}
