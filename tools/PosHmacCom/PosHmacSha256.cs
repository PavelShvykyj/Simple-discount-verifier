using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace SimpleDiscountVerifier.PosHmacCom;

[ComVisible(true)]
[Guid("3BDDC362-935F-497D-A573-86782CAFE43B")]
[ProgId("SimpleDiscountVerifier.PosHmacSha256")]
[ClassInterface(ClassInterfaceType.None)]
[ComDefaultInterface(typeof(IPosHmacSha256))]
public sealed class PosHmacSha256 : IPosHmacSha256
{
    public string ComputeBase64(string canonicalString, string secret)
    {
        if (canonicalString is null)
        {
            throw new ArgumentNullException(nameof(canonicalString));
        }

        if (string.IsNullOrEmpty(secret))
        {
            throw new ArgumentException(
                "HMAC secret must not be empty.",
                nameof(secret));
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(canonicalString));

        return Convert.ToBase64String(hash);
    }
}
