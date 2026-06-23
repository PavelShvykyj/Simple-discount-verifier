namespace SimpleDiscountVerifier.Api.Application.Pos;

public static class PosBarcodeValidationReasons
{
    public const string Unknown = "unknown";
    public const string Expired = "expired";
    public const string AlreadyUsed = "already_used";
    public const string InvalidFormat = "invalid_format";
}
