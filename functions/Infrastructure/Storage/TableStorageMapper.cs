using Azure;
using Azure.Data.Tables;

namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

internal static class TableStorageMapper
{
    public static string? ToConcurrencyToken(ETag etag)
    {
        var value = etag.ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public static DateTimeOffset GetRequiredDateTimeOffset(TableEntity entity, string propertyName)
    {
        return entity.TryGetValue(propertyName, out var value) && value is DateTimeOffset dateTimeOffset
            ? dateTimeOffset
            : throw new InvalidOperationException($"Table entity property '{propertyName}' is required.");
    }

    public static DateTimeOffset? GetOptionalDateTimeOffset(TableEntity entity, string propertyName)
    {
        return entity.TryGetValue(propertyName, out var value) && value is DateTimeOffset dateTimeOffset
            ? dateTimeOffset
            : null;
    }

    public static string GetRequiredString(TableEntity entity, string propertyName)
    {
        return entity.TryGetValue(propertyName, out var value) && value is string text
            ? text
            : throw new InvalidOperationException($"Table entity property '{propertyName}' is required.");
    }

    public static string? GetOptionalString(TableEntity entity, string propertyName)
    {
        return entity.TryGetValue(propertyName, out var value) ? value as string : null;
    }

    public static int GetRequiredInt32(TableEntity entity, string propertyName)
    {
        return entity.TryGetValue(propertyName, out var value) && value is int number
            ? number
            : throw new InvalidOperationException($"Table entity property '{propertyName}' is required.");
    }

    public static void AddIfNotNull(TableEntity entity, string propertyName, object? value)
    {
        if (value is not null)
        {
            entity[propertyName] = value;
        }
    }
}
