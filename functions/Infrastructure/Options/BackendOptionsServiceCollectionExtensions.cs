using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleDiscountVerifier.Api.Infrastructure.Options;

public static class BackendOptionsServiceCollectionExtensions
{
    public static IServiceCollection AddBackendOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<StorageOptions>(options =>
        {
            options.AppStorageConnectionString = GetValue(configuration, "AppStorageConnectionString");
            options.ScannerSurveyTableName = GetValue(configuration, "ScannerSurveyTableName");
            options.CustomerProfilesTableName = GetValue(configuration, "CustomerProfilesTableName");
            options.DiscountRuntimeTableName = GetValue(configuration, "DiscountRuntimeTableName");
            options.AuditEventsTableName = GetValue(configuration, "AuditEventsTableName");
        });

        services.Configure<PosOptions>(options =>
        {
            options.MainClientId = GetValue(configuration, "PosMainClientId");
            options.MainClientHmacSecret = GetValue(configuration, "PosMainClientHmacSecret");
            options.RequestFreshnessToleranceSeconds = GetInt(configuration, "PosRequestFreshnessToleranceSeconds");
        });

        services.Configure<HashingOptions>(options =>
        {
            options.PhoneRuntimeKeySecret = GetValue(configuration, "PhoneRuntimeKeySecret");
            options.SmsCodeHashSecret = GetValue(configuration, "SmsCodeHashSecret");
            options.BarcodeHashSecret = GetValue(configuration, "BarcodeHashSecret");
            options.AuditPhoneHashSecret = GetValue(configuration, "AuditPhoneHashSecret");
        });

        services.Configure<SmsOptions>(options =>
        {
            options.SmsFlyApiKey = GetValue(configuration, "SmsFlyApiKey");
            options.SmsFlySender = GetValue(configuration, "SmsFlySender");
            options.CodeTtlSeconds = GetInt(configuration, "SmsCodeTtlSeconds");
            options.RetryAfterSeconds = GetInt(configuration, "SmsRetryAfterSeconds");
            options.MaxPerHour = GetInt(configuration, "SmsMaxPerHour");
            options.MaxPerDay = GetInt(configuration, "SmsMaxPerDay");
            options.ResponseFloorMilliseconds = GetInt(configuration, "SmsResponseFloorMilliseconds", 1500);
        });

        services.Configure<RuntimeOptions>(options =>
        {
            options.BarcodeTtlSeconds = GetInt(configuration, "BarcodeTtlSeconds");
            options.DiscountRuntimeRetentionHours = GetInt(configuration, "DiscountRuntimeRetentionHours");
            options.AuditEventsRetentionDays = GetInt(configuration, "AuditEventsRetentionDays");
            options.CleanupAutomationKey = GetValue(configuration, "CleanupAutomationKey");
        });

        services.Configure<TelemetryOptions>(options =>
        {
            options.ApplicationInsightsConnectionString = GetValue(
                configuration,
                "APPLICATIONINSIGHTS_CONNECTION_STRING");
        });

        services.Configure<TurnstileOptions>(options =>
        {
            options.Enabled = GetBool(configuration, "TurnstileEnabled");
            options.SecretKey = GetValue(configuration, "TurnstileSecretKey");
            options.SiteKey = GetValue(configuration, "TurnstileSiteKey");
            options.ExpectedHostname = GetValue(configuration, "TurnstileExpectedHostname");
        });

        return services;
    }

    private static string GetValue(IConfiguration configuration, string name)
    {
        var value = configuration[name];
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value;
    }

    private static int GetInt(IConfiguration configuration, string name, int defaultValue = 0)
    {
        var value = configuration[name];

        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Application setting '{name}' must be a valid integer.");
    }

    private static bool GetBool(IConfiguration configuration, string name)
    {
        var value = configuration[name];

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return bool.TryParse(value, out var parsed)
            ? parsed
            : throw new InvalidOperationException($"Application setting '{name}' must be true or false.");
    }
}
