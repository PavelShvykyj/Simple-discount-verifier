namespace SimpleDiscountVerifier.Api.Domain.Shared;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
