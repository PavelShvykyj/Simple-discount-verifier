namespace SimpleDiscountVerifier.Api.Application.Common;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    string? ContinuationToken);
