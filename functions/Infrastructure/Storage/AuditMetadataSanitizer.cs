namespace SimpleDiscountVerifier.Api.Infrastructure.Storage;

internal static class AuditMetadataSanitizer
{
    private static readonly HashSet<string> SensitiveMetadataKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "phone",
        "rawPhone",
        "physicalCardNumber",
        "smsCode",
        "barcodeValue",
        "requestBody"
    };

    public static IReadOnlyDictionary<string, string?> Sanitize(IReadOnlyDictionary<string, string?>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return new Dictionary<string, string?>();
        }

        return metadata
            .Where(item => !SensitiveMetadataKeys.Contains(item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
    }
}
