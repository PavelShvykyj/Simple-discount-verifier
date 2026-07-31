namespace SimpleDiscountVerifier.Api.Domain.Security;

public sealed record PosHmacValidationResult(
    bool IsValid,
    PosHmacValidationFailure? FailureReason,
    string? CanonicalString)
{
    public static PosHmacValidationResult Success(string canonicalString) => new(true, null, canonicalString);

    public static PosHmacValidationResult Invalid(PosHmacValidationFailure failure) => new(false, failure, null);
}

public enum PosHmacValidationFailure
{
    UnknownClient,
    InvalidTimestamp,
    StaleTimestamp,
    InvalidSignature
}
