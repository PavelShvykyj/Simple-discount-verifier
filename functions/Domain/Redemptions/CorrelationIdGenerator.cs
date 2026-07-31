using System.Security.Cryptography;

namespace SimpleDiscountVerifier.Api.Domain.Redemptions;

public static class CorrelationIdGenerator
{
    public const int CorrelationIdLength = 10;
    private const int CorrelationIdBytesLength = (CorrelationIdLength * 5 + 7) / 8;

    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[CorrelationIdBytesLength];
        RandomNumberGenerator.Fill(bytes);
        return Base32NoPadding.Encode(bytes)[..CorrelationIdLength];
    }

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Trim() is { Length: CorrelationIdLength } trimmed
        && Base32NoPadding.IsEncodedValue(trimmed);
}
