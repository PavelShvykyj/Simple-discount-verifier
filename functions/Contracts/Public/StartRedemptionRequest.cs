namespace SimpleDiscountVerifier.Api.Contracts.Public;

public sealed record StartRedemptionRequest(string? Phone, string? TurnstileToken);
