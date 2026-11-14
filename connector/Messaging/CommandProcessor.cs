using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdv.Connector.Api;
using Sdv.Connector.Configuration;
using Sdv.Connector.Contracts;

namespace Sdv.Connector.Messaging;

/// <summary>Validates a command, runs the 1C lookup and publishes the result while the command is still valid.</summary>
internal sealed class CommandProcessor(
    IOptions<ConnectorOptions> options,
    IOneCApi api,
    IResultPublisher publisher,
    ListenerHealth health,
    TimeProvider time,
    ILogger<CommandProcessor> logger)
{
    static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(5);
    static readonly TimeSpan MaxTimeToLive = TimeSpan.FromSeconds(120);

    internal async Task ProcessAsync(byte[] body, string? contentType, CancellationToken stop)
    {
        health.SetActiveLookup(true);
        try
        {
            CommandRequest command;
            try { command = CommandParser.Parse(body, contentType, options.Value.BranchId, time.GetUtcNow()); }
            catch (Exception e) when (e is InvalidDataException or System.Text.Json.JsonException or FormatException or System.Text.DecoderFallbackException)
            {
                logger.LogWarning("Command discarded: invalid envelope ({ExceptionType})", e.GetType().Name);
                return;
            }

            LookupResult result;
            if (command.Rejection is not null) result = LookupResult.Failed(command.Rejection);
            else
            {
                try { result = await api.LookupAsync(command, stop); }
                // The command is already deleted from the queue and 1C may have executed it, so the caller must hear about it.
                catch (OperationCanceledException) when (stop.IsCancellationRequested)
                {
                    result = LookupResult.Failed(ReasonCodes.ApiOutcomeUnknown);
                }
            }

            var now = time.GetUtcNow();
            if (now >= command.Expiry)
            {
                logger.LogWarning("Command {CommandId} expired before the result was ready", command.Id);
                return;
            }
            var eventId = Guid.NewGuid().ToString("D");
            var envelope = ResultEnvelope.Create(command, result, now, eventId);
            var ttl = command.Expiry - time.GetUtcNow();
            if (ttl <= TimeSpan.Zero) return;

            // Not linked to the stop token: the result of a received command is still published during shutdown, bounded by this timeout.
            using var sendCancel = new CancellationTokenSource();
            sendCancel.CancelAfter(ttl < SendTimeout ? ttl : SendTimeout);
            try
            {
                await publisher.PublishAsync(envelope, eventId, command.Id, ttl < MaxTimeToLive ? ttl : MaxTimeToLive, sendCancel.Token);
                logger.LogInformation("Result sent for command {CommandId}: {Outcome}/{Reason}", command.Id, result.Outcome, result.Reason);
            }
            catch (Exception e)
            {
                health.MarkSendError();
                logger.LogWarning("Result for command {CommandId} was not confirmed as sent: {ExceptionType}", command.Id, e.GetType().Name);
            }
        }
        finally { health.SetActiveLookup(false); }
    }
}
