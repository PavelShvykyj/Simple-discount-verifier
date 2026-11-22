using Microsoft.Extensions.Options;
using Microsoft.Agents.AI;
using System.Text.Json;

namespace Sdv.Worker;

internal sealed class Lookup(IState store, ITransport transport, Snapshot snapshots, Wire wire, IOptions<WorkerOptions> options,
    TimeProvider clock, ILogger<Lookup> logger)
{
    private sealed record Outcome(string? Answer, string? ErrorCode);

    private readonly WorkerOptions settings = options.Value;
    private const int CatalogSchemaVersion = 1;
    private const int IndexCapacity = 128;
    private const int AdmissionWriteAttempts = 8;
    private const string BusyErrorCode = "Busy";
    private const string HistoryUnavailableErrorCode = "HistoryUnavailable";
    private const string InsufficientBudgetErrorCode = "InsufficientBudget";
    private const string IndexKeySuffix = "/admissions";

    internal async Task<Disposition> Run(Caller caller, JsonElement body, CancellationToken ct)
    {
        Envelope e;
        string? number = null;
        try
        {
            e = wire.Read(body, caller, clock.GetUtcNow());
            if (e.Kind == Ingress.Prepare) Wire.Shape(e.Data, []);
            if (e.Kind == Ingress.Start)
            {
                Wire.Shape(e.Data, ["text"]);
                number = Wire.Text(e.Data, "text");
                Wire.Require(Wire.Match(number, WorkerOptions.OrderNumberPattern));
            }
        }
        catch (Exception error) when (Wire.Invalid(error)) { return Reject(RejectionReason.InvalidSignedEnvelope); }

        using var scope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["Ingress"] = e.Kind, ["RequestId"] = e.RequestId, ["OperationId"] = e.OperationId,
            ["CommandId"] = e.CommandId, ["ToolCallId"] = e.ToolCallId
        });
        WorkerLog.EventVerified(logger);
        if (!settings.Queues.ContainsKey(e.Branch)) return Reject(RejectionReason.BranchNotConfigured);
        var remaining = e.Expiry - clock.GetUtcNow();
        if (remaining <= TimeSpan.Zero) return Reject(RejectionReason.Expired);
        using var deadline = new CancellationTokenSource(remaining, clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        var token = linked.Token;
        try
        {
            if (e.Kind == Ingress.Prepare) return await Prepare(caller, e, token);
            var current = await ReadControl(caller, token);
            if (current is null || current.Value.Owner != caller.Owner || current.Value.Session != caller.Session ||
                current.Value.Branch != e.Branch) return Reject(RejectionReason.ControlMismatch);
            if (!Live(e)) return Reject(RejectionReason.Expired);
            return e.Kind == Ingress.Start
                ? await Start(caller, e, number!, current, token)
                : await Result(caller, e, current, token);
        }
        catch (OperationCanceledException error) when (deadline.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException("The Worker event deadline elapsed during processing.", error);
        }
    }

    private async Task<Disposition> Prepare(Caller caller, Envelope e, CancellationToken ct)
    {
        var catalog = new Catalog(CatalogSchemaVersion, caller.Owner, caller.Session, e.Branch, clock.GetUtcNow(), settings.Agent, settings.Version);
        if (!Live(e)) return Reject(RejectionReason.Expired);
        if (await Create(caller, Category.Catalog, caller.Session, catalog, ct) is null)
            return Reject(RejectionReason.AdmissionConflict);
        if (!Live(e)) return Reject(RejectionReason.Expired);
        var control = new Control(caller.Owner, caller.Session, e.Branch, Phase.IdleEmpty);
        if (await Create(caller, Category.Control, caller.Session, control, ct) is null)
            return Reject(RejectionReason.AdmissionConflict);
        WorkerLog.PhaseChanged(logger, Phase.IdleEmpty);
        return Disposition.Processed;
    }

    private async Task<Disposition> Start(Caller caller, Envelope e, string number, Stored<Control> current,
        CancellationToken ct)
    {
        var admission = new Admission(caller.Owner, caller.Session, e.Branch, e.RequestId, e.EventId,
            e.OperationId, e.Hash, e.Expiry, e.Expiry.Add(LookupTiming.ClockSkew));
        var reserved = await Reserve(caller, e, admission, ct);
        if (reserved != Disposition.Processed) return reserved;
        if (!Live(e)) return Reject(RejectionReason.Expired);
        var receiptKey = StateKey.Compound(caller.Session, "/op/" + e.OperationId);
        if (await Create(caller, Category.Receipts, receiptKey, admission, ct) is null)
            return Reject(RejectionReason.AdmissionConflict);

        if (Active(current.Value.Phase)) return await Busy(caller, e, ct);
        if (current.Value.Phase is not (Phase.IdleEmpty or Phase.IdleSuccess)) return Reject(RejectionReason.InvalidPhase);
        if (!Live(e)) return Reject(RejectionReason.Expired);
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
                ? await Busy(caller, e, ct) : Reject(RejectionReason.AdmissionConflict);
        }
        WorkerLog.PhaseChanged(logger, Phase.InputClaimed);
        if (!Live(e)) return Reject(RejectionReason.Expired);
        byte[] pending;
        try
        {
            var session = current.Value.Phase == Phase.IdleEmpty
                ? await snapshots.New(caller, e.Branch, ct)
                : await Load(caller, e.Branch, current.Value, Category.Success, ct);
            pending = await snapshots.Save(session, ct);
        }
        catch (SnapshotUnavailable error)
        {
            WorkerLog.HistoryUnavailable(logger, error);
            return await Finish(caller, e, claimed, input with { Phase = Phase.ClosedFailed, SnapshotRef = null },
                new(null, HistoryUnavailableErrorCode), ct);
        }

        var now = clock.GetUtcNow();
        if (e.Expiry - now <= LookupTiming.SendReserve)
            return await Finish(caller, e, claimed, input with { Phase = Phase.ClosedFailed, SnapshotRef = null },
                new(null, InsufficientBudgetErrorCode), ct);
        var command = Wire.CreateCommand(e, number, now);
        var pendingKey = SnapshotKey(caller);
        if (await store.Create(caller, Category.Pending, pendingKey, pending, ct) is null)
            return Reject(RejectionReason.SnapshotConflict);
        if (!Live(e)) return Reject(RejectionReason.Expired);
        var waiting = input with
        {
            Phase = Phase.WaitingTool, SnapshotRef = pendingKey,
            Wait = new(command.Id, command.ToolId, command.Route, command.Hash, number, e.ExpiryText)
        };
        var confirmed = await Replace(caller, claimed, waiting, ct);
        if (confirmed is null) return Reject(RejectionReason.WaitingConflict);
        using var commandScope = logger.BeginScope(new Dictionary<string, object?>
        {
            ["CommandId"] = command.Id, ["ToolCallId"] = command.ToolId
        });
        WorkerLog.PhaseChanged(logger, Phase.WaitingTool);
        var attempted = false;
        if (e.Expiry - clock.GetUtcNow() > LookupTiming.SendReserve && CanAttemptEffect(e, EffectKind.SendCommand, ct))
            attempted = await transport.Send(e.Branch, command, e.Expiry, ct);
        if (!attempted && Live(e) && !ct.IsCancellationRequested)
            return await Finish(caller, e, confirmed, waiting with { Phase = Phase.ClosedFailed, SnapshotRef = null },
                new(null, InsufficientBudgetErrorCode), ct);
        return Disposition.Processed;
    }

    private async Task<Disposition> Result(Caller caller, Envelope e, Stored<Control> current, CancellationToken ct)
    {
        var state = current.Value;
        if (state.Active is not { } admission || state.Wait is not { } wait || admission.RequestId != e.RequestId ||
            admission.OperationId != e.OperationId || admission.EventId == e.EventId || admission.Expiry != e.Expiry ||
            wait.ExpiryText != e.ExpiryText || wait.CommandId != e.CommandId || wait.ToolId != e.ToolCallId ||
            wait.Route != e.Route || wait.Hash != e.Hash) return Reject(RejectionReason.ResultMismatch);

        (string Kind, string ReasonCode) connectorOutcome;
        try { connectorOutcome = Wire.ReadOutcome(e.Data, wait.Number); }
        catch (Exception error) when (Wire.Invalid(error)) { return Reject(RejectionReason.InvalidOutcome); }
        if (state.Phase != Phase.WaitingTool) return Disposition.Duplicate;
        if (!Live(e)) return Reject(RejectionReason.Expired);
        var result = state with { Phase = Phase.ResultClaimed, ResultEventId = e.EventId };
        var claimed = await Replace(caller, current, result, ct);
        if (claimed is null) return Reject(RejectionReason.AdmissionConflict);
        WorkerLog.PhaseChanged(logger, Phase.ResultClaimed);
        WorkerLog.ToolResultClaimed(logger, connectorOutcome.Kind, connectorOutcome.ReasonCode);

        var outcome = connectorOutcome.Kind switch
        {
            Wire.FoundOutcome => new Outcome("Заказ найден в выбранном филиале.", null),
            Wire.NotFoundOutcome => new Outcome("Заказ не найден в выбранном филиале.", null),
            _ => new Outcome(null, connectorOutcome.ReasonCode)
        };
        var terminal = result with { Phase = Phase.ClosedFailed, SnapshotRef = null };
        if (outcome.Answer is { } answer)
        {
            if (!Live(e)) return Reject(RejectionReason.Expired);
            byte[] success;
            try
            {
                var session = await Load(caller, e.Branch, result, Category.Pending, ct);
                Snapshot.Complete(session, answer);
                success = await snapshots.Save(session, ct);
            }
            catch (SnapshotUnavailable error)
            {
                WorkerLog.HistoryUnavailable(logger, error);
                return await Finish(caller, e, claimed, terminal, new(null, HistoryUnavailableErrorCode), ct);
            }
            var successKey = SnapshotKey(caller);
            var now = clock.GetUtcNow();
            if (!Live(e)) return Reject(RejectionReason.Expired);
            if (await store.Create(caller, Category.Success, successKey, success, ct) is null)
                return Reject(RejectionReason.SnapshotConflict);
            terminal = result with
            {
                Phase = Phase.IdleSuccess, SnapshotRef = successKey,
                LastSuccessfulAt = now, HistoryDeleteAt = now.Add(LookupTiming.SuccessHistoryLifetime)
            };
        }
        return await Finish(caller, e, claimed, terminal, outcome, ct);
    }

    private async Task<Disposition> Reserve(Caller caller, Envelope e, Admission admission, CancellationToken ct)
    {
        var key = StateKey.Compound(caller.Session, IndexKeySuffix);
        for (var attempt = 0; attempt < AdmissionWriteAttempts && Live(e); attempt++)
        {
            var read = Stored<Admission[]>.From(await store.Read(caller, Category.Receipts, key, ct));
            var now = clock.GetUtcNow();
            if (now >= e.Expiry) break;
            var entries = read?.Value.Where(entry => entry.ProtectUntil > now).ToArray() ?? [];
            var conflicts = entries.Where(entry => entry.RequestId == e.RequestId || entry.EventId == e.EventId ||
                entry.OperationId == e.OperationId).ToArray();
            if (conflicts.Length > 0)
                return conflicts.Length == 1 && conflicts[0] == admission ? Disposition.Duplicate : Reject(RejectionReason.AdmissionConflict);
            if (entries.Length >= IndexCapacity) throw new IOException("Admission index capacity reached.");
            var updated = Wire.Encode<Admission[]>([.. entries, admission]);
            var written = read is null
                ? await store.Create(caller, Category.Receipts, key, updated, ct)
                : await store.Replace(caller, Category.Receipts, key, read.Etag, updated, ct);
            if (written is not null) return Disposition.Processed;
        }
        if (Live(e)) throw new IOException("Admission reservation contention.");
        return Reject(RejectionReason.Expired);
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
        if (!Live(e)) return Reject(RejectionReason.Expired);
        if (await Replace(caller, claimed, terminal, ct) is null) return Reject(RejectionReason.TerminalConflict);
        WorkerLog.TurnFinalized(logger, terminal.Phase, outcome.ErrorCode);
        await Publish(caller, e, outcome, ct);
        return Disposition.Processed;
    }

    private async Task<Disposition> Busy(Caller caller, Envelope e, CancellationToken ct)
    {
        WorkerLog.SessionBusy(logger);
        await Publish(caller, e, new(null, BusyErrorCode), ct);
        return Disposition.Discarded;
    }

    private Task Publish(Caller caller, Envelope e, Outcome outcome, CancellationToken ct) =>
        CanAttemptEffect(e, EffectKind.PublishFinal, ct)
            ? transport.Publish(caller, Wire.CreateTurnFinal(e, outcome.Answer, outcome.ErrorCode), ct)
            : Task.CompletedTask;

    private bool CanAttemptEffect(Envelope e, EffectKind kind, CancellationToken ct)
    {
        if (Live(e) && !ct.IsCancellationRequested) return true;
        WorkerLog.EffectSkipped(logger, kind);
        return false;
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
        if (etag is null) WorkerLog.PhaseConflict(logger, next.Phase);
        return etag is null ? null : new(next, etag);
    }
    private Task<string?> Create<T>(Caller caller, Category category, string key, T value, CancellationToken ct) =>
        store.Create(caller, category, key, Wire.Encode(value), ct);
    private Disposition Reject(RejectionReason reason)
    {
        WorkerLog.EventRejected(logger, reason);
        return Disposition.Discarded;
    }
    private bool Live(Envelope e) => clock.GetUtcNow() < e.Expiry;
    private static bool Active(Phase phase) => phase is Phase.InputClaimed or Phase.WaitingTool or Phase.ResultClaimed;
    private static string SnapshotKey(Caller caller) =>
        StateKey.Compound(caller.Session, "/snapshot/" + Guid.NewGuid().ToString("D"));
    private static DateTimeOffset Max(DateTimeOffset? earlier, DateTimeOffset later) =>
        earlier is { } previous && previous > later ? previous : later;
}
