using System.Security.Cryptography;

namespace SimpleDiscountVerifier.Api.Domain.Redemptions;

public static class CorrelationIdGenerator
{
    public static string Create()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return $"c_{Base32NoPadding.Encode(bytes)}";
    }
}
