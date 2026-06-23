using System.Security.Cryptography;

namespace SimpleDiscountVerifier.Api.Domain.Redemptions;

public static class CorrelationIdGenerator
{
    public const int CorrelationIdLength = 10;

    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[8];
        RandomNumberGenerator.Fill(bytes);
        return Base32NoPadding.Encode(bytes)[..CorrelationIdLength];
    }

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Trim() is { Length: CorrelationIdLength } trimmed
        && Base32NoPadding.IsEncodedValue(trimmed);
}
