using System.Security.Cryptography;
using System.Text;

namespace SimpleDiscountVerifier.Api.Domain.Security;

public static class SecretHasher
{
    public static string HashPhone(string normalizedPhone, string secret) => HmacSha256(normalizedPhone, secret);

    public static string HashSmsCode(string code, string secret) => HmacSha256(code, secret);

    public static string HashBarcode(string barcodeValue, string secret) => HmacSha256(barcodeValue, secret);

    public static bool FixedTimeEquals(string expectedHash, string value, string secret)
    {
        var actualHash = HmacSha256(value, secret);
        if (expectedHash.Length != actualHash.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash),
            Encoding.UTF8.GetBytes(actualHash));
    }

    public static string Sha256Hex(ReadOnlySpan<byte> bytes)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(bytes, hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string HmacSha256(string value, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return $"hmac-sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }
}
