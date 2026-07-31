using SimpleDiscountVerifier.Api.Contracts.Admin;

namespace SimpleDiscountVerifier.Api.Contracts.Pos;

public sealed record PosCustomerProfileLookupResponse(
    bool Found,
    CustomerProfileResponse Profile,
    DateTimeOffset ServedAt);
