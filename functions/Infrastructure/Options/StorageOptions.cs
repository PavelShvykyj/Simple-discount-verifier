namespace SimpleDiscountVerifier.Api.Infrastructure.Options;

public sealed class StorageOptions
{
    public string AppStorageConnectionString { get; set; } = string.Empty;

    public string ScannerSurveyTableName { get; set; } = string.Empty;

    public string CustomerProfilesTableName { get; set; } = string.Empty;

    public string DiscountRuntimeTableName { get; set; } = string.Empty;

    public string AuditEventsTableName { get; set; } = string.Empty;
}
