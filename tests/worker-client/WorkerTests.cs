using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Azure;
using Xunit;
using Sdv.Worker;
using WorkerChecks;

public sealed class Checks : IAsyncLifetime
{
    private readonly Harness h = new();
    public Task InitializeAsync() => h.Start();
    public Task DisposeAsync() => h.DisposeAsync().AsTask();

    private int passed;

    [Fact]
    public async Task LocalMatrix()
    {
        await Authorization();
        await HostedInvocation();
        await SigningAndSkew();
        await FastAndConcurrent();
        await HistoryAndFailures();
        await Ambiguity();
        await DeadlineAndCleanup();
        await SubscriberValidation();
        Console.WriteLine($"Worker local matrix: {passed} checks passed.");
    }

    private void Check(bool condition, string name)
    {
        Fixtures.Need(condition, name);
        passed++;
    }

    private void Is(string actual, string expected, string name) =>
        Check(actual == expected, $"{name} (expected {expected}, got {actual})");
    private static Scope Scope(string name) => new("sdv:test:alice", "sandbox-" + name);
    private JsonObject Input(Scope scope, string text = "00042", string branch = "101") =>
        h.Fixtures.Input(scope, Ingress.Start, text, branch);
    private Task<string> Start(Scope scope, JsonObject body) => h.StartLookup(scope, body);
    private Task<string> Result(Scope scope, JsonObject body) => h.Invoke(scope, body);
    private JsonObject Command(Scope scope, JsonObject start, string number = "00042")
    {
        var sent = h.Transport.Commands.Last();
        var branch = start["branchId"]!.ToString();
        Check(sent.Branch == branch && sent.Deadline == DateTimeOffset.Parse(start["expiresAt"]!.ToString()),
            "Branch sender and original deadline");
        return Fixtures.CommandCheck(sent.Command.Body, branch, number, scope, h.SwaKey, start);
    }

    private async Task Authorization()
    {
        var a = h.Alice;
        var b = h.Bob;
        var input = Input(a);
        Is(await Start(a, input), "NotPrepared", "Client blocks unprepared start");
        Is(await h.Invoke(a, input), "Discarded", "Worker rejects raw unprepared start");
        Is(await h.Invoke(a, h.Fixtures.Input(a, Ingress.Prepare), platformOwner: false),
            "403", "Spoofed platform and forwarded headers");
        Is(await h.Prepare(a), "Processed", "Catalog-first prepare");
        Check(h.Store.Writes.ToArray()[..2].SequenceEqual(new[] { "Catalog", "IdleEmpty" }), "Catalog/control write order");
        Is(await h.Prepare(a), "Discarded", "Cannot reuse prepared session");
        Is(await h.Prepare(b, "102"), "Processed", "Second owner and branch");
        var writes = h.Store.Writes.Count;
        Is(await h.Invoke(a, input, callId: ""), "403", "Start without the platform call ID");
        Is(await h.Invoke(a, input, selector: b.Session), "403", "Sandbox selector mismatch");
        Is(await h.Invoke(a, input, noSelector: true), "403", "Sandbox fallback denied");
        Is(await h.Invoke(new(b.Owner, a.Session), input), "Discarded", "Delegated owner mismatch");
        var spoof = Input(new(b.Owner, a.Session));
        Is(await h.Invoke(new(b.Owner, a.Session), spoof), "Discarded", "Isolated control with valid foreign owner signature");
        var wrongKind = Fixtures.Copy(input);
        wrongKind["kind"] = "session.prepare";
        Is(await h.Invoke(a, wrongKind), "Discarded", "Start signature cannot authorize prepare");
        Is(await Start(a, Input(a, branch: "102")), "Discarded", "Prepared branch mismatch");
        Is(await Start(a, Input(a, "ordinary arbitrary request")), "Discarded", "Non-numeric lookup input is rejected");
        var expired = h.Fixtures.Input(a, Ingress.Start, clientSent: h.Clock.GetUtcNow().AddSeconds(-121));
        Is(await Start(a, expired), "Discarded", "Server rejects elapsed client deadline");
        var overCap = h.Fixtures.Input(a, Ingress.Start, clientSent: h.Clock.GetUtcNow().AddSeconds(180));
        Check(DateTimeOffset.Parse(overCap["expiresAt"]!.ToString()) == h.Clock.GetUtcNow().AddSeconds(120), "Server deadline cap");
        foreach (var (field, value) in new[] { ("iss", "untrusted"), ("aud", "untrusted"), ("dataHash", new string('0', 64)),
                     ("agentSessionId", b.Session), ("userKey", b.Owner), ("branchId", "102") })
        {
            var invalid = Fixtures.Copy(input);
            h.Fixtures.Sign(invalid, a, claims => claims[field] = value);
            Is(await Start(a, invalid), "Discarded", "Signed mismatch " + field);
        }
        var badKid = Fixtures.Copy(input);
        h.Fixtures.Sign(badKid, a, kid: "unknown");
        Is(await Start(a, badKid), "Discarded", "Unknown kid");
        var badAlg = Fixtures.Copy(input);
        h.Fixtures.Sign(badAlg, a, algorithm: "RS256");
        Is(await Start(a, badAlg), "Discarded", "Algorithm mismatch");
        var badSig = Fixtures.Copy(input);
        var route = badSig["route"]!.ToString().Split('.');
        route[2] = (route[2][0] == 'A' ? "B" : "A") + route[2][1..];
        badSig["route"] = string.Join('.', route);
        Is(await Start(a, badSig), "Discarded", "PS256 signature tampering");
        var upload = Fixtures.Copy(input);
        upload["dataB64"] = Fixtures.B64("""{"text":"00042","upload":{"path":"bogus"}}"""u8.ToArray());
        h.Fixtures.Sign(upload, a);
        Is(await Start(a, upload), "Discarded", "Upload rejected even when signed");
        var wrongVersion = Fixtures.Copy(input);
        wrongVersion["schemaVersion"] = JsonNode.Parse("1.0");
        Is(await Start(a, wrongVersion), "Discarded", "Exact JSON integer version");
        var duplicate = Encoding.UTF8.GetBytes(input.ToJsonString()[..^1] + ",\"branchId\":\"101\"}");
        Is(await h.Invoke(a, input, raw: duplicate), "Discarded", "Duplicate envelope property");
        var duplicateEvent = Encoding.UTF8.GetBytes(input.ToJsonString()[..^1] + ",\"eventId\":\"" + Fixtures.Id() + "\"}");
        Is(await h.Invoke(a, input, raw: duplicateEvent), "400", "Duplicate event ID has no unambiguous ACK");
        Is(await h.Invoke(a, input, raw: "{"u8.ToArray()), "400", "Malformed JSON");
        Is(await h.Invoke(a, input, raw: [0xff]), "400", "Invalid UTF-8");
        Is(await h.Invoke(a, input, raw: new byte[65537]), "413", "Bounded envelope");
        Check(h.Store.Writes.Count == writes && h.Transport.Commands.IsEmpty && h.Transport.Attempts.IsEmpty,
            "Rejected input has no writes or effects");
    }

    [Theory]
    [InlineData(false, "sandbox-a")]
    [InlineData(true, null)]
    [InlineData(true, "sandbox-b")]
    public async Task HostedPrepareRejectsInvalidPlatformSession(bool isHosted, string? session)
    {
        await using var hosted = new Harness();
        await hosted.Start(new FoundryHostIdentity(isHosted, "fixture-agent", "fixture-v1", session));
        var owner = hosted.Alice;
        var prepare = hosted.Fixtures.Input(owner, Ingress.Prepare);

        Assert.Equal("403", await hosted.Invoke(owner, prepare));
        Assert.Empty(hosted.Store.Writes);
    }

    [Theory]
    [InlineData("Application/JSON", "Processed")]
    [InlineData("application/json; charset=utf-8", "Processed")]
    [InlineData("application/json; charset=iso-8859-1", "415")]
    [InlineData("text/plain", "415")]
    public async Task InvocationChecksMediaTypeAndEncoding(string contentType, string expected)
    {
        var prepare = h.Fixtures.Input(h.Alice, Ingress.Prepare);
        Assert.Equal(expected, await h.Invoke(h.Alice, prepare, contentType: contentType));
    }

    [Fact]
    public async Task MaximumLengthSessionKeepsLookupAndReplayKeysWithinStoreLimits()
    {
        var scope = new Scope(h.Alice.Owner, new string('s', 128));
        Assert.Equal("Processed", await h.Prepare(scope));
        var start = Input(scope);

        Assert.Equal("Processed", await Start(scope, start));
        Assert.Equal("Duplicate", await Start(scope, start));
        var command = JsonNode.Parse(Assert.Single(h.Transport.Commands).Command.Body)!.AsObject();
        var result = h.Fixtures.Result(command, Fixtures.Missing);
        Assert.Equal("Processed", await Result(scope, result));
        Assert.Equal(Phase.IdleSuccess, (await h.State(scope)).Phase);
        Assert.Single(h.Transport.Attempts);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    public async Task TransientStoreFailureReturnsUnavailableWithoutAcknowledgement(int dependencyStatus)
    {
        Assert.Equal("Processed", await h.Prepare(h.Alice));
        var writesBefore = h.Store.Writes.Count;
        h.Store.AfterRead = (_, _, _, _) => throw new RequestFailedException(dependencyStatus, "Synthetic Store failure.");

        Assert.Equal("503", await Start(h.Alice, Input(h.Alice)));
        Assert.Equal(writesBefore, h.Store.Writes.Count);
        Assert.Empty(h.Transport.Commands);
    }

    [Theory]
    [InlineData("eventId", false, "400")]
    [InlineData("requestId", false, "Discarded")]
    [InlineData("kind", true, "Discarded")]
    public async Task InvocationRejectsInvalidStringFields(string field, bool remove, string expected)
    {
        var input = h.Fixtures.Input(h.Alice, Ingress.Prepare);
        if (remove) input.Remove(field);
        else input[field] = 42;

        Assert.Equal(expected, await h.Invoke(h.Alice, input));
        Assert.Empty(h.Store.Writes);
    }

    [Theory]
    [InlineData("eventId", "400")]
    [InlineData("branchId", "Discarded")]
    public async Task InvocationRejectsUnpairedUnicodeEscapes(string field, string expected)
    {
        var input = h.Fixtures.Input(h.Alice, Ingress.Prepare);
        var original = input[field]!.ToJsonString();
        var raw = Encoding.UTF8.GetBytes(input.ToJsonString().Replace(original, "\"\\uD800\"", StringComparison.Ordinal));

        Assert.Equal(expected, await h.Invoke(h.Alice, input, raw: raw));
        Assert.Empty(h.Store.Writes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("e30=")]
    [InlineData(" e30")]
    [InlineData("e31")]
    [InlineData("e3+")]
    public async Task PrepareRejectsBase64UrlOutsideCanonicalForm(string encoded)
    {
        var input = h.Fixtures.Input(h.Alice, Ingress.Prepare);
        input["dataB64"] = encoded;

        Assert.Equal("Discarded", await h.Invoke(h.Alice, input));
        Assert.Empty(h.Store.Writes);
    }

    private async Task HostedInvocation()
    {
        await using var hosted = new Harness();
        var owner = hosted.Alice;
        await hosted.Start(new FoundryHostIdentity(true, "fixture-agent", "fixture-v1", owner.Session));

        var prepare = hosted.Fixtures.Input(owner, Ingress.Prepare);
        Is(await hosted.Invoke(owner, prepare, callId: ""),
            "403", "Hosted prepare requires the platform call ID");
        Is(await hosted.Invoke(owner, prepare, platformOwner: false),
            "403", "Hosted prepare requires the platform owner");
        var other = new Scope(hosted.Bob.Owner, owner.Session);
        Is(await hosted.Invoke(other, prepare),
            "Discarded", "Hosted prepare rejects signed route for another owner");
        Is(await hosted.Invoke(hosted.Bob, hosted.Fixtures.Input(hosted.Bob, Ingress.Prepare)),
            "403", "Hosted prepare rejects a different runtime session");

        var tampered = prepare.DeepClone().AsObject();
        var route = tampered["route"]!.GetValue<string>();
        var firstSignatureByte = route.LastIndexOf('.') + 1;
        tampered["route"] = route[..firstSignatureByte] + (route[firstSignatureByte] == 'A' ? "B" : "A") +
            route[(firstSignatureByte + 1)..];
        Is(await hosted.Invoke(owner, tampered),
            "Discarded", "Hosted prepare rejects an invalid signature");
        Check(hosted.Store.Writes.IsEmpty, "Hosted rejections make no State Store writes");

        var opaqueCallId = new string('a', 200);
        var creates = new List<(string CallId, Category Category)>();
        hosted.Store.BeforeCreate = (caller, category) => creates.Add((caller.CallId, category));
        Is(await hosted.Invoke(owner, prepare, callId: opaqueCallId),
            "Processed", "Hosted prepare accepts the SDK context and opaque platform call ID");
        Check(creates.SequenceEqual(new[] { (opaqueCallId, Category.Catalog), (opaqueCallId, Category.Control) }),
            "Hosted prepare forwards the unchanged opaque call ID to the store");
        var control = await hosted.Control(owner);
        Check(control is not null && (await hosted.State(owner)).Phase == Phase.IdleEmpty,
            "Hosted prepare creates the owner control");
        Is(await hosted.Invoke(owner, prepare),
            "Discarded", "Hosted prepare rejects a replay");
        Check((await hosted.Control(owner))?.Etag == control!.Etag &&
            hosted.Store.Writes.ToArray().SequenceEqual(new[] { "Catalog", "IdleEmpty" }) &&
            hosted.Transport.Commands.IsEmpty && hosted.Transport.Attempts.IsEmpty,
            "Hosted prepare leaves one pair of writes and no business effects");

        var subscriber = hosted.Transport.Subscribe(owner);
        var start = hosted.Fixtures.Input(owner, Ingress.Start);
        hosted.Transport.Expect(owner, start);
        Is(await hosted.Invoke(owner, start), "Processed", "Invocations accepts signed start");
        var command = JsonNode.Parse(Assert.Single(hosted.Transport.Commands).Command.Body)!.AsObject();
        var result = hosted.Fixtures.Result(command, Fixtures.Missing);
        var unsigned = Fixtures.Copy(result);
        unsigned.Remove("resultProof");
        Is(await hosted.Invoke(owner, unsigned), "Discarded", "Invocations rejects result without Router proof");
        Assert.Empty(hosted.Transport.Attempts);
        Is(await hosted.Invoke(owner, result), "Processed", "Invocations accepts signed result");
        Assert.Equal("turn.final", Assert.Single(subscriber.Received)["kind"]!.GetValue<string>());
        Is(await hosted.Invoke(owner, result), "Duplicate", "Invocations recognizes a duplicate result");
        Assert.Single(hosted.Transport.Attempts);
    }

    private async Task SigningAndSkew()
    {
        var scope = Scope("skew");
        Is(await h.Prepare(scope), "Processed", "Prepare signed skew scenario");
        var start = Input(scope);
        var issued = h.Clock.GetUtcNow().AddSeconds(5);
        start["issuedAt"] = Fixtures.Stamp(issued);
        start["expiresAt"] = Fixtures.Stamp(issued.AddSeconds(120));
        h.Fixtures.Sign(start, scope);
        var oversized = Fixtures.Copy(start);
        oversized["expiresAt"] = Fixtures.Stamp(issued.AddSeconds(121));
        h.Fixtures.Sign(oversized, scope);
        var sends = h.Transport.Commands.Count;
        Is(await Start(scope, oversized), "Discarded", "Signed 121-second envelope rejected");
        Check(h.Transport.Commands.Count == sends, "Oversized deadline no Send");
        Is(await Start(scope, start), "Processed", "Valid clock five seconds ahead");
        var command = Command(scope, start);
        var commandIssued = DateTimeOffset.Parse(command["issuedAt"]!.ToString());
        var expiry = DateTimeOffset.Parse(command["expiresAt"]!.ToString());
        Check(commandIssued == issued && expiry == issued.AddSeconds(120), "S4 command preserves expiry within 120 seconds");

        var result = h.Fixtures.Result(command, Fixtures.Missing);
        var other = Fixtures.Copy(result);
        other["route"] = Input(scope)["route"]!.DeepClone();
        h.Fixtures.SignResult(other);
        Is(await Result(scope, other), "Discarded", "Router-attested different route cannot claim stored wait");
        Check((await h.State(scope)).Phase == Phase.WaitingTool, "Alternate signed route preserves wait");
        Is(await Result(scope, result), "Processed", "Exact stored route completes valid skew command");
    }

    private async Task FastAndConcurrent()
    {
        var a = h.Alice;
        var b = h.Bob;
        var alice = h.Transport.Subscribe(a).Received;
        var bob = h.Transport.Subscribe(b).Received;
        var input = Input(a);
        var initialSends = h.Transport.Commands.Count;
        var initialPublications = h.Transport.Attempts.Count;
        JsonObject? fastResult = null;
        var finishedWrites = -1;
        h.Transport.OnSend = async _ =>
        {
            var wait = await h.State(a);
            Check(wait.Phase == Phase.WaitingTool && wait.SnapshotRef is not null &&
                await h.Store.Read(Harness.Caller(a), Category.Pending, wait.SnapshotRef, default) is not null,
                "Full wait and snapshot before Send");
            var command = Command(a, input);
            fastResult = h.Fixtures.Result(command, Fixtures.Found("00042", "101"));
            Is(await Result(a, fastResult), "Processed", "Fast result before Send returns");
            finishedWrites = h.Store.Writes.Count;
            throw new IOException("Synthetic lost Send acknowledgment.");
        };
        Is(await Start(a, input), "Processed", "Waiting ACK after one ambiguous Send");
        h.Transport.OnSend = null;
        Check(h.Store.Writes.Count == finishedWrites && (await h.State(a)).Phase == Phase.IdleSuccess,
            "Zero start writes after Send");
        Is(await Start(a, input), "Duplicate", "Lost start ACK redelivery");
        Is(await Result(a, fastResult!), "Duplicate", "Lost result ACK redelivery");
        var another = Fixtures.Copy(fastResult!);
        another["eventId"] = Fixtures.Id();
        h.Fixtures.SignResult(another);
        Is(await Result(a, another), "Duplicate", "Fresh result event ID cannot repeat final");
        Check(h.Transport.Commands.Count == initialSends + 1 && h.Transport.Attempts.Count == initialPublications + 1 &&
            alice.Count == 1 && bob.IsEmpty,
            "One command and one owner-only final");
        Check(alice.TryPeek(out var answer) && answer.Count == 6 && answer["status"]!.ToString() == "Answered" &&
            answer["answer"]!.ToString() == "Заказ найден в выбранном филиале." &&
            answer["requestId"]!.ToString() == input["requestId"]!.ToString() &&
            answer["operationId"]!.ToString() == input["operationId"]!.ToString(), "Safe projected final contract");

        foreach (var field in new[] { "requestId", "eventId", "operationId" })
        {
            var reuse = Input(a, "00044");
            reuse[field] = input[field]!.DeepClone();
            h.Fixtures.Sign(reuse, a);
            Is(await Start(a, reuse), "Discarded", "Reserved identity with changed other IDs " + field);
        }
        var changed = Fixtures.Copy(input);
        changed["dataB64"] = Fixtures.B64("""{"text":"00044"}"""u8.ToArray());
        h.Fixtures.Sign(changed, a);
        Is(await Start(a, changed), "Discarded", "Same IDs with changed signed digest");

        var blocked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var sending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Transport.OnSend = async _ => { sending.SetResult(); await blocked.Task; };
        var first = Input(b, branch: "102");
        var rival = Input(b, branch: "102");
        var inFlight = Start(b, first);
        await sending.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var etag = (await h.Control(b))!.Etag;
        Is(await Start(b, rival), "Discarded", "Busy competing start");
        Is(await Start(b, rival), "Duplicate", "Busy receipt prevents later processing");
        Check((await h.Control(b))!.Etag == etag && bob.Count == 1 && bob.Last()["errorCode"]!.ToString() == "Busy",
            "Busy publishes once and preserves active ETag");
        var tool = Command(b, first);
        var bad = h.Fixtures.Result(tool, Fixtures.Found("42", "102"));
        Is(await Result(b, bad), "Discarded", "S4 wrong order number");
        bad = h.Fixtures.Result(tool, Fixtures.Found("00042", "102", "NotFiscalized"));
        Is(await Result(b, bad), "Discarded", "S4 invalid receipt relation");
        bad = h.Fixtures.Result(tool, new { outcome = "Maybe", reasonCode = "Success" });
        Is(await Result(b, bad), "Discarded", "Unknown machine outcome");
        bad = h.Fixtures.Result(tool, Fixtures.Missing);
        bad["expiresAt"] = Fixtures.Stamp(h.Clock.GetUtcNow().AddSeconds(119));
        Is(await Result(b, bad), "Discarded", "Changed result expiry");
        bad = h.Fixtures.Result(tool, Fixtures.Missing);
        bad["commandId"] = Fixtures.Id();
        h.Fixtures.SignResult(bad);
        Is(await Result(b, bad), "Discarded", "Changed command ID");
        bad = h.Fixtures.Result(tool, Fixtures.Missing);
        bad["dataB64"] = Fixtures.B64("""{"outcome":"NotFound","outcome":"Found","reasonCode":"OrderMissing"}"""u8.ToArray());
        h.Fixtures.SignResult(bad);
        Is(await Result(b, bad), "Discarded", "Strict result payload");
        var result = h.Fixtures.Result(tool, Fixtures.Missing);
        var wrongKind = Fixtures.Copy(result);
        wrongKind["kind"] = "turn.start";
        Is(await h.Invoke(b, wrongKind), "Discarded", "Result cannot become start");
        Is(await Result(a, result), "Discarded", "Cross-owner result");
        Check((await h.Control(b))!.Etag == etag, "Rejected results preserve wait");
        var resultWrites = h.Store.Writes.Count(w => w == "ResultClaimed");
        SynchronizeControlReads(b);
        var claims = await Task.WhenAll(Result(b, result), Result(b, h.Fixtures.Result(tool, Fixtures.Missing)));
        Check(claims.Count(status => status == "Processed") == 1 &&
            h.Store.Writes.Count(w => w == "ResultClaimed") == resultWrites + 1, "Race grants one result claim");
        blocked.SetResult();
        Is(await inFlight, "Processed", "Original handler still completes");
        h.Transport.OnSend = null;
        Check(bob.Count == 2 && alice.Count == 1 && bob.Last()["answer"]!.ToString() ==
            "Заказ не найден в выбранном филиале.", "NotFound final only for its owner");
        Is(await Start(b, rival), "Duplicate", "Busy request stays rejected when control becomes idle");

        var c = Scope("race");
        Is(await h.Prepare(c), "Processed", "Prepare simultaneous start scope");
        var before = h.Transport.Commands.Count;
        SynchronizeControlReads(c);
        var simultaneous = await Task.WhenAll(Start(c, Input(c)), Start(c, Input(c)));
        Check(simultaneous.Count(status => status == "Processed") == 1 && h.Transport.Commands.Count == before + 1,
            "Concurrent independent starts reserve IDs and Send only once");

        var completed = Scope("busy-after-terminal");
        Is(await h.Prepare(completed), "Processed", "Prepare completed-winner scenario");
        var staleRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Store.AfterRead = async (_, category, key, ct) =>
        {
            if (category != Category.Control || key != completed.Session) return;
            h.Store.AfterRead = null;
            staleRead.SetResult();
            await releaseRead.Task.WaitAsync(ct);
        };
        var losingInput = Input(completed);
        var losing = Start(completed, losingInput);
        await staleRead.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var winning = Input(completed);
        Is(await Start(completed, winning), "Processed", "Winner sends while other holds stale control");
        Is(await Result(completed, h.Fixtures.Result(Command(completed, winning), Fixtures.Missing)), "Processed",
            "Winner finishes before loser CAS/readback");
        var winnerEtag = (await h.Control(completed))!.Etag;
        var publications = h.Transport.Attempts.Count;
        releaseRead.SetResult();
        Is(await losing, "Discarded", "CAS loser after terminal winner");
        Check(h.Transport.Attempts.Count == publications + 1 &&
            h.Transport.Attempts.Last().Body["errorCode"]!.ToString() == "Busy" &&
            h.Transport.Attempts.Last().Body["requestId"]!.ToString() == losingInput["requestId"]!.ToString() &&
            (await h.Control(completed))!.Etag == winnerEtag, "Confirmed changed terminal admission yields Busy only for loser");
        h.Store.ConflictBeforeWrite = "InputClaimed";
        publications = h.Transport.Attempts.Count;
        Is(await Start(completed, Input(completed)), "Discarded", "Unchanged prior terminal is not competing admission");
        Check(h.Transport.Attempts.Count == publications, "No Busy for unchanged terminal");
    }

    private async Task HistoryAndFailures()
    {
        var a = h.Alice;
        var snapshots = new Snapshot();
        var before = await h.State(a);
        var item = await h.Store.Read(Harness.Caller(a), Category.Success, before.SnapshotRef!, default);
        var context = Snapshot.Read(await snapshots.Load(item!.Value, Harness.Caller(a), "101", default));
        Check(context.SuccessfulLookups == 1 && context.MafSessionId != a.Session, "MAF session identity and hydrated history");
        h.Clock.Advance(3600);
        var input = Input(a);
        Is(await Start(a, input), "Processed", "New scoped instance loads history after idle");
        Is(await Result(a, h.Fixtures.Result(Command(a, input), Fixtures.Missing)), "Processed", "Second lookup finishes");
        var after = await h.State(a);
        item = await h.Store.Read(Harness.Caller(a), Category.Success, after.SnapshotRef!, default);
        var hydrated = Snapshot.Read(await snapshots.Load(item!.Value, Harness.Caller(a), "101", default));
        Check(hydrated.SuccessfulLookups == 2 && hydrated.MafSessionId == context.MafSessionId &&
            after.HistoryDeleteAt == after.LastSuccessfulAt!.Value.AddDays(3), "Preserved MAF state and 3-day history");

        foreach (var failure in new[] { "missing", "version", "session-shape", "oversize", "foreign", "overflow" })
        {
            var scope = Scope(failure);
            Is(await h.Prepare(scope), "Processed", "Prepare history scenario");
            var start = Input(scope);
            Is(await Start(scope, start), "Processed", "Seed history");
            Is(await Result(scope, h.Fixtures.Result(Command(scope, start), Fixtures.Missing)), "Processed", "Save history");
            var active = await h.State(scope);
            var actor = Harness.Caller(scope);
            var key = active.SnapshotRef!;
            var stored = (await h.Store.Read(actor, Category.Success, key, default))!;
            if (failure == "missing") Check(await h.Store.Delete(actor, Category.Success, key, stored.Etag, default), "Remove snapshot");
            else
            {
                var corrupt = JsonNode.Parse(stored.Value)!.AsObject();
                if (failure == "version") corrupt["mafVersion"] = "0";
                if (failure == "session-shape") corrupt["serializedSession"] = 42;
                var session = await snapshots.Load(stored.Value, actor, "101", default);
                session.StateBag.SetValue("sdv-lookup-state", Snapshot.Read(session) with { SuccessfulLookups = int.MaxValue });
                var bytes = failure switch
                {
                    "version" or "session-shape" => Fixtures.Bytes(corrupt), "foreign" => item!.Value,
                    "overflow" => await snapshots.Save(session, default), _ => new byte[Snapshot.MaximumBytes + 1]
                };
                Check(await h.Store.Replace(actor, Category.Success, key, stored.Etag, bytes, default) is not null,
                    "Inject incompatible snapshot");
            }
            var count = h.Transport.Commands.Count;
            Is(await Start(scope, Input(scope)), "Processed", "Confirmed history error");
            Check(h.Transport.Commands.Count == count && (await h.State(scope)).Phase == Phase.ClosedFailed &&
                h.Transport.Attempts.Last().Body["errorCode"]!.ToString() == "HistoryUnavailable",
                "History " + failure + " never becomes an empty session");
        }

        var failed = Scope("failed");
        Is(await h.Prepare(failed), "Processed", "Prepare Failed outcome");
        var turn = Input(failed);
        Is(await Start(failed, turn), "Processed", "Send before known machine failure");
        Is(await Result(failed, h.Fixtures.Result(Command(failed, turn), Fixtures.Unknown)), "Processed", "S4 unknown API outcome");
        Check((await h.State(failed)).Phase == Phase.ClosedFailed &&
            h.Transport.Attempts.Last().Body["errorCode"]!.ToString() == "ApiOutcomeUnknown", "Failed closes session safely");
        Is(await Start(failed, Input(failed)), "Discarded", "Closed session cannot restart");
    }

    private async Task Ambiguity()
    {
        var partial = Scope("partial-prepare");
        Inject("Catalog", after: true);
        Is(await h.Prepare(partial), "500", "Lost catalog acknowledgment");
        Is(await h.Prepare(partial), "Discarded", "Partial prepare does not reopen same session");
        Is(await h.Invoke(partial, Input(partial)), "Discarded", "Partial prepare never allows raw start");

        var lostPrepare = Scope("lost-control-ack");
        Inject("IdleEmpty", after: true);
        Is(await h.Prepare(lostPrepare), "500", "Lost control-write acknowledgment");
        var requests = h.Requests;
        var beforeLostPrepare = h.Transport.Commands.Count;
        Is(await Start(lostPrepare, Input(lostPrepare)), "NotPrepared", "Client requires observed prepare ACK");
        Check(h.Requests == requests && h.Transport.Commands.Count == beforeLostPrepare &&
            (await h.State(lostPrepare)).Phase == Phase.IdleEmpty, "Lost prepare ACK never reaches start despite persisted control");

        foreach (var (stage, after) in new[] { ("Index", false), ("Index", true), ("Receipt", false), ("Receipt", true),
                     ("InputClaimed", false), ("InputClaimed", true), ("Pending", true), ("WaitingTool", false), ("WaitingTool", true) })
        {
            var scope = Scope("fault-" + stage + "-" + after);
            Is(await h.Prepare(scope), "Processed", "Prepare state fault scenario");
            var turn = Input(scope);
            var count = h.Transport.Commands.Count;
            Inject(stage, after);
            Is(await Start(scope, turn), "500", "Store fault " + stage + "/" + after);
            Check(h.Transport.Commands.Count == count, "No Send on unconfirmed state " + stage);
            var unadmitted = stage == "Index" && !after;
            Is(await Start(scope, turn), unadmitted ? "Processed" : "Duplicate", "Redelivery " + stage + "/" + after);
            Check(h.Transport.Commands.Count == count + (unadmitted ? 1 : 0), "Only first genuine admission may Send");
        }
        foreach (var (stage, after) in new[] { ("ResultClaimed", true), ("Success", true), ("IdleSuccess", true), ("IdleSuccess", false) })
        {
            var scope = Scope("result-fault-" + stage + "-" + after);
            Is(await h.Prepare(scope), "Processed", "Prepare result fault");
            var turn = Input(scope);
            Is(await Start(scope, turn), "Processed", "Send for result fault");
            var result = h.Fixtures.Result(Command(scope, turn), Fixtures.Missing);
            var publications = h.Transport.Attempts.Count;
            Inject(stage, after);
            Is(await Result(scope, result), "500", "Lost result write " + stage);
            Is(await Result(scope, result), "Duplicate", "Result fault cannot resume " + stage);
            Check(h.Transport.Attempts.Count == publications, "No final after unconfirmed " + stage);
        }
        var lost = Scope("lost-pubsub");
        var queue = h.Transport.Subscribe(lost).Received;
        Is(await h.Prepare(lost), "Processed", "Prepare lost PubSub");
        var input = Input(lost);
        Is(await Start(lost, input), "Processed", "Waiting before lost PubSub");
        var final = h.Fixtures.Result(Command(lost, input), Fixtures.Missing);
        var attempts = h.Transport.Attempts.Count;
        h.Transport.PublicationFailure = new IOException("Synthetic lost delivery.");
        Is(await Result(lost, final), "Processed", "Terminal result after one publish attempt");
        h.Transport.PublicationFailure = null;
        Is(await Result(lost, final), "Duplicate", "Lost delivery never triggers replay");
        Check(h.Transport.Attempts.Count == attempts + 1 && queue.IsEmpty, "Publication loss stays lost");

        var conflict = Scope("lost-input-cas");
        Is(await h.Prepare(conflict), "Processed", "Prepare ambiguous CAS readback");
        var ambiguousInput = Input(conflict);
        var sends = h.Transport.Commands.Count;
        var notices = h.Transport.Attempts.Count;
        h.Store.LoseAsConflict = "InputClaimed";
        Is(await Start(conflict, ambiguousInput), "Discarded", "Ambiguous own claim is never Busy");
        Is(await Start(conflict, ambiguousInput), "Duplicate", "Own persisted claim blocks redelivery");
        Check(h.Transport.Commands.Count == sends && h.Transport.Attempts.Count == notices, "Own ambiguous claim has no effect");

        var delayed = Scope("ambiguous-send-then-result");
        Is(await h.Prepare(delayed), "Processed", "Prepare ambiguous Send");
        var delayedInput = Input(delayed);
        h.Transport.OnSend = _ => throw new IOException("Synthetic missing Send acknowledgment.");
        Is(await Start(delayed, delayedInput), "Processed", "ACK preserves wait after ambiguous Send");
        h.Transport.OnSend = null;
        Check((await h.State(delayed)).Phase == Phase.WaitingTool, "Ambiguous Send leaves authoritative wait");
        Is(await Start(delayed, delayedInput), "Duplicate", "Ambiguous Send redelivery");
        Is(await Result(delayed, h.Fixtures.Result(Command(delayed, delayedInput), Fixtures.Missing)), "Processed",
            "Exact timely result after ambiguous Send ACK");
        Check(h.Transport.Commands.Count == sends + 1 && h.Transport.Attempts.Count == notices + 1,
            "One Send and final after ambiguity");

        var damaged = Scope("bad-store");
        Is(await h.Prepare(damaged), "Processed", "Prepare corruption test");
        var actor = Harness.Caller(damaged);
        var original = (await h.Control(damaged))!.Value;
        foreach (var phase in new JsonNode?[] { null, JsonValue.Create(99) })
        {
            var corrupt = JsonNode.Parse(original)!.AsObject();
            if (phase is null) corrupt.Remove("phase");
            else corrupt["phase"] = phase;
            h.Store.Corrupt(actor, Category.Control, damaged.Session, Fixtures.Bytes(corrupt));
            var writes = h.Store.Writes.Count;
            Is(await Start(damaged, Input(damaged)), "500", "Corrupt or missing phase has no ACK");
            Check(h.Store.Writes.Count == writes, "Corrupt control makes no admission writes");
        }
        h.Store.Corrupt(actor, Category.Control, damaged.Session, original);
        var record = new Admission(actor.Owner, actor.Session, "101", Fixtures.Id(), Fixtures.Id(), Fixtures.Id(),
            new string('1', 64), h.Clock.GetUtcNow().AddSeconds(120), h.Clock.GetUtcNow().AddSeconds(125));
        var index = JsonNode.Parse(Wire.Encode(new[] { record }))!.AsArray();
        index[0]!.AsObject().Remove("protectUntil");
        h.Store.Corrupt(actor, Category.Receipts, damaged.Session + "/admissions", Fixtures.Bytes(index));
        var stateWrites = h.Store.Writes.Count;
        Is(await Start(damaged, Input(damaged)), "500", "Missing index protectUntil has no ACK");
        Check(h.Store.Writes.Count == stateWrites && h.Transport.Commands.Count == sends + 1, "Corrupt index grants no effects");

        var transient = Scope("internal-transient");
        Is(await h.Prepare(transient), "Processed", "Prepare transient internal test");
        var retry = Input(transient);
        h.Store.AfterRead = (_, category, key, _) =>
        {
            if (category == Category.Control && key == transient.Session)
            {
                h.Store.AfterRead = null;
                throw new InvalidOperationException("Synthetic internal store exception.");
            }
            return Task.CompletedTask;
        };
        Is(await Start(transient, retry), "500", "Internal InvalidOperationException has no wire ACK");
        Is(await Start(transient, retry), "Processed", "First admission still available after internal read fault");
    }

    private async Task DeadlineAndCleanup()
    {
        var scope = Scope("deadline");
        Is(await h.Prepare(scope), "Processed", "Prepare deadline test");
        var turn = Input(scope);
        Is(await Start(scope, turn), "Processed", "Wait on original deadline");
        var command = Command(scope, turn);
        var actor = Harness.Caller(scope);
        var control = (await h.Control(scope))!;
        var state = await h.State(scope);
        var index = scope.Session + "/admissions";
        h.Clock.Advance(120);
        Is(await Result(scope, h.Fixtures.Result(command, Fixtures.Missing)), "Discarded", "Result exactly at deadline");
        Check(state.ProtectUntil == h.Clock.GetUtcNow().AddSeconds(5) &&
            await h.Store.Read(actor, Category.Receipts, index, default) is not null, "Marker retained through clock skew");
        var stale = control.Etag;
        Check(await h.Store.Replace(actor, Category.Control, scope.Session, stale, control.Value, default) is not null &&
            !await h.Store.Delete(actor, Category.Control, scope.Session, stale, default), "Cleaner stale ETag cannot remove writer");
        h.Clock.Advance(5);
        control = (await h.Control(scope))!;
        Check(await h.Store.Delete(actor, Category.Control, scope.Session, control.Etag, default), "Concrete ETag removes expired control");
        Is(await Start(scope, turn), "Discarded", "Expired start after control deletion");
        Is(await Result(scope, h.Fixtures.Result(command, Fixtures.Missing)), "Discarded", "Late result after control deletion");
        Is(await Start(scope, Input(scope)), "Discarded", "Fresh start cannot recreate missing control");
        Is(await h.Prepare(scope), "Discarded", "Existing catalog prevents prepare of deleted session");

        var reserve = Scope("reserve");
        Is(await h.Prepare(reserve), "Processed", "Prepare reserve boundary");
        var shortBudget = h.Fixtures.Input(reserve, Ingress.Start, clientSent: h.Clock.GetUtcNow().AddSeconds(-105));
        var before = h.Transport.Commands.Count;
        Is(await Start(reserve, shortBudget), "Processed", "Terminal insufficient budget");
        Check(h.Transport.Commands.Count == before && (await h.State(reserve)).Phase == Phase.ClosedFailed,
            "Exactly 15 seconds remaining does not Send");

        var shortWait = Scope("short-confirmed-wait");
        Is(await h.Prepare(shortWait), "Processed", "Prepare confirmed wait budget test");
        h.Store.Fault = (stage, after) =>
        {
            if (stage == "WaitingTool" && after) h.Clock.Advance(105);
        };
        Is(await Start(shortWait, Input(shortWait)), "Processed", "Known insufficient budget closes confirmed wait");
        h.Store.Fault = null;
        Check(h.Transport.Commands.Count == before && (await h.State(shortWait)).Phase == Phase.ClosedFailed &&
            h.Transport.Attempts.Last().Body["errorCode"]!.ToString() == "InsufficientBudget",
            "No Send attempted at 15-second waiting boundary");

        var time = Scope("post-cas-deadline");
        Is(await h.Prepare(time), "Processed", "Prepare elapsed waiting-save test");
        var input = Input(time);
        h.Store.Fault = (stage, after) =>
        {
            if (stage == "WaitingTool" && after) h.Clock.Advance(121);
        };
        Is(await Start(time, input), "Processed", "Wait confirmed with deadline exhausted");
        h.Store.Fault = null;
        Check(h.Transport.Commands.Count == before && (await h.State(time)).Phase == Phase.WaitingTool,
            "Deadline exhausted after wait never reaches Send");

        var capacity = Scope("index-capacity");
        Is(await h.Prepare(capacity), "Processed", "Prepare index capacity scenario");
        var now = h.Clock.GetUtcNow();
        var reserved = Enumerable.Range(0, 128).Select(_ => new Admission(capacity.Owner, capacity.Session, "101",
            Fixtures.Id(), Fixtures.Id(), Fixtures.Id(), new string('1', 64), now.AddSeconds(2), now.AddSeconds(7))).ToArray();
        Check(await h.Store.Create(Harness.Caller(capacity), Category.Receipts, capacity.Session + "/admissions",
            Wire.Encode(reserved), default) is not null, "Seed protected admissions");
        var eligible = Input(capacity);
        h.Clock.Advance(6);
        Is(await Start(capacity, eligible), "500", "Full index fails closed through protectUntil");
        Check(h.Transport.Commands.Count == before, "Capacity failure never Sends");
        h.Clock.Advance(1);
        Is(await Start(capacity, eligible), "Processed", "First admission permitted after safe prune");
        Check(h.Transport.Commands.Count == before + 1, "Safe pruning leaves one Send");

        var expiredFinal = Scope("post-terminal-expiry");
        Is(await h.Prepare(expiredFinal), "Processed", "Prepare post-terminal expiry");
        var next = Input(expiredFinal);
        Is(await Start(expiredFinal, next), "Processed", "Send for post-terminal expiry");
        var result = h.Fixtures.Result(Command(expiredFinal, next), Fixtures.Missing);
        var publishes = h.Transport.Attempts.Count;
        h.Store.Fault = (stage, after) =>
        {
            if (stage == "IdleSuccess" && after) h.Clock.Advance(120);
        };
        Is(await Result(expiredFinal, result), "Processed", "Terminal state persists at publication deadline");
        h.Store.Fault = null;
        Check(h.Transport.Attempts.Count == publishes && (await h.State(expiredFinal)).Phase == Phase.IdleSuccess,
            "Expired final not attempted");

        var canceled = Scope("state-cancellation");
        Is(await h.Prepare(canceled), "Processed", "Prepare state cancellation");
        var storeCanceled = false;
        h.Store.AfterRead = async (_, category, key, ct) =>
        {
            if (category != Category.Control || key != canceled.Session) return;
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            catch (OperationCanceledException) { storeCanceled = true; throw; }
        };
        var nearDeadline = h.Fixtures.Input(canceled, Ingress.Start, clientSent: h.Clock.GetUtcNow().AddSeconds(-119));
        var writesBefore = h.Store.Writes.Count;
        Is(await Start(canceled, nearDeadline), "504", "Validated expiry cancels state I/O");
        h.Store.AfterRead = null;
        Check(storeCanceled && h.Store.Writes.Count == writesBefore, "Canceled state operation grants no writes");
    }

    private async Task SubscriberValidation()
    {
        var scope = Scope("subscriber-filter");
        Is(await h.Prepare(scope), "Processed", "Prepare subscriber contract scenario");
        var subscriber = h.Transport.Subscribe(scope);
        var input = Input(scope);
        Is(await Start(scope, input), "Processed", "Client registers expected IDs and deadline before invocation");
        var final = new JsonObject
        {
            ["schemaVersion"] = 1, ["kind"] = "turn.final", ["requestId"] = input["requestId"]!.DeepClone(),
            ["operationId"] = input["operationId"]!.DeepClone(), ["status"] = "Answered", ["answer"] = "Synthetic answer"
        };
        var foreign = Fixtures.Copy(final);
        foreign["operationId"] = Fixtures.Id();
        Check(!subscriber.Receive(foreign), "Subscriber rejects foreign correlation");
        var malformed = Fixtures.Copy(final);
        malformed["errorCode"] = "Busy";
        Check(!subscriber.Receive(malformed), "Subscriber rejects extra final field");
        malformed = Fixtures.Copy(final);
        malformed["status"] = "Failed";
        Check(!subscriber.Receive(malformed), "Subscriber rejects wrong field for Failed");
        malformed = Fixtures.Copy(final);
        malformed["schemaVersion"] = JsonNode.Parse("1.0");
        Check(!subscriber.Receive(malformed), "Subscriber rejects noninteger schema version");
        Check(subscriber.Receive(final) && !subscriber.Receive(final), "Subscriber accepts one exact final per expected IDs");
        Is(await Start(scope, input), "Duplicate", "Replayed Start cannot reset subscriber consumption");
        Check(!subscriber.Receive(final), "Replayed Start cannot renew consumed final");

        var next = Input(scope);
        subscriber.Expect(next);
        var late = Fixtures.Copy(final);
        late["requestId"] = next["requestId"]!.DeepClone();
        late["operationId"] = next["operationId"]!.DeepClone();
        h.Clock.Advance(120);
        Check(!subscriber.Receive(late) && subscriber.Received.Count == 1, "Subscriber rejects final at original expiry");
    }

    private void SynchronizeControlReads(Scope scope)
    {
        var arrivals = 0;
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        h.Store.AfterRead = async (caller, category, key, ct) =>
        {
            if (category != Category.Control || caller.Owner != scope.Owner || key != scope.Session) return;
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                h.Store.AfterRead = null;
                both.SetResult();
            }
            await both.Task.WaitAsync(TimeSpan.FromSeconds(10), ct);
        };
    }

    private void Inject(string expected, bool after) =>
        h.Store.Fault = (stage, written) =>
        {
            if (stage != expected || written != after) return;
            h.Store.Fault = null;
            throw new IOException("Synthetic write fault.");
        };
}
