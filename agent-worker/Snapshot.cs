using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace Sdv.Worker;

internal sealed class SnapshotUnavailable : IOException;

internal sealed class Snapshot
{
    internal const int MaximumBytes = 900 * 1024;
    private const string Definition = "sdv-lookup-v1";
    private const string StateKey = "sdv-lookup-state";
    private readonly ChatClientAgent agent = new(new DisabledChat(), name: Definition);

    internal async Task<AgentSession> New(Caller caller, string branch, CancellationToken ct)
    {
        var session = await agent.CreateSessionAsync(ct);
        session.StateBag.SetValue(StateKey, new Context(Guid.NewGuid().ToString("D"), caller.Owner, caller.Session, branch, 0, null));
        return session;
    }

    internal async Task<byte[]> Save(AgentSession session, CancellationToken ct)
    {
        var serialized = await agent.SerializeSessionAsync(session, cancellationToken: ct);
        var bytes = Wire.Encode(new { schemaVersion = 1, mafVersion = "1.26.0", agentDefinitionVersion = Definition,
            serializedSession = serialized });
        if (bytes.Length > MaximumBytes) throw new SnapshotUnavailable();
        return bytes;
    }

    internal async Task<AgentSession> Load(byte[]? bytes, Caller caller, string branch, CancellationToken ct)
    {
        if (bytes is not { Length: <= MaximumBytes }) throw new SnapshotUnavailable();
        try
        {
            using var document = JsonDocument.Parse(bytes);
            var root = document.RootElement;
            Wire.Shape(root, ["schemaVersion", "mafVersion", "agentDefinitionVersion", "serializedSession"]);
            Wire.Require(root.GetProperty("schemaVersion").GetRawText() == "1" &&
                Wire.Text(root, "mafVersion") == "1.26.0" && Wire.Text(root, "agentDefinitionVersion") == Definition);
            var serialized = root.GetProperty("serializedSession");
            Wire.Require(serialized.ValueKind == JsonValueKind.Object);
            var session = await agent.DeserializeSessionAsync(serialized, cancellationToken: ct);
            var state = Read(session);
            Wire.Require(state.Owner == caller.Owner && state.Session == caller.Session && state.Branch == branch &&
                state.SuccessfulLookups < int.MaxValue);
            return session;
        }
        catch (Exception error) when (Wire.Invalid(error)) { throw new SnapshotUnavailable(); }
    }

    internal static void Complete(AgentSession session, string answer)
    {
        var state = Read(session);
        session.StateBag.SetValue(StateKey, state with { SuccessfulLookups = checked(state.SuccessfulLookups + 1), LastAnswer = answer });
    }

    internal static Context Read(AgentSession session)
    {
        if (!session.StateBag.TryGetValue<Context>(StateKey, out var state) || state is null ||
            !Guid.TryParseExact(state.MafSessionId, "D", out _) || state.SuccessfulLookups < 0 ||
            (state.SuccessfulLookups == 0) != (state.LastAnswer is null)) throw new InvalidDataException();
        return state;
    }

    internal sealed record Context(string MafSessionId, string Owner, string Session, string Branch,
        int SuccessfulLookups, string? LastAnswer);

    private sealed class DisabledChat : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options, CancellationToken cancellationToken) => throw new NotSupportedException();
        public object? GetService(Type serviceType, object? serviceKey) => null;
        public void Dispose() { }
    }
}
