namespace SimpleDiscountVerifier.Api.Contracts.Pos;

public sealed record PosBarcodeValidationFailureResponse(
    bool Valid,
    string Reason,
    string? CorrelationId,
    DateTimeOffset ValidatedAt);
