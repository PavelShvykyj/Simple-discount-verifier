using SimpleDiscountVerifier.Api.Contracts.Admin;

namespace SimpleDiscountVerifier.Api.Application.CustomerProfiles;

public sealed record AdminCustomerProfileListQuery(
    string? Phone,
    int? PageSize,
    string? ContinuationToken);

public sealed record AdminCustomerProfileListResult(
    IReadOnlyList<CustomerProfileResponse> Items,
    string? ContinuationToken);
