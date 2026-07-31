namespace SimpleDiscountVerifier.Api.Application.Pos;

public static class PosAuditEventTypes
{
    public const string BarcodeValidationRequested = "barcode_validation_requested";
    public const string BarcodeValidationFailed = "barcode_validation_failed";
    public const string BarcodeValidationSucceeded = "barcode_validation_succeeded";
    public const string BarcodeValidationIdempotentReplay = "barcode_validation_idempotent_replay";
    public const string BarcodeConsumed = "barcode_consumed";
    public const string BarcodeExpired = "barcode_expired";
}
