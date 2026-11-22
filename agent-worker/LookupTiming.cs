namespace Sdv.Worker;

internal static class LookupTiming
{
    internal static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan MaxTurnDuration = TimeSpan.FromSeconds(120);
    internal static readonly TimeSpan SendReserve = TimeSpan.FromSeconds(15);
    internal static readonly TimeSpan SuccessHistoryLifetime = TimeSpan.FromDays(3);
}
