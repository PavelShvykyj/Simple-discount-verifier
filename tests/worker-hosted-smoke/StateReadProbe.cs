using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Azure.AI.AgentServer.Core;
using Azure.Core;

internal sealed record StateReadEvidence(bool OwnVisible, bool ForeignVisible, bool OwnIdleEmpty, bool OwnHasEtag);

internal sealed class StateReadProbe(TokenCredential credential)
{
    private const string Catalog = "state_stores/agent-maintenance-v1";
    private const string Control = "state_stores/agent-control-v1";
    private const string Agent = "sdv-worker-s5-smoke";
    private const string UserA = "sdv:s5:probe:a";
    private const string UserB = "sdv:s5:probe:b";
    private const string Project = "https://aif-sdv-agent-develop-1978.services.ai.azure.com/api/projects/sdv-agent-develop";
    private static readonly HttpClient Http = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(12),
        MaxResponseContentBufferSize = 64 * 1024
    };

    internal async Task<StateReadEvidence> Read(string user, string callId, CancellationToken ct)
    {
        if (Environment.GetEnvironmentVariable("FOUNDRY_PROJECT_ENDPOINT")?.TrimEnd('/') != Project)
            throw new InvalidOperationException("Unexpected diagnostic project.");
        var token = await credential.GetTokenAsync(new TokenRequestContext(["https://ai.azure.com/.default"]), ct);
        return await Inspect(user, (path, query) => Get(path, query, callId, token.Token, ct));
    }

    private static async Task<JsonElement?> Get(string path, string? query, string callId, string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Project}/storage/{path}?api-version=v1{query}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add(PlatformHeaders.FoundryCallId, callId);
        using var response = await Http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
        return document.RootElement.Clone();
    }

    private static async Task<StateReadEvidence> Inspect(string user,
        Func<string, string?, Task<JsonElement?>> get)
    {
        var descriptor = await get(Control, null);
        if (descriptor is not { } store ||
            !store.TryGetProperty("user_isolation", out var isolation) || isolation.ValueKind != JsonValueKind.True ||
            !store.TryGetProperty("item_ttl_seconds", out var ttl) || ttl.GetInt32() != 604800)
            throw new InvalidDataException("Unexpected control store descriptor.");

        var page = await get(Catalog + "/items:keys", "&limit=3");
        if (page is not { } result || result.GetProperty("has_more").ValueKind != JsonValueKind.False ||
            result.GetProperty("data") is not { ValueKind: JsonValueKind.Array } items || items.GetArrayLength() != 2)
            throw new InvalidDataException("Expected exactly two diagnostic catalog records.");

        var keys = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var item in items.EnumerateArray())
        {
            var key = item.GetProperty("key").GetString();
            if (key is null || !Regex.IsMatch(key, @"\A[A-Za-z0-9_-]{1,128}\z"))
                throw new InvalidDataException("Unexpected catalog key.");
            var catalogItem = await get(ItemPath(Catalog, key), null);
            if (catalogItem is not { } entry) throw new InvalidDataException("Catalog record missing.");
            var record = entry.GetProperty("value").GetProperty("record");
            var owner = record.GetProperty("ownerKey").GetString();
            if (owner is not (UserA or UserB) || record.GetProperty("agentSessionId").GetString() != key ||
                record.GetProperty("branchId").GetString() != "101" || record.GetProperty("agent").GetString() != Agent ||
                record.GetProperty("version").GetString() != "2" || !keys.TryAdd(owner, key))
                throw new InvalidDataException("Catalog does not match the version 2 fixture.");
        }
        if (keys[UserA] == keys[UserB]) throw new InvalidDataException("Catalog keys must be distinct.");

        var ownKey = keys[user];
        var own = await get(ItemPath(Control, ownKey), null);
        var foreign = await get(ItemPath(Control, keys[user == UserA ? UserB : UserA]), null);
        if (own is not { } control) return new(false, foreign is not null, false, false);
        var data = control.GetProperty("value").GetProperty("record");
        var idleEmpty = data.GetProperty("owner").GetString() == user && data.GetProperty("session").GetString() == ownKey &&
            data.GetProperty("branch").GetString() == "101" && data.GetProperty("phase").GetInt32() == 0;
        var etag = control.GetProperty("etag").GetString();
        return new(true, foreign is not null, idleEmpty, !string.IsNullOrEmpty(etag) && etag != "*");
    }

    private static string ItemPath(string store, string key) => store + "/items/" + Uri.EscapeDataString(key);

    internal static async Task SelfTest()
    {
        const string a = "10000000-0000-0000-0000-000000000000";
        const string b = "20000000-0000-0000-0000-000000000000";
        static JsonElement Make(object value) => JsonSerializer.SerializeToElement(value);
        static JsonElement CatalogEntry(string ownerKey, string agentSessionId) =>
            Make(new { value = new { record = new { ownerKey, agentSessionId, branchId = "101", agent = Agent, version = "2" } } });
        static JsonElement ControlEntry(string owner, string session) =>
            Make(new { etag = "v1", value = new { record = new { owner, session, branch = "101", phase = 0 } } });

        foreach (var user in new[] { UserA, UserB })
        {
            var reads = new List<string>();
            var own = user == UserA ? a : b;
            var foreign = user == UserA ? b : a;
            var leak = false;
            Task<JsonElement?> GetFixture(string path, string? query)
            {
                reads.Add(path);
                return Task.FromResult<JsonElement?>(path switch
                {
                    Control => Make(new { user_isolation = true, item_ttl_seconds = 604800 }),
                    Catalog + "/items:keys" when query == "&limit=3" =>
                        Make(new { has_more = false, data = new[] { new { key = a }, new { key = b } } }),
                    Catalog + "/items/" + a => CatalogEntry(UserA, a),
                    Catalog + "/items/" + b => CatalogEntry(UserB, b),
                    _ when path == ItemPath(Control, own) => ControlEntry(user, own),
                    _ when path == ItemPath(Control, foreign) => leak ? ControlEntry(user == UserA ? UserB : UserA, foreign) : null,
                    _ => throw new InvalidDataException("Unexpected test read.")
                });
            }

            var evidence = await Inspect(user, GetFixture);
            if (evidence != new StateReadEvidence(true, false, true, true) ||
                !reads.TakeLast(2).SequenceEqual(new[] { ItemPath(Control, own), ItemPath(Control, foreign) }))
                throw new InvalidDataException("State probe read matrix failed.");
            leak = true;
            if (!(await Inspect(user, GetFixture)).ForeignVisible)
                throw new InvalidDataException("State probe must detect a visible foreign key.");
        }
        Console.WriteLine("PASS: A and B State Store read matrix with local fixtures.");
    }
}
