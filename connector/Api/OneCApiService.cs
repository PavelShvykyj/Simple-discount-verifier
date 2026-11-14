using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdv.Connector.Configuration;
using Sdv.Connector.Contracts;

namespace Sdv.Connector.Api;

/// <summary>
/// Talks to the local 1C HTTP API: Basic authorization on every request and a reusable
/// infobase session identified by the <c>ibsession</c> cookie (<c>IBSession: start/finish</c>).
/// A business request is never retried after a timeout or connection failure.
/// </summary>
internal sealed class OneCApiService(
    IHttpClientFactory factory,
    IOptions<ConnectorOptions> options,
    TimeProvider time,
    ILogger<OneCApiService> logger) : IOneCApi
{
    internal const string HttpClientName = "OneC";
    static readonly TimeSpan ControlTimeout = TimeSpan.FromSeconds(5);
    static readonly TimeSpan StageBudget = TimeSpan.FromSeconds(30);
    static readonly TimeSpan ExpiryReserve = TimeSpan.FromSeconds(15);

    readonly SemaphoreSlim gate = new(1, 1);
    readonly object sync = new();
    string? sessionId;
    bool enabled = true;
    ApiState state = ApiState.NoSession;
    DateTimeOffset? lastApi, lastPing;
    string errorCode = ApiErrors.None;

    public bool Enabled { get { lock (sync) return enabled; } }

    public ApiSnapshot Snapshot()
    {
        lock (sync) return new(enabled, enabled ? state : ApiState.Disabled, lastApi, lastPing, errorCode);
    }

    internal static TimeSpan Budget(DateTimeOffset expiry, DateTimeOffset now, TimeSpan spent)
    {
        var stage = StageBudget - spent;
        var remaining = expiry - now - ExpiryReserve;
        return stage < remaining ? stage : remaining;
    }

    public async Task<LookupResult> LookupAsync(CommandRequest command, CancellationToken stop)
    {
        var started = time.GetTimestamp();
        TimeSpan Remaining() => Budget(command.Expiry, time.GetUtcNow(), time.GetElapsedTime(started));
        await gate.WaitAsync(stop);
        var inLookup = false;
        try
        {
            if (!Enabled) return LookupResult.Failed(ReasonCodes.ApiUnavailable);
            if (Remaining() <= TimeSpan.Zero) return LookupResult.Failed(ReasonCodes.Expired);
            var (session, failure) = await EnsureSessionAsync(Remaining, stop);
            if (failure is not null) return failure;
            var budget = Remaining();
            if (budget <= TimeSpan.Zero) return LookupResult.Failed(ReasonCodes.Expired);
            var body = BuildLookupBody(command);
            inLookup = true;
            var reply = await SendAsync(HttpMethod.Post, options.Value.Api.LookupPath, null, session, body, budget, readBody: true, stop);
            return CompleteLookup(reply, command);
        }
        catch (BudgetExhaustedException) { return LookupResult.Failed(ReasonCodes.Expired); }
        catch (Exception e) when (IsApiFailure(e) && !stop.IsCancellationRequested)
        {
            var transport = IsTransportFailure(e);
            SetState(transport ? ApiState.Unknown : ApiState.Faulted,
                transport ? (inLookup ? ApiErrors.ApiTransportFailure : ApiErrors.SessionUnavailable) : ApiErrors.ApiProtocolFailure);
            logger.LogWarning("1C API call failed during {Stage}: {ExceptionType}", inLookup ? "lookup" : "session creation", e.GetType().Name);
            return LookupResult.Failed(inLookup ? ReasonCodes.ApiOutcomeUnknown : ReasonCodes.ApiUnavailable);
        }
        finally { gate.Release(); }
    }

    public async Task<string> StartAsync(CancellationToken stop)
    {
        await gate.WaitAsync(stop);
        try
        {
            if (Enabled && Snapshot().State == ApiState.Active && CurrentSession() is not null) return "AlreadyEnabled";
            var previous = TakeSession();
            if (previous is not null) await FinishAsync(previous, ControlTimeout, stop);
            try
            {
                var failure = await LoginAsync(ControlTimeout, stop);
                if (failure is not null) return failure.Reason;
                SetEnabled(true);
                return "Started";
            }
            catch (Exception e) when (IsApiFailure(e) && !stop.IsCancellationRequested)
            {
                logger.LogWarning("1C session start failed: {ExceptionType}", e.GetType().Name);
                SetState(ApiState.Faulted, ApiErrors.SessionUnavailable);
                return ApiErrors.SessionUnavailable;
            }
        }
        finally { gate.Release(); }
    }

    public async Task<string> PingAsync(CancellationToken stop)
    {
        await gate.WaitAsync(stop);
        try
        {
            var session = CurrentSession();
            if (session is null) return "NoSession";
            try
            {
                var reply = await SendAsync(HttpMethod.Get, options.Value.Api.PingPath, null, session, null, ControlTimeout, readBody: false, stop);
                if (reply.Status is 401 or 403)
                {
                    TakeSession();
                    SetState(ApiState.NoSession, ReasonCodes.AccessDenied);
                    return ReasonCodes.AccessDenied;
                }
                if (!IsSuccess(reply.Status))
                {
                    SetState(ApiState.Faulted, ApiErrors.ApiCheckFailed);
                    return ApiErrors.ApiCheckFailed;
                }
                MarkSuccess(ping: true);
                return "Active";
            }
            catch (Exception e) when (IsApiFailure(e) && !stop.IsCancellationRequested)
            {
                logger.LogWarning("1C ping failed: {ExceptionType}", e.GetType().Name);
                SetState(IsTransportFailure(e) ? ApiState.Unknown : ApiState.Faulted, ApiErrors.ApiCheckFailed);
                return ApiErrors.ApiCheckFailed;
            }
        }
        finally { gate.Release(); }
    }

    public async Task<string> LogoutAsync(CancellationToken stop)
    {
        await gate.WaitAsync(stop);
        try { return await LogoutCoreAsync(stop); }
        finally { gate.Release(); }
    }

    public async Task<string> DisableAsync(CancellationToken stop)
    {
        await gate.WaitAsync(stop);
        try
        {
            SetEnabled(false);
            return await LogoutCoreAsync(stop);
        }
        finally { gate.Release(); }
    }

    async Task<string> LogoutCoreAsync(CancellationToken stop)
    {
        var saved = TakeSession();
        if (saved is null) return "NoSession";
        return await FinishAsync(saved, ControlTimeout, stop) ? "Closed" : "LogoutUnknown";
    }

    async Task<(string? Session, LookupResult? Failure)> EnsureSessionAsync(Func<TimeSpan> remaining, CancellationToken stop)
    {
        var current = CurrentSession();
        if (current is not null)
        {
            var budget = remaining();
            if (budget <= TimeSpan.Zero) throw new BudgetExhaustedException();
            if (await TryPingAsync(current, budget < ControlTimeout ? budget : ControlTimeout, stop)) return (current, null);
            TakeSession();
            await FinishAsync(current, TimeSpan.FromSeconds(2), stop);
        }
        var loginBudget = remaining();
        if (loginBudget <= TimeSpan.Zero) throw new BudgetExhaustedException();
        var failure = await LoginAsync(loginBudget, stop);
        return failure is null ? (CurrentSession(), null) : (null, failure);
    }

    async Task<bool> TryPingAsync(string session, TimeSpan timeout, CancellationToken stop)
    {
        try
        {
            var reply = await SendAsync(HttpMethod.Get, options.Value.Api.PingPath, null, session, null, timeout, readBody: false, stop);
            return IsSuccess(reply.Status);
        }
        catch (Exception e) when (IsTransportFailure(e) && !stop.IsCancellationRequested)
        {
            return false;
        }
    }

    async Task<LookupResult?> LoginAsync(TimeSpan timeout, CancellationToken stop)
    {
        var reply = await SendAsync(HttpMethod.Get, options.Value.Api.PingPath, "start", null, null, timeout, readBody: false, stop);
        if (!IsSuccess(reply.Status))
        {
            var denied = reply.Status is 401 or 403;
            SetState(ApiState.Faulted, denied ? ReasonCodes.AccessDenied : ApiErrors.SessionUnavailable);
            logger.LogWarning("1C session start rejected with HTTP {Status}", reply.Status);
            return LookupResult.Failed(denied ? ReasonCodes.AccessDenied : ReasonCodes.ApiUnavailable, denied ? null : ApiErrors.SessionUnavailable);
        }
        var id = ExtractSessionId(reply.SetCookies);
        if (id is null)
        {
            SetState(ApiState.Faulted, ApiErrors.ApiProtocolFailure);
            logger.LogWarning("1C session start response has no valid ibsession cookie");
            return LookupResult.Failed(ReasonCodes.ApiUnavailable, ApiErrors.ApiProtocolFailure);
        }
        lock (sync) sessionId = id;
        MarkSuccess(ping: false);
        logger.LogInformation("1C session started");
        return null;
    }

    async Task<bool> FinishAsync(string session, TimeSpan timeout, CancellationToken stop)
    {
        try
        {
            var reply = await SendAsync(HttpMethod.Get, options.Value.Api.PingPath, "finish", session, null, timeout, readBody: false, stop);
            return IsSuccess(reply.Status);
        }
        catch (Exception e) when (IsApiFailure(e) && !stop.IsCancellationRequested)
        {
            logger.LogWarning("1C session finish failed: {ExceptionType}", e.GetType().Name);
            return false;
        }
    }

    LookupResult CompleteLookup(ApiReply reply, CommandRequest command)
    {
        // 1C may answer an authorization failure with a non-JSON body.
        if (reply.Status is 401 or 403)
        {
            TakeSession();
            SetState(ApiState.NoSession, ReasonCodes.AccessDenied);
            return LookupResult.Failed(ReasonCodes.AccessDenied);
        }
        // 1C HTTP services send no Content-Type unless the handler sets it; the body is still parsed as strict JSON.
        var mime = reply.ContentType;
        if (mime is not null && (mime.MediaType != "application/json" || (mime.CharSet is not null && !string.Equals(mime.CharSet.Trim('"'), "utf-8", StringComparison.OrdinalIgnoreCase))))
            throw new InvalidDataException();
        var result = OneCResponse.Normalize(reply.Status, StrictJson.Parse(reply.Body, OneCResponse.MaxBodyBytes), command.OrderNumber!);
        if (result.Reason == ReasonCodes.AccessDenied)
        {
            TakeSession();
            SetState(ApiState.NoSession, ReasonCodes.AccessDenied);
        }
        else if (result.Outcome is Outcomes.Found or Outcomes.NotFound) MarkSuccess(ping: false);
        else SetState(ApiState.Faulted, result.Diagnostic ?? result.Reason);
        return result;
    }

    static byte[] BuildLookupBody(CommandRequest command)
    {
        var body = new Dictionary<string, object?>
        {
            ["contractVersion"] = 1,
            ["commandId"] = command.Id,
            ["branchId"] = command.BranchId,
            ["orderNumber"] = command.OrderNumber,
            ["expiresAt"] = command.ExpiresAtText
        };
        if (command.AmountHint is not null) body["amountHint"] = command.AmountHint;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(body);
        if (bytes.Length > OneCResponse.MaxBodyBytes) throw new InvalidDataException();
        return bytes;
    }

    async Task<ApiReply> SendAsync(HttpMethod method, string path, string? ibSession, string? cookie, byte[]? body, TimeSpan timeout, bool readBody, CancellationToken stop)
    {
        if (timeout <= TimeSpan.Zero) throw new BudgetExhaustedException();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(stop);
        cancel.CancelAfter(timeout);
        var api = options.Value.Api;
        using var request = new HttpRequestMessage(method, path.TrimStart('/'));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{api.User}:{api.Password}")));
        if (ibSession is not null) request.Headers.TryAddWithoutValidation("IBSession", ibSession);
        if (cookie is not null) request.Headers.TryAddWithoutValidation("Cookie", $"ibsession={cookie}");
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        }
        using var client = factory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel.Token);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values) ? values.ToArray() : [];
        var data = Array.Empty<byte>();
        if (readBody)
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancel.Token);
            var buffer = new byte[OneCResponse.MaxBodyBytes + 1];
            var count = 0;
            while (count < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(count), cancel.Token);
                if (read == 0) break;
                count += read;
            }
            if (count > OneCResponse.MaxBodyBytes) throw new InvalidDataException();
            data = buffer[..count];
        }
        return new ApiReply((int)response.StatusCode, cookies, response.Content.Headers.ContentType, data);
    }

    internal static string? ExtractSessionId(IEnumerable<string> setCookieValues)
    {
        foreach (var line in setCookieValues)
        {
            var pair = line.Split(';', 2)[0];
            var separator = pair.IndexOf('=');
            if (separator <= 0 || !pair[..separator].Trim().Equals("ibsession", StringComparison.OrdinalIgnoreCase)) continue;
            var value = pair[(separator + 1)..].Trim();
            return Patterns.SessionId().IsMatch(value) ? value : null;
        }
        return null;
    }

    static bool IsSuccess(int status) => status is >= 200 and < 300;

    static bool IsTransportFailure(Exception e) => e is HttpRequestException or OperationCanceledException or IOException;

    static bool IsApiFailure(Exception e) => IsTransportFailure(e) || e is JsonException or InvalidDataException or DecoderFallbackException;

    string? CurrentSession() { lock (sync) return sessionId; }

    string? TakeSession()
    {
        lock (sync)
        {
            var saved = sessionId;
            sessionId = null;
            if (state == ApiState.Active) state = ApiState.NoSession;
            return saved;
        }
    }

    void SetEnabled(bool value) { lock (sync) enabled = value; }

    void SetState(ApiState value, string error) { lock (sync) { state = value; errorCode = error; } }

    void MarkSuccess(bool ping)
    {
        lock (sync)
        {
            var now = time.GetUtcNow();
            lastApi = now;
            if (ping) lastPing = now;
            state = ApiState.Active;
            errorCode = ApiErrors.None;
        }
    }

    sealed record ApiReply(int Status, string[] SetCookies, MediaTypeHeaderValue? ContentType, byte[] Body);

    sealed class BudgetExhaustedException : Exception;
}
