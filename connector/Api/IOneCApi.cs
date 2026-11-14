using System.Text.Json;
using Sdv.Connector.Contracts;

namespace Sdv.Connector.Api;

internal enum ApiState { NoSession, Active, Unknown, Faulted, Disabled }

internal static class ApiErrors
{
    internal const string None = "None";
    internal const string SessionUnavailable = "SessionUnavailable";
    internal const string ApiProtocolFailure = "ApiProtocolFailure";
    internal const string ApiTransportFailure = "ApiTransportFailure";
    internal const string ApiCheckFailed = "ApiCheckFailed";
}

internal sealed record ApiSnapshot(bool Enabled, ApiState State, DateTimeOffset? LastSuccessfulApiAt, DateTimeOffset? LastSuccessfulPingAt, string ErrorCode);

/// <summary>Business and management access to the local 1C HTTP API.</summary>
internal interface IOneCApi
{
    bool Enabled { get; }
    ApiSnapshot Snapshot();
    Task<LookupResult> LookupAsync(CommandRequest command, CancellationToken stop);
    Task<string> StartAsync(CancellationToken stop);
    Task<string> PingAsync(CancellationToken stop);
    Task<string> LogoutAsync(CancellationToken stop);
    Task<string> DisableAsync(CancellationToken stop);
}
