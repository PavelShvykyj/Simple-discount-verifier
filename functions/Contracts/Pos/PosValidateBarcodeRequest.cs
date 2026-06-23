namespace SimpleDiscountVerifier.Api.Contracts.Pos;

public sealed record PosValidateBarcodeRequest(
    string? BarcodeValue,
    string? TerminalId,
    string? BranchId,
    string? ScanId);
