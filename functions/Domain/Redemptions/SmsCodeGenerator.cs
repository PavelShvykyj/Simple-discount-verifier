using System.Security.Cryptography;

namespace SimpleDiscountVerifier.Api.Domain.Redemptions;

public static class SmsCodeGenerator
{
    public static string Create()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    public static bool IsValidFormat(string? code)
    {
        return code is { Length: 6 } && code.All(char.IsDigit);
    }
}
