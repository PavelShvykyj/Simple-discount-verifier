using SimpleDiscountVerifier.Api.Domain.Shared;

namespace SimpleDiscountVerifier.Api.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
