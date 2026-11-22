using System.Text.Json;
using Azure;
using Azure.AI.AgentServer.Core;
using Azure.AI.AgentServer.Core.Storage;
using Azure.Core;
using System.ClientModel;

namespace Sdv.Worker;

internal enum Category { Catalog, Control, Receipts, Pending, Success }
internal sealed record Item(byte[] Value, string Etag);

internal interface IState
{
    Task<Item?> Read(Caller caller, Category category, string key, CancellationToken ct);
    Task<string?> Create(Caller caller, Category category, string key, byte[] value, CancellationToken ct);
    Task<string?> Replace(Caller caller, Category category, string key, string etag, byte[] value, CancellationToken ct);
    Task<bool> Delete(Caller caller, Category category, string key, string etag, CancellationToken ct);
}

internal sealed class FoundryState(TokenCredential credential, Uri endpoint, ILogger<FoundryState> logger) : IState, IDisposable
{
    private readonly SemaphoreSlim storeGate = new(1);
    private readonly Dictionary<Category, FoundryStateStore> stores = new();

    private async Task<FoundryStateStore> Open(Category category, CancellationToken ct)
    {
        await storeGate.WaitAsync(ct);
        try
        {
            if (stores.TryGetValue(category, out var cached)) return cached;
            if (!FoundryEnvironment.IsHosted) throw new IOException("Foundry State Store requires Hosted.");
            var (name, ttl) = category switch
            {
                Category.Catalog => ("agent-maintenance-v1", -1),
                Category.Control => ("agent-control-v1", 604800),
                Category.Receipts => ("agent-receipts-v1", 604800),
                Category.Pending => ("agent-pending-v1", 300),
                _ => ("agent-success-v1", (int)LookupTiming.SuccessHistoryLifetime.TotalSeconds)
            };
            var isolated = category != Category.Catalog;
            var store = await FoundryStateStore.GetOrCreateAsync(name, credential, endpoint,
                userIsolation: isolated, itemTtlSeconds: ttl, cancellationToken: ct);
            ct.ThrowIfCancellationRequested();
            var descriptor = await store.GetAsync(ct);
            if (descriptor is null || descriptor.UserIsolation != isolated || descriptor.ItemTtlSeconds != ttl)
                throw new IOException("State store configuration mismatch.");
            stores.Add(category, store);
            return store;
        }
        finally
        {
            storeGate.Release();
        }
    }

    public void Dispose() => storeGate.Dispose();

    public async Task<Item?> Read(Caller caller, Category category, string key, CancellationToken ct)
    {
        var store = await Open(category, ct);
        ct.ThrowIfCancellationRequested();
        var item = await store.GetItemAsync(key, callId: caller.CallId, cancellationToken: ct);
        WorkerLog.StateRead(logger, category, item is not null);
        return item is null ? null : new(item.Value["record"].ToArray(), Etag(item.Etag));
    }

    public async Task<string?> Create(Caller caller, Category category, string key, byte[] value, CancellationToken ct)
    {
        var store = await Open(category, ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var item = await store.CreateItemAsync(key, Value(value), tags: null, callId: caller.CallId, cancellationToken: ct);
            return Etag(item.Etag);
        }
        catch (Exception error) when (Status(error) is 409 or 412)
        {
            WorkerLog.StateConflict(logger, nameof(Create), category, Status(error));
            return null;
        }
    }

    public async Task<string?> Replace(Caller caller, Category category, string key, string etag, byte[] value, CancellationToken ct)
    {
        var store = await Open(category, ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var item = await store.SetItemAsync(key, Value(value), tags: null, ifMatch: Etag(etag),
                requireExists: false, callId: caller.CallId, cancellationToken: ct);
            return Etag(item.Etag);
        }
        catch (Exception error) when (Status(error) is 404 or 409 or 412)
        {
            WorkerLog.StateConflict(logger, nameof(Replace), category, Status(error));
            return null;
        }
    }

    public async Task<bool> Delete(Caller caller, Category category, string key, string etag, CancellationToken ct)
    {
        var store = await Open(category, ct);
        try
        {
            ct.ThrowIfCancellationRequested();
            var item = await store.DeleteItemAsync(key, ifMatch: Etag(etag), callId: caller.CallId, cancellationToken: ct);
            return item.Deleted;
        }
        catch (Exception error) when (Status(error) is 404 or 409 or 412)
        {
            WorkerLog.StateConflict(logger, nameof(Delete), category, Status(error));
            return false;
        }
    }

    private static string Etag(string? value) =>
        string.IsNullOrEmpty(value) || value == "*" ? throw new IOException("Missing concrete ETag.") : value;
    private static Dictionary<string, BinaryData> Value(byte[] bytes) => new() { ["record"] = new(bytes) };
    private static int Status(Exception error) => error switch
    {
        ClientResultException client => client.Status,
        RequestFailedException request => request.Status,
        _ => 0
    };
}

internal enum Phase { IdleEmpty, InputClaimed, WaitingTool, ResultClaimed, IdleSuccess, ClosedFailed }
internal sealed record Control(string Owner, string Session, string Branch, Phase Phase, Admission? Active = null,
    Waiting? Wait = null, string? SnapshotRef = null, DateTimeOffset? LastSuccessfulAt = null,
    DateTimeOffset? HistoryDeleteAt = null, DateTimeOffset? ProtectUntil = null, string? ResultEventId = null);
internal sealed record Admission(string Owner, string Session, string Branch, string RequestId, string EventId,
    string OperationId, string Digest, DateTimeOffset Expiry, DateTimeOffset ProtectUntil);
internal sealed record Waiting(string CommandId, string ToolId, string Route, string Hash, string Number, string ExpiryText);
internal sealed record Catalog(int SchemaVersion, string OwnerKey, string AgentSessionId, string BranchId,
    DateTimeOffset RegisteredAt, string Agent, string Version);

internal sealed record Stored<T>(T Value, string Etag)
{
    internal static Stored<T>? From(Item? item) => item is null ? null :
        new(JsonSerializer.Deserialize<T>(item.Value, Wire.Json) ?? throw new IOException("Invalid state record."), item.Etag);
}
