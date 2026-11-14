using System.Net.Http.Headers;
using System.Text.Json;
using Azure.AI.AgentServer.Invocations;

namespace Sdv.Worker;

internal interface IAuthority
{
    Caller? Resolve(HttpRequest request, InvocationContext invocation);
}

internal sealed class HostedAuthority(IConfiguration config) : IAuthority
{
    public Caller? Resolve(HttpRequest request, InvocationContext invocation)
    {
        var session = config["FOUNDRY_AGENT_SESSION_ID"];
        var agent = config["WORKER_AGENT"];
        var version = config["WORKER_VERSION"];
        if (config["WORKER_PROFILE"] != "develop-synthetic" || config["FOUNDRY_HOSTING_ENVIRONMENT"] != "1" ||
            string.IsNullOrEmpty(session) || invocation.SessionId != session ||
            string.IsNullOrEmpty(agent) || config["FOUNDRY_AGENT_NAME"] != agent ||
            string.IsNullOrEmpty(version) || config["FOUNDRY_AGENT_VERSION"] != version ||
            invocation.PlatformContext.UserIdKey is not { Length: > 0 } owner ||
            invocation.PlatformContext.CallId is not { Length: > 0 } callId)
            return null;

        // shortcut: only signed prepare is admitted until the result producer can attest provenance.
        return new(owner, session, callId, Ingress.Prepare);
    }
}

internal sealed class Invocation(IAuthority authority, IServiceProvider services) : InvocationHandler
{
    public override async Task HandleAsync(HttpRequest request, HttpResponse response, InvocationContext context,
        CancellationToken cancellationToken)
    {
        var caller = authority.Resolve(request, context);
        if (caller is null || !Wire.IsId(caller.Owner) || !Wire.IsId(caller.Session) || string.IsNullOrWhiteSpace(caller.CallId) ||
            !request.Query.TryGetValue("agent_session_id", out var query) || query.Count != 1 ||
            query[0] != caller.Session || context.SessionId != caller.Session)
        {
            response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var content) ||
            content.MediaType != "application/json" || (content.CharSet is { } encoding &&
                !string.Equals(encoding.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase)))
        {
            response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }
        if (request.ContentLength > Wire.Limit)
        {
            response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        var bytes = new byte[Wire.Limit + 1];
        var count = await request.Body.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken);
        if (count > Wire.Limit)
        {
            response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }

        JsonElement body;
        string eventId;
        try
        {
            body = Wire.Parse(bytes[..count]);
            Wire.Require(body.ValueKind == JsonValueKind.Object &&
                body.EnumerateObject().Count(p => p.NameEquals("eventId")) == 1);
            eventId = Wire.Uuid(body, "eventId");
        }
        catch (Exception error) when (Wire.Invalid(error))
        {
            response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }
        Disposition disposition;
        try
        {
            disposition = await services.GetRequiredService<Lookup>().Run(caller, body, cancellationToken);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = "application/json";
        await response.Body.WriteAsync(Wire.Encode(new { schemaVersion = 1, eventId, disposition = disposition.ToString() }),
            cancellationToken);
    }
}
