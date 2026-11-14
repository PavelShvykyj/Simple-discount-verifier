using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.AI.AgentServer.Core;
using Azure.AI.AgentServer.Invocations;
using Azure.Identity;
using OpenTelemetry.Trace;

if (args is ["--state-self-test"])
{
    await StateReadProbe.SelfTest();
    return;
}

var builder = AgentHost.CreateBuilder(args);
var config = builder.WebApplicationBuilder.Configuration;
var publicKey = Convert.FromBase64String(config["SDV_SMOKE_PUBLIC_KEY"] ?? "");
using var signer = RSA.Create();
signer.ImportSubjectPublicKeyInfo(publicKey, out var consumed);
if (consumed != publicKey.Length || signer.KeySize < 2048)
    throw new InvalidOperationException("Invalid diagnostic verification key.");
builder.Services.AddSingleton(new ProbeKey(publicKey));
builder.Services.AddSingleton(new StateReadProbe(new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned)));
builder.AddInvocations<ProbeHandler>();
builder.ConfigureTracing(tracing => tracing.SetSampler(new AlwaysOffSampler()));
builder.WebApplicationBuilder.Logging.ClearProviders();
builder.Build().Run();

internal sealed record ProbeKey(byte[] Value);

internal sealed record ProbeRequest(
    string Purpose, string ProbeId, string Session, string Version, string DelegatedKey,
    string RunNonce, string? ExpectedFingerprint, long ExpiresAt);

internal sealed class ProbeHandler(ProbeKey key, IConfiguration config, StateReadProbe state) : InvocationHandler
{
    private const int Limit = 2048;
    private const string AgentName = "sdv-worker-s5-smoke";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        AllowDuplicateProperties = false,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public override async Task HandleAsync(HttpRequest request, HttpResponse response, InvocationContext context,
        CancellationToken cancellationToken)
    {
        response.StatusCode = StatusCodes.Status403Forbidden;
        if (config["FOUNDRY_HOSTING_ENVIRONMENT"] != "1" || config["FOUNDRY_AGENT_NAME"] != AgentName ||
            !request.Query.TryGetValue("agent_session_id", out var selector) || selector.Count != 1 ||
            !request.Headers.TryGetValue("x-client-sdv-signature", out var signature) || signature.Count != 1)
            return;
        if (!MediaTypeHeaderValue.TryParse(request.ContentType, out var content) || content.MediaType != "application/json")
        {
            response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
            return;
        }
        if (request.ContentLength > Limit)
        {
            response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        var bytes = new byte[Limit + 1];
        var count = await request.Body.ReadAtLeastAsync(bytes, bytes.Length, false, cancellationToken);
        if (count > Limit)
        {
            response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            return;
        }
        var probe = Parse(bytes.AsSpan(0, count), signature[0]);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var stateRead = config["SDV_SMOKE_STATE_READ"] == "1";
        var purpose = stateRead ? "sdv-s5-state-read" : "sdv-s5-hosted-smoke";
        if (probe is null || probe.Purpose != purpose || !Guid.TryParseExact(probe.ProbeId, "D", out _) ||
            !Guid.TryParseExact(probe.RunNonce, "D", out _) || probe.DelegatedKey is not ("sdv:s5:probe:a" or "sdv:s5:probe:b") ||
            string.IsNullOrEmpty(probe.Session) || string.IsNullOrEmpty(probe.Version) ||
            probe.Session != selector[0] || probe.Session != context.SessionId ||
            probe.Session != config["FOUNDRY_AGENT_SESSION_ID"] || probe.Version != config["FOUNDRY_AGENT_VERSION"] ||
            probe.ExpiresAt <= now || probe.ExpiresAt > now + 120)
            return;

        var user = context.PlatformContext.UserIdKey;
        var call = context.PlatformContext.CallId;
        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(call) ||
            user == "sdv-smoke-forged-user" || call == "sdv-smoke-forged-call")
            return;

        if (stateRead)
        {
            if (probe.Version != "3" || user != probe.DelegatedKey || probe.ExpectedFingerprint is not null ||
                !long.TryParse(config["SDV_SMOKE_STATE_READ_UNTIL"], out var until) || until <= now)
                return;
            try
            {
                var evidence = await state.Read(user, call, cancellationToken);
                response.StatusCode = StatusCodes.Status200OK;
                await response.WriteAsJsonAsync(new { schemaVersion = 1, probe.ProbeId, probe.Version, evidence }, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested && !response.HasStarted)
            {
                response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
            return;
        }

        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(probe.RunNonce + "\n" + user)));
        if (probe.ExpectedFingerprint is not null && probe.ExpectedFingerprint != fingerprint)
            return;

        response.StatusCode = StatusCodes.Status200OK;
        await response.WriteAsJsonAsync(new
        {
            schemaVersion = 1,
            probe.ProbeId,
            version = probe.Version,
            platformUserPresent = true,
            platformCallPresent = true,
            matchesDelegatedKey = user == probe.DelegatedKey,
            fingerprint
        }, cancellationToken);
    }

    private ProbeRequest? Parse(ReadOnlySpan<byte> body, string? signature)
    {
        if (signature is not { Length: > 0 and <= 1024 }) return null;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(key.Value, out _);
            return rsa.VerifyData(body, Convert.FromBase64String(signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
                ? JsonSerializer.Deserialize<ProbeRequest>(body, Json)
                : null;
        }
        catch (Exception error) when (error is FormatException or JsonException or CryptographicException)
        {
            return null;
        }
    }
}
