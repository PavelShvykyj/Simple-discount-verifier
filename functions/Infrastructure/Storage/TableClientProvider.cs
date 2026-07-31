using Azure.Data.Tables;
using Microsoft.Extensions.Options;
using SimpleDiscountVerifier.Api.Infrastructure.Options;

namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

public sealed class TableClientProvider
{
    private const string StorageConnectionSettingName = "AppStorageConnectionString";

    private readonly StorageOptions _options;

    public TableClientProvider(IOptions<StorageOptions> options)
    {
        _options = options.Value;

        if (string.IsNullOrWhiteSpace(_options.AppStorageConnectionString))
        {
            throw new InvalidOperationException(
                $"Application setting '{StorageConnectionSettingName}' is required.");
        }
    }

    public TableClient CreateCustomerProfilesClient() =>
        CreateClient(_options.CustomerProfilesTableName, "CustomerProfilesTableName");

    public TableClient CreateDiscountRuntimeClient() =>
        CreateClient(_options.DiscountRuntimeTableName, "DiscountRuntimeTableName");

    public TableClient CreateAuditEventsClient() =>
        CreateClient(_options.AuditEventsTableName, "AuditEventsTableName");

    private TableClient CreateClient(string tableName, string settingName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new InvalidOperationException($"Application setting '{settingName}' is required.");
        }

        return new TableClient(_options.AppStorageConnectionString, tableName);
    }
}
