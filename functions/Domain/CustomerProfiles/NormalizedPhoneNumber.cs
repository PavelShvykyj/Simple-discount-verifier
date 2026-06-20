using System.Text;

namespace SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

public sealed record NormalizedPhoneNumber
{
    private NormalizedPhoneNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public string StorageKey => Value[1..];

    public static bool TryCreate(string? input, out NormalizedPhoneNumber phone)
    {
        phone = default!;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();
        var digits = new StringBuilder(trimmed.Length);

        foreach (var character in trimmed)
        {
            if (char.IsDigit(character))
            {
                digits.Append(character);
                continue;
            }

            if (character is '+' or ' ' or '-' or '(' or ')')
            {
                continue;
            }

            return false;
        }

        var normalizedDigits = NormalizeDigits(digits.ToString());

        if (normalizedDigits is null)
        {
            return false;
        }

        phone = new NormalizedPhoneNumber($"+{normalizedDigits}");
        return true;
    }

    private static string? NormalizeDigits(string digits)
    {
        if (digits.Length == 12 && digits.StartsWith("380", StringComparison.Ordinal))
        {
            return IsValidUkrainianNationalPart(digits[3..]) ? digits : null;
        }

        if (digits.Length == 10 && digits.StartsWith('0'))
        {
            var nationalPart = digits[1..];
            return IsValidUkrainianNationalPart(nationalPart) ? $"380{nationalPart}" : null;
        }

        return null;
    }

    private static bool IsValidUkrainianNationalPart(string nationalPart)
    {
        return nationalPart.Length == 9 && nationalPart[0] is '3' or '4' or '5' or '6' or '7' or '8' or '9';
    }
}
