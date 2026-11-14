using Microsoft.Agents.AI;
using System.Text.Json;

namespace Sdv.Worker;

internal sealed record Settings(string Agent, string Version, IReadOnlyDictionary<string, string> Queues,
    IReadOnlyDictionary<string, string> Fixtures);

internal sealed class Lookup(IState store, ITransport transport, Snapshot snapshots, Wire wire, Settings settings,
    TimeProvider clock)
{
    private const int IndexCapacity = 128;
    private const string IndexKeySuffix = "/admissions";
    private static readonly TimeSpan SendReserve = TimeSpan.FromSeconds(15);

    internal async Task<Disposition> Run(Caller caller, JsonElement body, CancellationToken ct)
    {
        Envelope e;
        string? fixture = null;
        try
        {
            e = wire.Read(body, caller, clock.GetUtcNow());
            if (caller.Source == Ingress.Prepare) Wire.Shape(e.Data, []);
            if (caller.Source == Ingress.Start)
            {
                Wire.Shape(e.Data, ["text"]);
                fixture = Wire.Text(e.Data, "text");
                Wire.Require(Wire.TextSize(fixture) <= 8192);
            }
        }
        catch (Exception error) when (Wire.Invalid(error)) { return Disposition.Discarded; }

        if (!settings.Queues.ContainsKey(e.Branch)) return Disposition.Discarded;
        string? number = null;
        if (fixture is not null)
        {
            if (!settings.Fixtures.TryGetValue(fixture, out number)) return Disposition.Discarded;
            if (!Wire.Match(number, "[0-9]{1,64}")) throw new InvalidOperationException("Invalid fixture configuration.");
        }
        var remaining = e.Expiry - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero) return Disposition.Discarded;
        using var deadline = new CancellationTokenSource(remaining, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var token = linked.Token;
        if (caller.Source == Ingress.Prepare) return await Prepare(caller, e, token);
        var current = await ReadControl(caller, token);
        if (current is null || current.Value.Owner != caller.Owner || current.Value.Session != caller.Session ||
            current.Value.Branch != e.Branch || !Live(e)) return Disposition.Discarded;
        return caller.Source == Ingress.Start
            ? await Start(caller, e, number!, current, token)
            : await Result(caller, e, current, token);
    }

    private async Task<Disposition> Prepare(Caller caller, Envelope e, CancellationToken ct)
    {
        var catalog = new Catalog(1, caller.Owner, caller.Session, e.Branch, clock.GetUtcNow(), settings.Agent, settings.Version);
        if (!Live(e) || await Create(caller, Category.Catalog, caller.Session, catalog, ct) is null || !Live(e))
            return Disposition.Discarded;
        var control = new Control(caller.Owner, caller.Session, e.Branch, Phase.IdleEmpty);
        return await Create(caller, Category.Control, caller.Session, control, ct) is null
            ? Disposition.Discarded : Disposition.Processed;
    }

    private async Task<Disposition> Start(Caller caller, Envelope e, string number, Stored<Control> current,
        CancellationToken ct)
    {
        var admission = new Admission(caller.Owner, caller.Session, e.Branch, e.RequestId, e.EventId,
            e.OperationId, e.Hash, e.Expiry, e.Expiry.AddSeconds(5));
        var reserved = await Reserve(caller, e, admission, ct);
        if (reserved != Disposition.Processed) return reserved;
        if (!Live(e) || await Create(caller, Category.Receipts, caller.Session + "/op/" + e.OperationId, admission, ct) is null)
            return Disposition.Discarded;

        if (Active(current.Value.Phase)) return await Busy(caller, e, ct);
        if (current.Value.Phase is not (Phase.IdleEmpty or Phase.IdleSuccess) || !Live(e)) return Disposition.Discarded;
        var input = current.Value with
        {
            Phase = Phase.InputClaimed, Active = admission, ProtectUntil = Max(current.Value.ProtectUntil, admission.ProtectUntil),
            Wait = null, ResultEventId = null
        };
        var claimed = await Replace(caller, current, input, ct);
        if (claimed is null)
        {
            var next = await ReadControl(caller, ct);
            return next is not null && next.Value.Owner == caller.Owner && next.Value.Session == caller.Session &&
                next.Value.Branch == e.Branch && next.Value.Phase != Phase.IdleEmpty && next.Value.Active is { } winner &&
                winner != admission && winner != current.Value.Active
                ? await Busy(caller, e, ct) : Disposition.Discarded;
        }
        if (!Live(e)) return Disposition.Discarded;
        byte[] pending;
        try
        {
            var session = current.Value.Phase == Phase.IdleEmpty
                ? await snapshots.New(caller, e.Branch, ct)
                : await Load(caller, e.Branch, current.Value, Category.Success, ct);
            pending = await snapshots.Save(session, ct);
        }
        catch (SnapshotUnavailable)
        {
            return await Finish(caller, e, claimed, input with { Phase = Phase.ClosedFailed, SnapshotRef = null },
                new(null, "HistoryUnavailable"), ct);
        }

        var now = clock.GetUtcNow();
        if (e.Expiry - now <= SendReserve)
            return await Finish(caller, e, claimed, input with { Phase = Phase.ClosedFailed, SnapshotRef = null },
                new(null, "InsufficientBudget"), ct);
        var command = wire.CreateCommand(caller, e, number, now);
        var pendingKey = SnapshotKey(caller);
        if (await store.Create(caller, Category.Pending, pendingKey, pending, ct) is null || !Live(e))
            return Disposition.Discarded;
        var waiting = input with
        {
            Phase = Phase.WaitingTool, SnapshotRef = pendingKey,
            Wait = new(command.Id, command.ToolId, command.Route, command.Hash, number, e.ExpiryText)
        };
        var confirmed = await Replace(caller, claimed, waiting, ct);
        if (confirmed is null) return Disposition.Discarded;
        var attempted = false;
        if (e.Expiry - clock.GetUtcNow() > SendReserve)
        {
            await TryEffect(e, async token =>
            {
                attempted = true;
                attempted = await transport.Send(e.Branch, command, e.Expiry, token);
            }, ct);
        }
        if (!attempted && Live(e) && !ct.IsCancellationRequested)
            return await Finish(caller, e, confirmed, waiting with { Phase = Phase.ClosedFailed, SnapshotRef = null },
                new(null, "InsufficientBudget"), ct);
        return Disposition.Processed;
    }

    private async Task<Disposition> Result(Caller caller, Envelope e, Stored<Control> current, CancellationToken ct)
    {
        var state = current.Value;
        if (state.Active is not { } admission || state.Wait is not { } wait || admission.RequestId != e.RequestId ||
            admission.OperationId != e.OperationId || admission.EventId == e.EventId || admission.Expiry != e.Expiry ||
            wait.ExpiryText != e.ExpiryText || wait.CommandId != e.CommandId || wait.ToolId != e.ToolCallId ||
            wait.Route != e.Route || wait.Hash != e.Hash) return Disposition.Discarded;

        Outcome outcome;
        try { outcome = Wire.ReadOutcome(e.Data, wait.Number); }
        catch (Exception error) when (Wire.Invalid(error)) { return Disposition.Discarded; }
        if (state.Phase != Phase.WaitingTool) return Disposition.Duplicate;
        if (!Live(e)) return Disposition.Discarded;
        var result = state with { Phase = Phase.ResultClaimed, ResultEventId = e.EventId };
        var claimed = await Replace(caller, current, result, ct);
        if (claimed is null) return Disposition.Discarded;

        var terminal = result with { Phase = Phase.ClosedFailed, SnapshotRef = null };
        if (outcome.Answer is { } answer)
        {
            if (!Live(e)) return Disposition.Discarded;
            byte[] success;
            try
            {
                var session = await Load(caller, e.Branch, result, Category.Pending, ct);
                Snapshot.Complete(session, answer);
                success = await snapshots.Save(session, ct);
            }
            catch (SnapshotUnavailable)
            {
                return await Finish(caller, e, claimed, terminal, new(null, "HistoryUnavailable"), ct);
            }
            var successKey = SnapshotKey(caller);
            var now = clock.GetUtcNow();
            if (!Live(e) || await store.Create(caller, Category.Success, successKey, success, ct) is null)
                return Disposition.Discarded;
            terminal = result with
            {
                Phase = Phase.IdleSuccess, SnapshotRef = successKey,
                LastSuccessfulAt = now, HistoryDeleteAt = now.AddDays(3)
            };
        }
        return await Finish(caller, e, claimed, terminal, outcome, ct);
    }

    private async Task<Disposition> Reserve(Caller caller, Envelope e, Admission admission, CancellationToken ct)
    {
        var key = caller.Session + IndexKeySuffix;
        for (var attempt = 0; attempt < 8 && Live(e); attempt++)
        {
            var read = Stored<Admission[]>.From(await store.Read(caller, Category.Receipts, key, ct));
            var now = clock.GetUtcNow();
            if (now >= e.Expiry) break;
            var entries = read?.Value.Where(entry => entry.ProtectUntil > now).ToArray() ?? [];
            var conflicts = entries.Where(entry => entry.RequestId == e.RequestId || entry.EventId == e.EventId ||
                entry.OperationId == e.OperationId).ToArray();
            if (conflicts.Length > 0)
                return conflicts.Length == 1 && conflicts[0] == admission ? Disposition.Duplicate : Disposition.Discarded;
            if (entries.Length >= IndexCapacity) throw new IOException("Admission index capacity reached.");
            var updated = Wire.Encode<Admission[]>([.. entries, admission]);
            var written = read is null
                ? await store.Create(caller, Category.Receipts, key, updated, ct)
                : await store.Replace(caller, Category.Receipts, key, read.Etag, updated, ct);
            if (written is not null) return Disposition.Processed;
        }
        if (Live(e)) throw new IOException("Admission reservation contention.");
        return Disposition.Discarded;
    }

    private async Task<AgentSession> Load(Caller caller, string branch, Control control, Category category, CancellationToken ct)
    {
        if (control.SnapshotRef is null || (category == Category.Success && !(control.HistoryDeleteAt > clock.GetUtcNow())))
            throw new SnapshotUnavailable();
        var item = await store.Read(caller, category, control.SnapshotRef, ct);
        return await snapshots.Load(item?.Value, caller, branch, ct);
    }

    private async Task<Disposition> Finish(Caller caller, Envelope e, Stored<Control> claimed, Control terminal,
        Outcome outcome, CancellationToken ct)
    {
        if (!Live(e) || await Replace(caller, claimed, terminal, ct) is null) return Disposition.Discarded;
        await Publish(caller, e, outcome, ct);
        return Disposition.Processed;
    }

    private async Task<Disposition> Busy(Caller caller, Envelope e, CancellationToken ct)
    {
        await Publish(caller, e, new(null, "Busy"), ct);
        return Disposition.Discarded;
    }

    private Task Publish(Caller caller, Envelope e, Outcome outcome, CancellationToken ct)
    {
        var body = outcome.Answer is { } answer
            ? Wire.Encode(new { schemaVersion = 1, kind = "turn.final", e.RequestId, e.OperationId, status = "Answered", answer })
            : Wire.Encode(new { schemaVersion = 1, kind = "turn.final", e.RequestId, e.OperationId, status = "Failed", outcome.ErrorCode });
        return TryEffect(e, token => transport.Publish(caller, body, token), ct);
    }

    private async Task TryEffect(Envelope e, Func<CancellationToken, Task> effect, CancellationToken ct)
    {
        var remaining = e.Expiry - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero || ct.IsCancellationRequested) return;
        using var deadline = new CancellationTokenSource(remaining, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        try { await effect(linked.Token); }
        catch (Exception error) when (error is OperationCanceledException or IOException or Azure.RequestFailedException or
            System.ClientModel.ClientResultException or Azure.Messaging.ServiceBus.ServiceBusException or HttpRequestException or
            Azure.Identity.AuthenticationFailedException or TimeoutException)
        {
            // A missing acknowledgment never authorizes a second effect.
        }
    }

    private async Task<Stored<Control>?> ReadControl(Caller caller, CancellationToken ct)
    {
        var control = Stored<Control>.From(await store.Read(caller, Category.Control, caller.Session, ct));
        if (control is not null && !Enum.IsDefined(control.Value.Phase)) throw new IOException("Invalid control phase.");
        return control;
    }
    private async Task<Stored<Control>?> Replace(Caller caller, Stored<Control> current, Control next, CancellationToken ct)
    {
        var etag = await store.Replace(caller, Category.Control, caller.Session, current.Etag, Wire.Encode(next), ct);
        return etag is null ? null : new(next, etag);
    }
    private Task<string?> Create<T>(Caller caller, Category category, string key, T value, CancellationToken ct) =>
        store.Create(caller, category, key, Wire.Encode(value), ct);
    private bool Live(Envelope e) => clock.GetUtcNow() < e.Expiry;
    private static bool Active(Phase phase) => phase is Phase.InputClaimed or Phase.WaitingTool or Phase.ResultClaimed;
    private static string SnapshotKey(Caller caller) => caller.Session + "/snapshot/" + Guid.NewGuid().ToString("D");
    private static DateTimeOffset Max(DateTimeOffset? earlier, DateTimeOffset later) =>
        earlier is { } previous && previous > later ? previous : later;
}
