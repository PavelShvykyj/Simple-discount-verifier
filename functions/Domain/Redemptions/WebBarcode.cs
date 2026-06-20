using System.Text.RegularExpressions;

namespace SimpleDiscountVerifier.Api.Domain.Redemptions;

public sealed partial record WebBarcode(string Value, string PhoneRuntimeKey, string CorrelationId)
{
    public const string Prefix = "SDV";
    public const string Format = "code128";

    public static string Create(string phoneRuntimeKey, string correlationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneRuntimeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return $"{Prefix}-{phoneRuntimeKey}-{correlationId}";
    }

    public static bool TryParse(string? value, out WebBarcode barcode)
    {
        barcode = default!;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var match = BarcodePattern().Match(value.Trim());

        if (!match.Success)
        {
            return false;
        }

        barcode = new WebBarcode(
            match.Value,
            match.Groups["phoneRuntimeKey"].Value,
            match.Groups["correlationId"].Value);

        return true;
    }

    [GeneratedRegex(@"^SDV-(?<phoneRuntimeKey>p_[A-Z2-7]{16})-(?<correlationId>c_[A-Z2-7]{26})$", RegexOptions.CultureInvariant)]
    private static partial Regex BarcodePattern();
}
