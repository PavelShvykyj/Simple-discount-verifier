using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

namespace SimpleDiscountVerifier.Api.Domain.Redemptions;

public static partial class PhoneRuntimeKeyGenerator
{
    private const int RuntimeKeyBytesLength = 10;
    private const string Prefix = "p_";
    private const string RuntimeKeyPatternValue = "^" + Prefix + @"[A-Z2-7]{16}$";

    public static string Derive(NormalizedPhoneNumber phone, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(phone.Value));

        return $"{Prefix}{Base32NoPadding.Encode(hash.AsSpan(0, RuntimeKeyBytesLength))}";
    }

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && RuntimeKeyPattern().IsMatch(value.Trim());

    [GeneratedRegex(RuntimeKeyPatternValue, RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeKeyPattern();
}
