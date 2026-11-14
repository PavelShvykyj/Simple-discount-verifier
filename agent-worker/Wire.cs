using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sdv.Worker;

internal enum Ingress { Prepare, Start, Result }
internal enum Disposition { Processed, Duplicate, Discarded }
internal sealed record Caller(string Owner, string Session, string CallId, Ingress Source);
internal sealed record Envelope(string EventId, string RequestId, string OperationId, string Branch,
    DateTimeOffset Issued, DateTimeOffset Expiry, string ExpiryText, string Route, string Hash,
    string? CommandId, string? ToolCallId, JsonElement Data);
internal sealed record Command(string Id, string ToolId, string Route, string Hash, byte[] Body);
internal sealed record Outcome(string? Answer, string? ErrorCode);

internal sealed class Wire(IReadOnlyDictionary<string, RSA> testKeys, string? backendKid = null, RSA? backendKey = null)
{
    internal const int Limit = 64 * 1024;
    private static readonly UTF8Encoding Utf8 = new(false, true);
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true
    };
    private static readonly string[] Common = ["schemaVersion", "kind", "eventId", "requestId", "operationId",
        "branchId", "issuedAt", "expiresAt", "route", "dataB64"];
    private static readonly string[] Claims = ["v", "iss", "aud", "userKey", "agentSessionId", "branchId",
        "requestId", "operationId", "issuedAt", "expiresAt"];
    private static readonly string[] Tool = ["commandId", "toolCallId", "commandType"];

    internal Envelope Read(JsonElement body, Caller caller, DateTimeOffset now)
    {
        if (caller.Source != Ingress.Prepare) Require(backendKey is not null);
        var result = caller.Source == Ingress.Result;
        Shape(body, result ? [.. Common, .. Tool] : Common);
        Require(body.GetProperty("schemaVersion").GetRawText() == "1");
        Require(Text(body, "kind") == caller.Source switch
        {
            Ingress.Prepare => "session.prepare",
            Ingress.Start => "turn.start",
            _ => "tool.result"
        });

        var eventId = Uuid(body, "eventId");
        var requestId = Uuid(body, "requestId");
        var operationId = Uuid(body, "operationId");
        var branch = Text(body, "branchId");
        Require(branch is "101" or "102");
        var issuedText = Text(body, "issuedAt");
        var expiryText = Text(body, "expiresAt");
        var issued = Time(issuedText);
        var expiry = Time(expiryText);
        Window(issued, expiry, now);
        if (!result) Require(expiry - issued <= TimeSpan.FromSeconds(120));
        var route = Text(body, "route");
        var claims = Verify(route, result);
        var audience = caller.Source switch
        {
            Ingress.Prepare => "sdv-hosted-prepare",
            Ingress.Start => "sdv-hosted-start",
            _ => "sdv-hosted-tool-result"
        };
        var hashField = result ? "commandDataHash" : "dataHash";
        Shape(claims, result ? [.. Claims, .. Tool, hashField] : [.. Claims, hashField]);
        Require(claims.GetProperty("v").GetRawText() == "1" &&
            Text(claims, "iss") == (result ? "sdv-backend" : "sdv-worker-test") &&
            Text(claims, "aud") == audience && Text(claims, "userKey") == caller.Owner &&
            Text(claims, "agentSessionId") == caller.Session);
        foreach (var field in new[] { "branchId", "requestId", "operationId", "expiresAt" })
            Require(Text(claims, field) == Text(body, field));

        var dataBytes = Decode(Text(body, "dataB64"));
        var hash = Text(claims, hashField);
        string? commandId = null, toolId = null;
        if (result)
        {
            commandId = Uuid(body, "commandId");
            toolId = Text(body, "toolCallId");
            Require(eventId != commandId && IsId(toolId) && Text(body, "commandType") == "LookupOrder");
            foreach (var field in Tool) Require(Text(claims, field) == Text(body, field));
            var commandIssued = Time(Text(claims, "issuedAt"));
            Window(commandIssued, expiry, now);
            Require(expiry - commandIssued <= TimeSpan.FromSeconds(120) && Match(hash, "[0-9a-f]{64}"));
        }
        else
        {
            Require(Text(claims, "issuedAt") == issuedText && hash == Hash(dataBytes));
        }

        return new(eventId, requestId, operationId, branch, issued, expiry, expiryText, route, hash,
            commandId, toolId, Parse(dataBytes));
    }

    internal Command CreateCommand(Caller caller, Envelope start, string number, DateTimeOffset now)
    {
        var id = Guid.NewGuid().ToString("D");
        var tool = Guid.NewGuid().ToString("D");
        var earliest = start.Expiry.AddSeconds(-120);
        var issued = Stamp(now < earliest ? earliest : now);
        var data = Encode(new { orderNumber = number });
        var hash = Hash(data);
        var route = Sign(Encode(new
        {
            v = 1, iss = "sdv-backend", aud = "sdv-hosted-tool-result",
            userKey = caller.Owner, agentSessionId = caller.Session, branchId = start.Branch,
            start.RequestId, start.OperationId, commandId = id, toolCallId = tool, commandType = "LookupOrder",
            issuedAt = issued, expiresAt = start.ExpiryText, commandDataHash = hash
        }));
        var body = Encode(new
        {
            schemaVersion = 1, kind = "tool.command", eventId = id, start.RequestId, start.OperationId,
            branchId = start.Branch, issuedAt = issued, expiresAt = start.ExpiryText, route,
            dataB64 = B64(data), commandId = id, toolCallId = tool, commandType = "LookupOrder"
        });
        Require(body.Length <= Limit);
        return new(id, tool, route, hash, body);
    }

    internal static Outcome ReadOutcome(JsonElement data, string number)
    {
        Shape(data, ["outcome", "reasonCode"], "order");
        var outcome = Text(data, "outcome");
        var reason = Text(data, "reasonCode");
        var hasOrder = data.TryGetProperty("order", out var order);
        if (outcome == "Found" && reason == "Success" && hasOrder)
        {
            Shape(order, ["orderNumber", "orderStateCode", "receiptState", "amount"], "receiptNumber");
            var receipt = Text(order, "receiptState");
            var hasReceipt = order.TryGetProperty("receiptNumber", out _);
            Require(Text(order, "orderNumber") == number && Text(order, "orderStateCode") is "Open" or "Closed" &&
                receipt is "NotFiscalized" or "Fiscalized" or "Unknown" && hasReceipt == (receipt == "Fiscalized"));
            if (hasReceipt) Require(Match(Text(order, "receiptNumber"), "TEST-(101|102)-[0-9]+"));
            var amount = order.GetProperty("amount");
            Shape(amount, ["value", "currency"]);
            Require(Match(Text(amount, "value"), "[0-9]{1,12}\\.[0-9]{2}") && Text(amount, "currency") == "UAH");
            return new("Заказ найден в выбранном филиале.", null);
        }
        if (!hasOrder && outcome == "NotFound" && reason == "OrderMissing")
            return new("Заказ не найден в выбранном филиале.", null);
        Require(!hasOrder && outcome == "Failed" && reason is
            "AccessDenied" or "Expired" or "InvalidArguments" or "ApiUnavailable" or "ApiOutcomeUnknown");
        return new(null, reason);
    }

    private JsonElement Verify(string route, bool result)
    {
        var segments = route.Split('.');
        Require(segments.Length == 3);
        var header = Parse(Decode(segments[0]));
        Shape(header, ["alg", "kid"]);
        Require(Text(header, "alg") == "PS256");
        var kid = Text(header, "kid");
        var key = result ? kid == backendKid ? backendKey : null : testKeys.GetValueOrDefault(kid);
        if (key is not { KeySize: >= 2048 }) throw new InvalidDataException();
        var input = Encoding.ASCII.GetBytes(segments[0] + "." + segments[1]);
        var signature = Decode(segments[2]);
        lock (key) Require(key.VerifyData(input, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        return Parse(Decode(segments[1]));
    }

    private string Sign(byte[] claims)
    {
        var signer = backendKey ?? throw new InvalidOperationException("Backend signing key unavailable.");
        var input = B64(Encode(new { alg = "PS256", kid = backendKid })) + "." + B64(claims);
        lock (signer)
            return input + "." + B64(signer.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss));
    }

    internal static JsonElement Parse(byte[] bytes)
    {
        Require(bytes.Length <= Limit && !bytes.AsSpan().StartsWith(new byte[] { 239, 187, 191 }));
        _ = Utf8.GetCharCount(bytes);
        using var doc = JsonDocument.Parse(bytes);
        return doc.RootElement.Clone();
    }

    internal static void Shape(JsonElement element, string[] required, params string[] optional)
    {
        Require(element.ValueKind == JsonValueKind.Object);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            Require(seen.Add(property.Name) && property.Value.ValueKind != JsonValueKind.Null &&
                (required.Contains(property.Name) || optional.Contains(property.Name)));
        Require(required.All(seen.Contains));
    }

    internal static string Text(JsonElement element, string name) =>
        element.GetProperty(name).GetString() ?? throw new InvalidDataException();
    internal static string Uuid(JsonElement element, string name)
    {
        var value = Text(element, name);
        Require(value.Length == 36 && Guid.TryParseExact(value, "D", out _));
        return value;
    }

    internal static DateTimeOffset Time(string value)
    {
        if (!Match(value, "[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\\.[0-9]{1,7})?Z") ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time))
            throw new InvalidDataException();
        return time;
    }

    private static void Window(DateTimeOffset issued, DateTimeOffset expiry, DateTimeOffset now) =>
        Require(issued <= now.AddSeconds(5) && issued < expiry && now < expiry);
    internal static string Stamp(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    internal static bool IsId(string value) => Match(value, "[!-~]{1,128}");
    internal static bool Match(string value, string pattern) =>
        Regex.IsMatch(value, "\\A" + pattern + "\\z", RegexOptions.CultureInvariant);
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    internal static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value)
    {
        Require(Match(value, "[A-Za-z0-9_-]+"));
        var bytes = Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
        Require(B64(bytes) == value);
        return bytes;
    }
    internal static byte[] Encode<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);
    internal static int TextSize(string value) => Utf8.GetByteCount(value);
    internal static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException();
    }
    internal static bool Invalid(Exception error) =>
        error is InvalidDataException or JsonException or FormatException or DecoderFallbackException or EncoderFallbackException
            or InvalidOperationException or KeyNotFoundException or CryptographicException;
}
