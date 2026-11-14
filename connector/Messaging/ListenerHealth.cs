namespace Sdv.Connector.Messaging;

internal enum ListenerState { Starting, Listening, Stopped, PausedForApi, Reconnecting, Faulted }

internal sealed record ListenerSnapshot(
    ListenerState State,
    bool ActiveLookup,
    DateTimeOffset? LastReceiveAt,
    DateTimeOffset? LastReceiveErrorAt,
    string ReceiveErrorCode,
    DateTimeOffset? LastSendErrorAt,
    string SendErrorCode);

/// <summary>Thread-safe operational state of the command listener, shown by the operator status command.</summary>
internal sealed class ListenerHealth(TimeProvider time)
{
    readonly object sync = new();
    ListenerState state = ListenerState.Starting;
    bool activeLookup;
    DateTimeOffset? lastReceive, lastReceiveError, lastSendError;
    string receiveError = "None", sendError = "None";

    internal bool ActiveLookup { get { lock (sync) return activeLookup; } }

    // Service Bus has no "recovered" callback; while the receiver keeps failing it keeps raising errors, so silence means recovery.
    internal static readonly TimeSpan ReconnectQuietPeriod = TimeSpan.FromMinutes(2);

    internal ListenerSnapshot Snapshot()
    {
        lock (sync)
        {
            if (state == ListenerState.Reconnecting && lastReceiveError is { } at && time.GetUtcNow() - at >= ReconnectQuietPeriod)
            {
                state = ListenerState.Listening;
                receiveError = "None";
            }
            return new(state, activeLookup, lastReceive, lastReceiveError, receiveError, lastSendError, sendError);
        }
    }

    internal void SetState(ListenerState value) { lock (sync) state = value; }

    internal void SetActiveLookup(bool value) { lock (sync) activeLookup = value; }

    internal void MarkReceived()
    {
        lock (sync) { lastReceive = time.GetUtcNow(); receiveError = "None"; if (state == ListenerState.Reconnecting) state = ListenerState.Listening; }
    }

    internal void MarkReceiveError(string code, bool faulted)
    {
        lock (sync)
        {
            lastReceiveError = time.GetUtcNow();
            // A late error must not overwrite a state the operator chose (stopped or paused).
            if (state is ListenerState.Stopped or ListenerState.PausedForApi) return;
            receiveError = code;
            state = faulted ? ListenerState.Faulted : ListenerState.Reconnecting;
        }
    }

    internal void MarkSendError() { lock (sync) { lastSendError = time.GetUtcNow(); sendError = "SendUnknown"; } }
}
