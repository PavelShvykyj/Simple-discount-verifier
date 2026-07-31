namespace SimpleDiscountVerifier.Api.Infrastructure.Options;

public sealed class RuntimeOptions
{
    public int BarcodeTtlSeconds { get; set; }

    public int DiscountRuntimeRetentionHours { get; set; }

    public int AuditEventsRetentionDays { get; set; }

    public string CleanupAutomationKey { get; set; } = string.Empty;
}
