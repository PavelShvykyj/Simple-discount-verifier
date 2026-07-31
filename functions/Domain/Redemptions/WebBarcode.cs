namespace SimpleDiscountVerifier.Api.Domain.Redemptions;

public sealed record WebBarcode(string Value, string PhoneRuntimeKey, string CorrelationId)
{
    public const string Format = "code128";
    public const int TotalLength = PhoneRuntimeKeyGenerator.RuntimeKeyLength + CorrelationIdGenerator.CorrelationIdLength;

    public static string Create(string phoneRuntimeKey, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneRuntimeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        if (!PhoneRuntimeKeyGenerator.IsValid(phoneRuntimeKey))
        {
            throw new ArgumentException("Phone runtime key is malformed.", nameof(phoneRuntimeKey));
        }

        if (!CorrelationIdGenerator.IsValid(correlationId))
        {
            throw new ArgumentException("Correlation id is malformed.", nameof(correlationId));
        }

        return string.Concat(phoneRuntimeKey, correlationId);
    }

    public static bool TryParse(string? value, out WebBarcode barcode)
    {
        barcode = default!;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.Length != TotalLength || !Base32NoPadding.IsEncodedValue(trimmed))
        {
            return false;
        }

        var phoneRuntimeKey = trimmed[..PhoneRuntimeKeyGenerator.RuntimeKeyLength];
        var correlationId = trimmed[PhoneRuntimeKeyGenerator.RuntimeKeyLength..];

        barcode = new WebBarcode(
            trimmed,
            phoneRuntimeKey,
            correlationId);

        return true;
    }
}
