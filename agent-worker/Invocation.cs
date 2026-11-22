using System.ClientModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text.Json;
using Azure;
using Azure.AI.AgentServer.Invocations;

namespace Sdv.Worker;

internal sealed record FoundryHostIdentity(bool IsHosted, string? AgentName, string? AgentVersion, string? SessionId);

internal sealed class Invocation(FoundryHostIdentity host, Lookup lookup, ILogger<Invocation> logger) : InvocationHandler
{
    public override async Task HandleAsync(HttpRequest request, HttpResponse response, InvocationContext context,
        CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var session = host.SessionId;
        var owner = context.PlatformContext.UserIdKey;
        var callId = context.PlatformContext.CallId;
        if (!host.IsHosted ||
            string.IsNullOrEmpty(session) || !Wire.IsId(session) ||
            string.IsNullOrEmpty(owner) || !Wire.IsId(owner) || string.IsNullOrWhiteSpace(callId) ||
            !request.Query.TryGetValue("agent_session_id", out var query) || query.Count != 1 || query[0] != session)
        {
            response.StatusCode = StatusCodes.Status403Forbidden;
            WorkerLog.InvocationRejected(logger, LogLevel.Warning, RejectionReason.AuthorityMismatch, response.StatusCode);
            return;
        }
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var content) ||
            !string.Equals(content.MediaType, MediaTypeNames.Application.Json, StringComparison.OrdinalIgnoreCase) ||
            (content.CharSet is { } encoding &&
                !string.Equals(encoding.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)))
        {
            response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            WorkerLog.InvocationRejected(logger, LogLevel.Information, RejectionReason.UnsupportedContentType, response.StatusCode);
            return;
        }
        if (request.ContentLength > Wire.Limit)
        {
            response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            WorkerLog.InvocationRejected(logger, LogLevel.Information, RejectionReason.EnvelopeTooLarge, response.StatusCode);
            return;
        }
        var bytes = new byte[Wire.Limit + 1];
        var count = await request.Body.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken);
        if (count > Wire.Limit)
        {
            response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            WorkerLog.InvocationRejected(logger, LogLevel.Information, RejectionReason.EnvelopeTooLarge, response.StatusCode);
            return;
        }

        JsonElement body;
        string eventId;
        try
        {
            body = Wire.Parse(bytes[..count]);
            eventId = Wire.ReadEventId(body);
        }
        catch (Exception error) when (Wire.Invalid(error))
        {
            response.StatusCode = StatusCodes.Status400BadRequest;
            WorkerLog.InvocationRejected(logger, LogLevel.Information, RejectionReason.MalformedEnvelope, response.StatusCode);
            return;
        }
        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["MessageEventId"] = eventId
        });
        Disposition disposition;
        try
        {
            disposition = await lookup.Run(new Caller(owner, session, callId), body, cancellationToken);
        }
        catch (Exception error) when (!cancellationToken.IsCancellationRequested)
        {
            response.StatusCode = error switch
            {
                TimeoutException => StatusCodes.Status504GatewayTimeout,
                RequestFailedException { Status: StatusCodes.Status429TooManyRequests or StatusCodes.Status503ServiceUnavailable }
                    or ClientResultException { Status: StatusCodes.Status429TooManyRequests or StatusCodes.Status503ServiceUnavailable }
                    => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status500InternalServerError
            };
            if (error is OperationCanceledException or TimeoutException)
                WorkerLog.InvocationCancelled(logger, response.StatusCode);
            else
                WorkerLog.InvocationFailed(logger, error, response.StatusCode);
            return;
        }
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = MediaTypeNames.Application.Json;
        await response.Body.WriteAsync(Wire.Encode(new { schemaVersion = 1, eventId, disposition = disposition.ToString() }),
            cancellationToken);
        WorkerLog.InvocationCompleted(logger, disposition, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }
}
