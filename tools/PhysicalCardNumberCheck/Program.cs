using SimpleDiscountVerifier.Api.Domain.CustomerProfiles;

var cases = new (string? Value, bool Expected)[]
{
    ("4820001234565", true),
    ("4006381333931", true),
    (null, false),
    ("482000123456", false),
    ("482000123456X", false),
    ("4820001234564", false)
};

foreach (var (value, expected) in cases)
{
    if (PhysicalCardNumber.TryCreate(value, out _) != expected)
    {
        throw new InvalidOperationException($"Unexpected EAN-13 validation result for '{value ?? "<missing>"}'.");
    }
}

Console.WriteLine("PhysicalCardNumber EAN-13 check passed.");
