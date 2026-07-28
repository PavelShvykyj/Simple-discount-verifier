namespace SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

public sealed record PhysicalCardNumber
{
    private const int Length = 13;

    private PhysicalCardNumber(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static bool TryCreate(string? input, out PhysicalCardNumber cardNumber)
    {
        cardNumber = null!;

        if (input is null
            || input.Length != Length
            || input.Any(character => character is < '0' or > '9'))
        {
            return false;
        }

        var checksum = 0;

        for (var index = 0; index < Length - 1; index++)
        {
            checksum += (input[index] - '0') * (index % 2 == 0 ? 1 : 3);
        }

        if ((10 - checksum % 10) % 10 != input[^1] - '0')
        {
            return false;
        }

        cardNumber = new PhysicalCardNumber(input);
        return true;
    }

    public override string ToString() => Value;
}
