using System.Security.Cryptography;
using System.Text;
using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

namespace SimpleDiscountVerifier.Api.Domain.Redemptions;

public static class PhoneRuntimeKeyGenerator
{
    public const int RuntimeKeyLength = 10;

    public static string Derive(NormalizedPhoneNumber phone, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(phone.Value));

        return Base32NoPadding.Encode(hash)[..RuntimeKeyLength];
    }

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Trim() is { Length: RuntimeKeyLength } trimmed
        && Base32NoPadding.IsEncodedValue(trimmed);
}
