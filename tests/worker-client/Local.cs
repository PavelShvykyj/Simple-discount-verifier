using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure.AI.AgentServer.Core;
using Azure.AI.AgentServer.Invocations;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Sdv.Worker;

namespace WorkerChecks;

internal sealed record Scope(string Owner, string Session);

internal sealed class Clock : TimeProvider
{
    private long ticks = new DateTimeOffset(2026, 11, 9, 12, 0, 0, TimeSpan.Zero).Ticks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    internal void Advance(double seconds) => Interlocked.Add(ref ticks, TimeSpan.FromSeconds(seconds).Ticks);
}

internal sealed class TestAuthority : IAuthority
{
    private readonly ConcurrentDictionary<string, (Scope Scope, Ingress Source)> tokens = new();

    internal string Issue(Scope scope, Ingress source)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        tokens[token] = (scope, source);
        return token;
    }

    public Caller? Resolve(HttpRequest request, InvocationContext invocation) =>
        AuthenticationHeaderValue.TryParse(request.Headers.Authorization, out var auth) &&
        auth.Scheme == "Bearer" && auth.Parameter is { } token && tokens.TryGetValue(token, out var value)
            ? new(value.Scope.Owner, value.Scope.Session, Fixtures.Id(), value.Source) : null;
}

internal sealed class LocalState(Clock clock) : IState
{
    private readonly Dictionary<(string Owner, Category Category, string Key), (Item Item, DateTimeOffset? Expiry)> items = new();
    private readonly object gate = new();
    private int version;
    internal ConcurrentQueue<string> Writes { get; } = new();
    internal Action<string, bool>? Fault;
    internal Action<Caller, Category>? BeforeCreate;
    internal Func<Caller, Category, string, CancellationToken, Task>? AfterRead;
    internal string? LoseAsConflict;
    internal string? ConflictBeforeWrite;

    private static (string, Category, string) Scope(Caller caller, Category category, string key) =>
        (category == Category.Catalog ? "" : caller.Owner, category, key);

    private Item? Get((string, Category, string) key)
    {
        if (!items.TryGetValue(key, out var entry)) return null;
        if (entry.Expiry <= clock.GetUtcNow())
        {
            items.Remove(key);
            return null;
        }
        return new(entry.Item.Value.ToArray(), entry.Item.Etag);
    }

    private string Write((string, Category, string) key, byte[] value, string stage)
    {
        var ttl = key.Item2 switch
        {
            Category.Pending => 300, Category.Success => 259200, Category.Catalog => -1, _ => 604800
        };
        var etag = (++version).ToString();
        items[key] = (new(value.ToArray(), etag), ttl == -1 ? null : clock.GetUtcNow().AddSeconds(ttl));
        Writes.Enqueue(stage);
        return etag;
    }

    public async Task<Item?> Read(Caller caller, Category category, string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Item? result;
        lock (gate) result = Get(Scope(caller, category, key));
        if (AfterRead is { } afterRead) await afterRead(caller, category, key, ct);
        return result;
    }

    internal void Corrupt(Caller caller, Category category, string key, byte[] value)
    {
        lock (gate) Write(Scope(caller, category, key), value, "Corrupt");
    }

    public Task<string?> Create(Caller caller, Category category, string key, byte[] value, CancellationToken ct)
    {
        BeforeCreate?.Invoke(caller, category);
        return Mutate(caller, category, key, null, value, ct);
    }
    public Task<string?> Replace(Caller caller, Category category, string key, string etag, byte[] value, CancellationToken ct) =>
        Mutate(caller, category, key, etag, value, ct);

    private Task<string?> Mutate(Caller caller, Category category, string key, string? etag, byte[] value, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var stage = category == Category.Receipts
            ? key.EndsWith("/admissions", StringComparison.Ordinal) ? "Index" : "Receipt"
            : category == Category.Control
                ? JsonSerializer.Deserialize<Control>(value, Wire.Json)!.Phase.ToString() : category.ToString();
        lock (gate)
        {
            var scoped = Scope(caller, category, key);
            var old = Get(scoped);
            if (old?.Etag != etag) return Task.FromResult<string?>(null);
            if (ConflictBeforeWrite == stage)
            {
                ConflictBeforeWrite = null;
                return Task.FromResult<string?>(null);
            }
            Fault?.Invoke(stage, false);
            var written = Write(scoped, value, stage);
            Fault?.Invoke(stage, true);
            if (LoseAsConflict == stage)
            {
                LoseAsConflict = null;
                return Task.FromResult<string?>(null);
            }
            return Task.FromResult<string?>(written);
        }
    }

    public Task<bool> Delete(Caller caller, Category category, string key, string etag, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (gate)
        {
            var scoped = Scope(caller, category, key);
            return Task.FromResult(Get(scoped)?.Etag == etag && items.Remove(scoped));
        }
    }
}

internal sealed record Sent(string Branch, Command Command, DateTimeOffset Deadline);
internal sealed record Notice(string Group, JsonObject Body);

internal sealed class LocalSubscriber(Clock clock)
{
    private readonly ConcurrentDictionary<(string Request, string Operation), DateTimeOffset?> expected = new();
    internal readonly ConcurrentQueue<JsonObject> Received = new();

    internal void Expect(JsonObject start)
    {
        var key = (start["requestId"]!.ToString(), start["operationId"]!.ToString());
        expected.TryAdd(key, DateTimeOffset.Parse(start["expiresAt"]!.ToString()));
    }

    internal bool Receive(JsonObject body)
    {
        static string? Field(JsonObject value, string name) =>
            value[name] is JsonValue field && field.TryGetValue<string>(out var text) ? text : null;
        var request = Field(body, "requestId");
        var operation = Field(body, "operationId");
        if (body.Count != 6 || body["schemaVersion"]?.ToJsonString() != "1" || Field(body, "kind") != "turn.final" ||
            request is not { Length: 36 } || !Guid.TryParseExact(request, "D", out _) ||
            operation is not { Length: 36 } || !Guid.TryParseExact(operation, "D", out _)) return false;
        var valid = Field(body, "status") switch
        {
            "Answered" => Field(body, "answer") is { Length: > 0 },
            "Failed" => Field(body, "errorCode") is { Length: > 0 },
            _ => false
        };
        var key = (request, operation);
        if (!valid || !expected.TryGetValue(key, out var expiry) || expiry <= clock.GetUtcNow() ||
            expiry is null || !expected.TryUpdate(key, null, expiry)) return false;
        Received.Enqueue(body);
        return true;
    }
}

internal sealed class LocalTransport(Clock clock) : ITransport
{
    internal readonly ConcurrentQueue<Sent> Commands = new();
    internal readonly ConcurrentQueue<Notice> Attempts = new();
    private readonly ConcurrentDictionary<string, LocalSubscriber> subscribers = new();
    internal Func<Sent, Task>? OnSend;
    internal bool LosePublication;

    public async Task<bool> Send(string branch, Command command, DateTimeOffset expiry, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var sent = new Sent(branch, command, expiry);
        Commands.Enqueue(sent);
        if (OnSend is not null) await OnSend(sent);
        return true;
    }

    public Task Publish(Caller caller, byte[] body, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var group = AzureTransport.Group(caller);
        var notice = new Notice(group, JsonNode.Parse(body)!.AsObject());
        Attempts.Enqueue(notice);
        if (LosePublication) throw new IOException("Synthetic lost delivery.");
        if (subscribers.TryGetValue(group, out var subscriber)) subscriber.Receive(notice.Body);
        return Task.CompletedTask;
    }

    internal LocalSubscriber Subscribe(Scope scope) => subscribers.GetOrAdd(Group(scope), _ => new(clock));
    internal void Expect(Scope scope, JsonObject body)
    {
        if (subscribers.TryGetValue(Group(scope), out var subscriber)) subscriber.Expect(body);
    }

    private static string Group(Scope scope) => "sdv-worker-" + Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new[] { scope.Owner, scope.Session }))).ToLowerInvariant();
}

internal sealed class Harness : IAsyncDisposable
{
    private readonly RSA fixtureKey = RSA.Create(2048);
    private readonly RSA fixtureVerify = RSA.Create();
    private readonly RSA backendSigner = RSA.Create(2048);
    internal readonly RSA BackendKey = RSA.Create();
    private readonly TestAuthority authority = new();
    private readonly ConcurrentDictionary<Scope, byte> prepared = new();
    private int requests;
    private WebApplication? server;
    private HttpClient? http;
    internal readonly Clock Clock = new();
    internal LocalTransport Transport { get; }
    internal int Requests => Volatile.Read(ref requests);
    internal IConfiguration Configuration => server!.Configuration;
    internal readonly Scope Alice = new("sdv:test:alice", "sandbox-a");
    internal readonly Scope Bob = new("sdv:test:bob", "sandbox-b");
    internal LocalState Store { get; }
    internal Fixtures Fixtures { get; }

    internal Harness()
    {
        fixtureVerify.ImportSubjectPublicKeyInfo(fixtureKey.ExportSubjectPublicKeyInfo(), out _);
        BackendKey.ImportSubjectPublicKeyInfo(backendSigner.ExportSubjectPublicKeyInfo(), out _);
        Store = new(Clock);
        Transport = new(Clock);
        Fixtures = new(fixtureKey, Clock);
    }

    internal async Task Start(IReadOnlyDictionary<string, string?>? hosted = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        builder.Logging.ClearProviders();
        builder.Services.AddAgentServerCore();
        builder.Services.AddInvocationsServer();
        builder.Services.AddScoped<InvocationHandler, Invocation>();
        if (hosted is null)
            builder.Services.AddSingleton<IAuthority>(authority);
        else
        {
            builder.Configuration.AddInMemoryCollection(hosted);
            builder.Services.AddSingleton<IAuthority, HostedAuthority>();
        }
        builder.Services.AddSingleton<TimeProvider>(Clock);
        builder.Services.AddSingleton<IState>(Store);
        builder.Services.AddSingleton<ITransport>(Transport);
        builder.Services.AddScoped<Snapshot>();
        builder.Services.AddScoped<Lookup>();
        builder.Services.AddSingleton(new Wire(new Dictionary<string, RSA> { ["fixture"] = fixtureVerify }, "backend", backendSigner));
        builder.Services.AddSingleton(new Settings("fixture-agent", "fixture-v1",
            new Dictionary<string, string> { ["101"] = "fixture-101", ["102"] = "fixture-102" },
            new Dictionary<string, string>
            {
                ["lookup-fixture-00042"] = "00042", ["lookup-fixture-00044"] = "00044", ["lookup-fixture-99999"] = "99999"
            }));
        server = builder.Build();
        server.UseAgentServerCore();
        server.MapInvocationsServer();
        await server.StartAsync();
        var url = server.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        http = new HttpClient { BaseAddress = new(url), Timeout = TimeSpan.FromSeconds(15) };
    }

    internal async Task<string> Invoke(Scope scope, Ingress source, JsonObject body, string? selector = null,
        bool authorized = true, byte[]? raw = null, bool noSelector = false, string? callId = null, bool platformOwner = true)
    {
        var query = noSelector ? "" : "?agent_session_id=" + Uri.EscapeDataString(selector ?? scope.Session);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/invocations" + query);
        if (platformOwner) request.Headers.Add("x-agent-user-id", scope.Owner);
        if (callId != "") request.Headers.Add("x-agent-foundry-call-id", callId ?? Fixtures.Id());
        request.Headers.Add("x-client-user", scope.Owner);
        if (authorized) request.Headers.Authorization = new("Bearer", authority.Issue(scope, source));
        request.Content = new ByteArrayContent(raw ?? Fixtures.Bytes(body));
        request.Content.Headers.ContentType = new("application/json");
        Interlocked.Increment(ref requests);
        using var reply = await http!.SendAsync(request);
        if (reply.StatusCode != HttpStatusCode.OK) return ((int)reply.StatusCode).ToString();
        Fixtures.Need(reply.Content.Headers.ContentType?.MediaType == "application/json", "Exact ACK MIME");
        using var json = JsonDocument.Parse(await reply.Content.ReadAsByteArrayAsync());
        var ack = json.RootElement;
        Fixtures.Need(ack.EnumerateObject().Count() == 3 && ack.GetProperty("schemaVersion").GetRawText() == "1" &&
            ack.GetProperty("eventId").GetString() == body["eventId"]!.ToString() &&
            ack.GetProperty("disposition").GetString() is "Processed" or "Duplicate" or "Discarded", "Exact correlated ACK");
        return ack.GetProperty("disposition").GetString()!;
    }

    internal async Task<string> Prepare(Scope scope, string branch = "101")
    {
        var ack = await Invoke(scope, Ingress.Prepare, Fixtures.Input(scope, Ingress.Prepare, branch: branch));
        if (ack == "Processed") prepared.TryAdd(scope, 0);
        return ack;
    }

    internal Task<string> StartLookup(Scope scope, JsonObject body)
    {
        if (!prepared.ContainsKey(scope)) return Task.FromResult("NotPrepared");
        Transport.Expect(scope, body);
        return Invoke(scope, Ingress.Start, body);
    }

    internal string ResignBackend(string route)
    {
        var input = route[..route.LastIndexOf('.')];
        return input + "." + Fixtures.B64(backendSigner.SignData(Encoding.ASCII.GetBytes(input),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    internal JsonObject SignedResult(Scope scope, JsonObject start)
    {
        var wire = new Wire(new Dictionary<string, RSA> { ["fixture"] = fixtureVerify }, "backend", backendSigner);
        var caller = Caller(scope);
        var now = Clock.GetUtcNow();
        var input = wire.Read(JsonSerializer.SerializeToElement(start), caller, now);
        var command = wire.CreateCommand(caller, input, "00042", now);
        return Fixtures.Result(JsonNode.Parse(command.Body)!.AsObject(), WorkerChecks.Fixtures.Missing);
    }

    internal static Caller Caller(Scope scope) => new(scope.Owner, scope.Session, Fixtures.Id(), Ingress.Start);
    internal Task<Item?> Control(Scope scope) => Store.Read(Caller(scope), Category.Control, scope.Session, default);
    internal async Task<Control> State(Scope scope) => Stored<Control>.From(await Control(scope))!.Value;

    public async ValueTask DisposeAsync()
    {
        http?.Dispose();
        if (server is not null) await server.DisposeAsync();
        fixtureKey.Dispose();
        fixtureVerify.Dispose();
        backendSigner.Dispose();
        BackendKey.Dispose();
    }
}
