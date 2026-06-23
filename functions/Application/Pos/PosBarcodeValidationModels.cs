namespace SimpleDiscountVerifier.Api.Application.Pos;

public sealed record PosAuthenticateCommand(
    string Method,
    string Path,
    string? Timestamp,
    string BodySha256Hex,
    string? ClientId,
    string? Signature);

public sealed record PosClientIdentity(string ClientId);

public sealed record ValidatePosBarcodeCommand(
    string? BarcodeValue,
    string? TerminalId,
    string? BranchId,
    string? ScanId,
    PosClientIdentity Client);

public abstract record PosBarcodeValidationResult(
    bool Valid,
    string? CorrelationId,
    DateTimeOffset ValidatedAt);

public sealed record PosBarcodeValidationSuccessResult(
    string LookupKey,
    string CorrelationId,
    DateTimeOffset ValidatedAt,
    bool IdempotentReplay)
    : PosBarcodeValidationResult(true, CorrelationId, ValidatedAt);

public sealed record PosBarcodeValidationFailureResult(
    string Reason,
    string? CorrelationId,
    DateTimeOffset ValidatedAt)
    : PosBarcodeValidationResult(false, CorrelationId, ValidatedAt);
