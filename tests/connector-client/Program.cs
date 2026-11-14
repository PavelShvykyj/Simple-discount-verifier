using Azure.Identity;
using Azure.Messaging.ServiceBus;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

// Independent wire producer/verifier; deliberately no references to the Connector or API projects.
return await Client.Run(args);

static class Client
{
    const string Namespace = "sb-sdv-agent-develop-1978.servicebus.windows.net";
    static readonly string[] Fields = ["schemaVersion", "kind", "eventId", "requestId", "operationId", "branchId", "issuedAt", "expiresAt", "route", "dataB64", "commandId", "toolCallId", "commandType"];
    static readonly UTF8Encoding Utf8 = new(false, true);
    static string Text(JsonElement v) => v.ValueKind == JsonValueKind.String ? v.GetString()! : throw new InvalidDataException();
    static bool Match(string value, string pattern) => Regex.IsMatch(value, "\\A" + pattern + "\\z", RegexOptions.CultureInvariant);
    static void Shape(JsonElement value, string[] required, params string[] optional)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException();
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var p in value.EnumerateObject()) if (!found.Add(p.Name) || p.Value.ValueKind == JsonValueKind.Null || (!required.Contains(p.Name) && !optional.Contains(p.Name))) throw new InvalidDataException();
        if (required.Any(x => !found.Contains(x))) throw new InvalidDataException();
    }
    static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static byte[] Decode(string value)
    {
        if (!Match(value, "[A-Za-z0-9_-]+")) throw new InvalidDataException();
        var bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
        if (B64(bytes) != value) throw new InvalidDataException(); return bytes;
    }
    static JsonElement Parse(byte[] bytes)
    {
        if (bytes.Length > 65536 || bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 })) throw new InvalidDataException();
        _ = Utf8.GetString(bytes); using var doc = JsonDocument.Parse(bytes); return doc.RootElement.Clone();
    }
    static string Stamp(DateTimeOffset time) => time.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    static DateTimeOffset Time(string text)
    {
        if (!Match(text, "[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]{1,7})?Z")) throw new InvalidDataException();
        return DateTimeOffset.Parse(text, CultureInfo.InvariantCulture);
    }
    static JsonObject Command(string branch, string number, RSA key, bool badSignature = false, bool hint = false)
    {
        var id = Guid.NewGuid().ToString("D"); var now = DateTimeOffset.UtcNow;
        var data = new JsonObject { ["orderNumber"] = number };
        if (hint) data["amountHint"] = new JsonObject { ["value"] = "00001.00", ["currency"] = "UAH" };
        var bytes = Utf8.GetBytes(data.ToJsonString());
        var result = new JsonObject { ["schemaVersion"] = 1, ["kind"] = "tool.command", ["eventId"] = id, ["requestId"] = Guid.NewGuid().ToString("D"), ["operationId"] = Guid.NewGuid().ToString("D"), ["commandId"] = id, ["toolCallId"] = "synthetic-tool-" + Guid.NewGuid().ToString("N"), ["commandType"] = "LookupOrder", ["branchId"] = branch, ["issuedAt"] = Stamp(now), ["expiresAt"] = Stamp(now.AddSeconds(120)), ["dataB64"] = B64(bytes) };
        var claims = new JsonObject { ["v"] = 1, ["iss"] = "sdv-backend", ["aud"] = "sdv-hosted-tool-result", ["userKey"] = "sdv:user:synthetic", ["agentSessionId"] = "synthetic-session", ["commandDataHash"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() };
        foreach (var field in new[] { "requestId", "operationId", "branchId", "commandId", "toolCallId", "commandType", "issuedAt", "expiresAt" }) claims[field] = result[field]!.DeepClone();
        var signing = B64(Utf8.GetBytes("{\"alg\":\"PS256\",\"kid\":\"disposable-test\",\"v\":1}")) + "." + B64(Utf8.GetBytes(claims.ToJsonString()));
        var signature = key.SignData(Encoding.ASCII.GetBytes(signing), HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        if (badSignature) signature[0] ^= 1;
        result["route"] = signing + "." + B64(signature); return result;
    }
    static bool Signature(string route, RSA key)
    { var segments = route.Split('.'); return segments.Length == 3 && key.VerifyData(Encoding.ASCII.GetBytes(segments[0] + "." + segments[1]), Decode(segments[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pss); }
    static string Validate(byte[] bytes, string? mime, string? messageId, string? correlation, JsonObject command, string number, string expectedReason, RSA key, bool signatureValid)
    {
        var root = Parse(bytes); Shape(root, Fields);
        if (mime != "application/json" || root.GetProperty("schemaVersion").GetRawText() != "1" || Text(root.GetProperty("kind")) != "tool.result") throw new InvalidDataException();
        var eventId = Text(root.GetProperty("eventId"));
        if (eventId.Length != 36 || !Guid.TryParseExact(eventId, "D", out _) || eventId == command["eventId"]!.GetValue<string>() || messageId != eventId || correlation != command["commandId"]!.GetValue<string>()) throw new InvalidDataException();
        foreach (var field in Fields.Except(["kind", "eventId", "issuedAt", "dataB64"])) if (root.GetProperty(field).GetRawText() != JsonSerializer.Serialize(command[field])) throw new InvalidDataException();
        var issued = Time(Text(root.GetProperty("issuedAt"))); var expiry = Time(Text(root.GetProperty("expiresAt")));
        if (issued >= expiry || issued > DateTimeOffset.UtcNow.AddSeconds(5) || DateTimeOffset.UtcNow >= expiry) throw new InvalidDataException();
        if (Signature(Text(root.GetProperty("route")), key) != signatureValid) throw new InvalidDataException();
        var data = Parse(Decode(Text(root.GetProperty("dataB64")))); Shape(data, ["outcome", "reasonCode"], "order");
        var outcome = Text(data.GetProperty("outcome")); var reason = Text(data.GetProperty("reasonCode"));
        if (reason != expectedReason) throw new InvalidDataException();
        if (reason == "Success")
        {
            if (outcome != "Found") throw new InvalidDataException();
            var order = data.GetProperty("order"); Shape(order, ["orderNumber", "orderStateCode", "receiptState", "amount"], "receiptNumber");
            var branch = command["branchId"]!.GetValue<string>(); var fixture = (branch, number) switch
            {
                ("101", "00042") => ("Open", "NotFiscalized", "123.45", (string?)null),
                ("101", "00043") => ("Closed", "Fiscalized", "230.00", "TEST-101-00043"),
                ("102", "00042") => ("Closed", "Fiscalized", "987.65", "TEST-102-00042"),
                ("102", "00044") => ("Open", "Unknown", "50.00", (string?)null),
                _ => throw new InvalidDataException()
            };
            if (Text(order.GetProperty("orderNumber")) != number || Text(order.GetProperty("orderStateCode")) != fixture.Item1 || Text(order.GetProperty("receiptState")) != fixture.Item2 || order.TryGetProperty("receiptNumber", out var receipt) != (fixture.Item4 is not null) || (fixture.Item4 is not null && Text(receipt) != fixture.Item4)) throw new InvalidDataException();
            var amount = order.GetProperty("amount"); Shape(amount, ["value", "currency"]);
            if (Text(amount.GetProperty("value")) != fixture.Item3 || Text(amount.GetProperty("currency")) != "UAH") throw new InvalidDataException();
        }
        else if (reason == "OrderMissing") { if (outcome != "NotFound" || data.TryGetProperty("order", out _)) throw new InvalidDataException(); }
        else if (outcome != "Failed" || reason is not ("AccessDenied" or "Expired" or "InvalidArguments" or "ApiUnavailable" or "ApiOutcomeUnknown") || data.TryGetProperty("order", out _)) throw new InvalidDataException();
        return outcome + "/" + reason;
    }
    internal static async Task<int> Run(string[] args)
    {
        try
        {
            using var key = RSA.Create(2048);
            if (args.SequenceEqual(["--self-test"])) { SelfTest(key); return 0; }
            if (args.Length != 4 || args[0] != "--live-exclusive-window" || args[1] != "--evidence" || args[3] != "--approved-exchange") throw new InvalidDataException();
            var evidence = Path.GetFullPath(args[2]);
            var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            if (!evidence.StartsWith(Path.Combine(repo, "agent-extension", ".local") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || (Directory.Exists(evidence) && Directory.EnumerateFileSystemEntries(evidence).Any())) throw new InvalidDataException();
            Directory.CreateDirectory(evidence);
            var options = new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpTcp, RetryOptions = new ServiceBusRetryOptions { MaxRetries = 0 } };
            await using var bus = new ServiceBusClient(Namespace, new AzureCliCredential(), options);
            await using var receiver = bus.CreateReceiver("results", new ServiceBusReceiverOptions { ReceiveMode = ServiceBusReceiveMode.PeekLock, PrefetchCount = 0 });
            var observations = new List<object>();
            foreach (var branch in new[] { "101", "102" })
            {
                await using var sender = bus.CreateSender("commands" + branch);
                foreach (var number in branch == "101" ? new[] { "00042", "00043", "99999", "42" } : new[] { "00042", "00044", "99999", "42" })
                foreach (var variation in new[] { "normal", "hint", "bad-signature" })
                {
                    var command = Command(branch, number, key, variation == "bad-signature", variation == "hint");
                    var bytes = Utf8.GetBytes(command.ToJsonString()); var id = command["commandId"]!.GetValue<string>();
                    // This manifest contains only synthetic IDs/hashes; it is never a source of replay.
                    File.AppendAllText(Path.Combine(evidence, "sent-ids.jsonl"), JsonSerializer.Serialize(new { branch, commandId = id, expiresAt = command["expiresAt"]!.GetValue<string>() }) + "\n");
                    var remaining = Time(command["expiresAt"]!.GetValue<string>()) - DateTimeOffset.UtcNow;
                    if (remaining <= TimeSpan.Zero) throw new InvalidDataException();
                    using var deadline = new CancellationTokenSource(remaining);
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    var ttl = Time(command["expiresAt"]!.GetValue<string>()) - DateTimeOffset.UtcNow;
                    if (ttl <= TimeSpan.Zero) throw new InvalidDataException();
                    await sender.SendMessageAsync(new ServiceBusMessage(new BinaryData(bytes)) { MessageId = id, CorrelationId = id, ContentType = "application/json", TimeToLive = ttl < TimeSpan.FromSeconds(120) ? ttl : TimeSpan.FromSeconds(120) }, deadline.Token);
                    ServiceBusReceivedMessage? result = null;
                    while (result is null) result = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(5), deadline.Token);
                    if (result.CorrelationId != id)
                    {
                        await receiver.AbandonMessageAsync(result, cancellationToken: deadline.Token);
                        Console.Error.WriteLine("ExclusiveWindowConflict; foreign result released; no body displayed."); return 1;
                    }
                    try
                    {
                        var outcome = Validate(result.Body.ToArray(), result.ContentType, result.MessageId, result.CorrelationId, command, number, number is "99999" or "42" ? "OrderMissing" : "Success", key, variation != "bad-signature");
                        await receiver.CompleteMessageAsync(result, deadline.Token);
                        observations.Add(new { branch, commandId = id, variation, outcome, elapsedMs = timer.ElapsedMilliseconds, commandSendAttempts = 1, routeEchoHash = Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(command["route"]!.GetValue<string>()))), independentApiReadCount = "NotObservedByClient" });
                        File.WriteAllText(Path.Combine(evidence, "results.json"), JsonSerializer.Serialize(observations, new JsonSerializerOptions { WriteIndented = true }));
                    }
                    catch { await receiver.AbandonMessageAsync(result, cancellationToken: deadline.Token); throw; }
                }
            }
            Console.WriteLine("PASS real-queue wire fixture exchange only; API attempt/read counters and SYSTEM/service/negative rights/Telegram/crash checks require separate evidence."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine("ConnectorClientFailed type=" + e.GetType().Name + "; no automatic retry or queue cleanup performed."); return 1; }
    }
    static void SelfTest(RSA key)
    {
        var command = Command("101", "00042", key); var route = command["route"]!.GetValue<string>();
        if (!Signature(route, key) || Signature(Command("101", "00042", key, true)["route"]!.GetValue<string>(), key)) throw new InvalidDataException();
        var result = (JsonObject)command.DeepClone(); var eventId = Guid.NewGuid().ToString("D"); result["kind"] = "tool.result"; result["eventId"] = eventId;
        result["dataB64"] = B64(Utf8.GetBytes("{\"outcome\":\"Found\",\"reasonCode\":\"Success\",\"order\":{\"orderNumber\":\"00042\",\"orderStateCode\":\"Open\",\"receiptState\":\"NotFiscalized\",\"amount\":{\"value\":\"123.45\",\"currency\":\"UAH\"}}}"));
        var correlation = command["commandId"]!.GetValue<string>();
        if (Validate(Utf8.GetBytes(result.ToJsonString()), "application/json", eventId, correlation, command, "00042", "Success", key, true) != "Found/Success") throw new InvalidDataException();
        var rejected = 0;
        foreach (var name in Fields)
        {
            var bad = (JsonObject)result.DeepClone(); bad.Remove(name);
            try { Validate(Utf8.GetBytes(bad.ToJsonString()), "application/json", eventId, correlation, command, "00042", "Success", key, true); } catch { rejected++; }
        }
        if (rejected != Fields.Length) throw new InvalidDataException();
        Console.WriteLine("PASS independent Connector client: PS256 disposable key/signature rejection, exact wire fixture and13 required fields; no credentials or network accessed.");
    }
}
