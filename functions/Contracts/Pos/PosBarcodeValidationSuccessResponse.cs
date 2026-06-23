namespace SimpleDiscountVerifier.Api.Contracts.Pos;

public sealed record PosBarcodeValidationSuccessResponse(
    bool Valid,
    string LookupKeyType,
    string LookupKey,
    string CorrelationId,
    DateTimeOffset ValidatedAt,
    bool IdempotentReplay);
