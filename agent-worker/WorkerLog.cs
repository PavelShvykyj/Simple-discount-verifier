namespace Sdv.Worker;

internal enum RejectionReason
{
    AuthorityMismatch, UnsupportedContentType, EnvelopeTooLarge, MalformedEnvelope,
    InvalidSignedEnvelope, BranchNotConfigured, Expired, ControlMismatch, InvalidPhase,
    AdmissionConflict, SnapshotConflict, WaitingConflict, ResultMismatch, InvalidOutcome, TerminalConflict
}

internal enum EffectKind { SendCommand, PublishFinal }

internal static partial class WorkerLog
{
    [LoggerMessage(1000, LogLevel.Information, "Worker started")]
    internal static partial void Started(ILogger logger);

    [LoggerMessage(1001, LogLevel.Information, "Worker stopped")]
    internal static partial void Stopped(ILogger logger);

    [LoggerMessage(1002, LogLevel.Error, "Invalid Worker configuration: {Failure}")]
    internal static partial void InvalidConfiguration(ILogger logger, string failure);

    [LoggerMessage(1003, LogLevel.Critical, "Worker terminated unexpectedly")]
    internal static partial void Terminated(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 1100, Message = "Invocation rejected: {Reason}; HTTP {Status}")]
    internal static partial void InvocationRejected(ILogger logger, LogLevel level, RejectionReason reason, int status);

    [LoggerMessage(1101, LogLevel.Information, "Invocation acknowledged: {Disposition}; duration {DurationMs} ms")]
    internal static partial void InvocationCompleted(ILogger logger, Disposition disposition, double durationMs);

    [LoggerMessage(1102, LogLevel.Error, "Invocation failed; HTTP {Status}")]
    internal static partial void InvocationFailed(ILogger logger, Exception exception, int status);

    [LoggerMessage(1103, LogLevel.Information, "Invocation operation cancelled or timed out; HTTP {Status}")]
    internal static partial void InvocationCancelled(ILogger logger, int status);

    [LoggerMessage(1200, LogLevel.Information, "Event discarded: {Reason}")]
    internal static partial void EventRejected(ILogger logger, RejectionReason reason);

    [LoggerMessage(1201, LogLevel.Information, "Session entered {Phase}")]
    internal static partial void PhaseChanged(ILogger logger, Phase phase);

    [LoggerMessage(1202, LogLevel.Information, "Tool result claimed: {Outcome}, {ReasonCode}")]
    internal static partial void ToolResultClaimed(ILogger logger, string outcome, string reasonCode);

    [LoggerMessage(1203, LogLevel.Information, "Turn finalized: {Phase}; error code {ErrorCode}")]
    internal static partial void TurnFinalized(ILogger logger, Phase phase, string? errorCode);

    [LoggerMessage(1204, LogLevel.Warning, "Effect {Effect} has no confirmed completion; no retry")]
    internal static partial void EffectUnconfirmed(ILogger logger, EffectKind effect, Exception exception);

    [LoggerMessage(1205, LogLevel.Information, "Effect {Effect} cancelled; completion is not confirmed; no retry")]
    internal static partial void EffectCancelled(ILogger logger, EffectKind effect);

    [LoggerMessage(1206, LogLevel.Information, "Effect {Effect} skipped because its deadline elapsed or cancellation was requested")]
    internal static partial void EffectSkipped(ILogger logger, EffectKind effect);

    [LoggerMessage(1207, LogLevel.Warning, "Session history is unavailable")]
    internal static partial void HistoryUnavailable(ILogger logger, Exception exception);

    [LoggerMessage(1208, LogLevel.Information, "Competing turn rejected because the session is busy")]
    internal static partial void SessionBusy(ILogger logger);

    [LoggerMessage(1209, LogLevel.Debug, "Concurrent state change prevented transition to {Phase}")]
    internal static partial void PhaseConflict(ILogger logger, Phase phase);

    [LoggerMessage(1210, LogLevel.Information, "Signed event verified")]
    internal static partial void EventVerified(ILogger logger);

    [LoggerMessage(1211, LogLevel.Error, "Azure rejected effect {Effect}; reason {Reason}; no retry")]
    internal static partial void EffectRejected(ILogger logger, EffectKind effect, string reason, Exception exception);

    [LoggerMessage(1300, LogLevel.Information, "Service Bus accepted command {CommandId}")]
    internal static partial void CommandAccepted(ILogger logger, string commandId);

    [LoggerMessage(1301, LogLevel.Information, "Command {CommandId} was not sent: insufficient time or cancellation requested")]
    internal static partial void CommandSkipped(ILogger logger, string commandId);

    [LoggerMessage(1302, LogLevel.Information, "Web PubSub accepted the final notification")]
    internal static partial void NotificationAccepted(ILogger logger);

    [LoggerMessage(1400, LogLevel.Debug, "State {Operation} conflicted for {Category}; status {Status}")]
    internal static partial void StateConflict(ILogger logger, string operation, Category category, int status);

    [LoggerMessage(1401, LogLevel.Debug, "State read for {Category}: found {Found}")]
    internal static partial void StateRead(ILogger logger, Category category, bool found);
}
