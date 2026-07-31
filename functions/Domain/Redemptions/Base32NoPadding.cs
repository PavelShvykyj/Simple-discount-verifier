namespace SimpleDiscountVerifier.Api.Domain.Redemptions;

internal static class Base32NoPadding
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static bool IsEncodedValue(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (Alphabet.IndexOf(character) < 0)
            {
                return false;
            }
        }

        return true;
    }

    public static string Encode(ReadOnlySpan<byte> bytes)
    {
        if (bytes.IsEmpty)
        {
            return string.Empty;
        }

        var outputLength = (bytes.Length * 8 + 4) / 5;
        return string.Create(outputLength, bytes.ToArray(), static (chars, data) =>
        {
            var buffer = 0;
            var bitsLeft = 0;
            var index = 0;

            foreach (var value in data)
            {
                buffer = (buffer << 8) | value;
                bitsLeft += 8;

                while (bitsLeft >= 5)
                {
                    chars[index++] = Alphabet[(buffer >> (bitsLeft - 5)) & 31];
                    bitsLeft -= 5;
                }
            }

            if (bitsLeft > 0)
            {
                chars[index] = Alphabet[(buffer << (5 - bitsLeft)) & 31];
            }
        });
    }
}
