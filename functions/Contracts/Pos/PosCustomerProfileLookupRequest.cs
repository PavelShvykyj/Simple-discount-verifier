namespace SimpleDiscountVerifier.Api.Contracts.Pos;

public sealed record PosCustomerProfileLookupRequest(
    string? Phone,
    string? TerminalId,
    string? BranchId);
