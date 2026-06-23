using SimpleDiscountVerifier.Api.Contracts.Admin;

namespace SimpleDiscountVerifier.Api.Application.Pos;

public sealed record LookupPosCustomerProfileCommand(
    string? Phone,
    string? TerminalId,
    string? BranchId,
    PosClientIdentity Client);

public sealed record PosCustomerProfileLookupResult(
    CustomerProfileResponse Profile,
    DateTimeOffset ServedAt);
