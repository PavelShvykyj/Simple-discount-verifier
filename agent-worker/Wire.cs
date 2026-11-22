using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Sdv.Worker;

internal enum Ingress { Prepare, Start, Result }
internal enum Disposition { Processed, Duplicate, Discarded }
internal sealed record Caller(string Owner, string Session, string CallId);
internal sealed record Envelope(Ingress Kind, string EventId, string RequestId, string OperationId, string Branch,
    DateTimeOffset Issued, DateTimeOffset Expiry, string ExpiryText, string Route, string Hash,
    string? CommandId, string? ToolCallId, JsonElement Data);
internal sealed record Command(string Id, string ToolId, string Route, string Hash, byte[] Body);

internal sealed class Wire : IDisposable
{
    internal const string SwaIssuer = "sdv-swa";
    internal const string RouterIssuer = "sdv-router";
    internal const string PrepareAudience = "sdv-hosted-prepare";
    internal const string StartAudience = "sdv-hosted-start";
    internal const string ResultAudience = "sdv-hosted-tool-result";
    internal const string InputVersion = "1";
    internal const string ResultVersion = "2";
    internal const int CommandVersion = 2;
    internal const string FoundOutcome = "Found";
    internal const string NotFoundOutcome = "NotFound";
    private const int TurnFinalSchemaVersion = 1;
    private const string TurnFinalKind = "turn.final";
    private const string AnsweredStatus = "Answered";
    private const string FailedStatus = "Failed";

    private readonly Dictionary<string, RSA> swaKeys = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RSA> routerKeys = new(StringComparer.Ordinal);

    internal Wire(WorkerOptions options)
    {
        try
        {
            foreach (var (kid, pem) in options.SwaPublicKeys) swaKeys.Add(kid, WorkerOptions.ReadPublicKey(pem));
            foreach (var (kid, pem) in options.RouterPublicKeys) routerKeys.Add(kid, WorkerOptions.ReadPublicKey(pem));
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        foreach (var key in swaKeys.Values.Concat(routerKeys.Values)) key.Dispose();
    }

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
        var (kind, audience) = Text(body, "kind") switch
        {
            "session.prepare" => (Ingress.Prepare, PrepareAudience),
            "turn.start" => (Ingress.Start, StartAudience),
            "tool.result" => (Ingress.Result, ResultAudience),
            _ => throw new InvalidDataException()
        };
        var result = kind == Ingress.Result;
        Shape(body, result ? [.. Common, .. Tool, "commandDataHash", "resultProof"] : Common);
        Require(body.GetProperty("schemaVersion").GetRawText() == (result ? ResultVersion : InputVersion));

        var eventId = Uuid(body, "eventId");
        var requestId = Uuid(body, "requestId");
        var operationId = Uuid(body, "operationId");
        var branch = Text(body, "branchId");
        Require(Match(branch, WorkerOptions.BranchPattern));
        var issuedText = Text(body, "issuedAt");
        var expiryText = Text(body, "expiresAt");
        var issued = Time(issuedText);
        var expiry = Time(expiryText);
        Window(issued, expiry, now);
        if (!result) Require(expiry - issued <= LookupTiming.MaxTurnDuration);
        var route = Text(body, "route");
        var claims = Verify(result ? Text(body, "resultProof") : route, result);
        var hashField = result ? "commandDataHash" : "dataHash";
        Shape(claims, result ? [.. Claims, .. Tool, hashField, "eventId", "resultDataHash", "routeHash"] : [.. Claims, hashField]);
        Require(claims.GetProperty("v").GetRawText() == "1" &&
            Text(claims, "iss") == (result ? RouterIssuer : SwaIssuer) &&
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
            Require(Text(claims, "eventId") == eventId && Text(claims, "issuedAt") == issuedText &&
                Text(body, "commandDataHash") == hash &&
                Text(claims, "resultDataHash") == Hash(dataBytes) &&
                Text(claims, "routeHash") == Hash(Encoding.UTF8.GetBytes(route)) &&
                Match(hash, "[0-9a-f]{64}"));
        }
        else
        {
            Require(Text(claims, "issuedAt") == issuedText && hash == Hash(dataBytes));
        }

        return new(kind, eventId, requestId, operationId, branch, issued, expiry, expiryText, route, hash,
            commandId, toolId, Parse(dataBytes));
    }

    internal static Command CreateCommand(Envelope start, string number, DateTimeOffset now)
    {
        var id = Guid.NewGuid().ToString("D");
        var tool = Guid.NewGuid().ToString("D");
        var earliest = start.Expiry.Subtract(LookupTiming.MaxTurnDuration);
        var issued = Stamp(now < earliest ? earliest : now);
        var data = Encode(new { orderNumber = number });
        var hash = Hash(data);
        var route = start.Route;
        var body = Encode(new
        {
            schemaVersion = CommandVersion, kind = "tool.command", eventId = id, start.RequestId, start.OperationId,
            branchId = start.Branch, issuedAt = issued, expiresAt = start.ExpiryText, route,
            dataB64 = Base64Url.EncodeToString(data), commandId = id, toolCallId = tool, commandType = "LookupOrder", commandDataHash = hash
        });
        Require(body.Length <= Limit);
        return new(id, tool, route, hash, body);
    }

    internal static byte[] CreateTurnFinal(Envelope envelope, string? answer, string? errorCode)
    {
        if (answer is not null)
            return Encode(new
            {
                schemaVersion = TurnFinalSchemaVersion, kind = TurnFinalKind, envelope.RequestId, envelope.OperationId,
                status = AnsweredStatus, answer
            });

        ArgumentNullException.ThrowIfNull(errorCode);
        return Encode(new
        {
            schemaVersion = TurnFinalSchemaVersion, kind = TurnFinalKind, envelope.RequestId, envelope.OperationId,
            status = FailedStatus, errorCode
        });
    }

    internal static (string Kind, string ReasonCode) ReadOutcome(JsonElement data, string number)
    {
        Shape(data, ["outcome", "reasonCode"], "order");
        var outcome = Text(data, "outcome");
        var reason = Text(data, "reasonCode");
        var hasOrder = data.TryGetProperty("order", out var order);
        if (outcome == FoundOutcome && reason == "Success" && hasOrder)
        {
            Shape(order, ["orderNumber", "orderStateCode", "receiptState", "amount"], "receiptNumber");
            var receipt = Text(order, "receiptState");
            var hasReceipt = order.TryGetProperty("receiptNumber", out _);
            Require(Text(order, "orderNumber") == number && Text(order, "orderStateCode") is "Open" or "Closed" &&
                receipt is "NotFiscalized" or "Fiscalized" or "Unknown" && hasReceipt == (receipt == "Fiscalized"));
            if (hasReceipt) Require(!string.IsNullOrWhiteSpace(Text(order, "receiptNumber")));
            var amount = order.GetProperty("amount");
            Shape(amount, ["value", "currency"]);
            Require(Match(Text(amount, "value"), "[0-9]{1,12}\\.[0-9]{2}") && Text(amount, "currency") == "UAH");
            return (outcome, reason);
        }
        Require(!hasOrder && (outcome == NotFoundOutcome && reason == "OrderMissing" ||
            outcome == "Failed" && reason is
                "AccessDenied" or "Expired" or "InvalidArguments" or "ApiUnavailable" or "ApiOutcomeUnknown"));
        return (outcome, reason);
    }

    private JsonElement Verify(string route, bool result)
    {
        var segments = route.Split('.');
        Require(segments.Length == 3);
        var header = Parse(Decode(segments[0]));
        Shape(header, ["alg", "kid"]);
        Require(Text(header, "alg") == "PS256");
        var kid = Text(header, "kid");
        var key = (result ? routerKeys : swaKeys).GetValueOrDefault(kid);
        if (key is null || key.KeySize < WorkerOptions.MinimumRsaKeySize) throw new InvalidDataException();
        var input = Encoding.ASCII.GetBytes(segments[0] + "." + segments[1]);
        var signature = Decode(segments[2]);
        lock (key)
            Require(signature.Length == (key.KeySize + 7) / 8 &&
                key.VerifyData(input, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        return Parse(Decode(segments[1]));
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
        try
        {
            foreach (var property in element.EnumerateObject())
            {
                var name = property.Name;
                Require(seen.Add(name) && property.Value.ValueKind != JsonValueKind.Null &&
                    (required.Contains(name) || optional.Contains(name)));
            }
        }
        catch (InvalidOperationException error) when (error is not ObjectDisposedException)
        {
            throw new InvalidDataException("Invalid JSON property name.", error);
        }
        Require(required.All(seen.Contains));
    }

    internal static string ReadEventId(JsonElement body)
    {
        Require(body.ValueKind == JsonValueKind.Object);
        try
        {
            Require(body.EnumerateObject().Count(property => property.NameEquals("eventId")) == 1);
        }
        catch (InvalidOperationException error) when (error is not ObjectDisposedException)
        {
            throw new InvalidDataException("Invalid JSON property name.", error);
        }
        return Uuid(body, "eventId");
    }

    internal static string Text(JsonElement element, string name)
    {
        Require(element.ValueKind == JsonValueKind.Object);
        try
        {
            if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
                throw new InvalidDataException();
            return property.GetString()!;
        }
        catch (InvalidOperationException error) when (error is not ObjectDisposedException)
        {
            throw new InvalidDataException("Invalid JSON string.", error);
        }
    }

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
        Require(issued <= now.Add(LookupTiming.ClockSkew) && issued < expiry && now < expiry);
    internal static string Stamp(DateTimeOffset value) => value.UtcDateTime.ToString("O", CultureInfo.InvariantCulture);
    internal static bool IsId(string value) => Match(value, "[!-~]{1,128}");
    internal static bool Match(string value, string pattern) =>
        Regex.IsMatch(value, "\\A" + pattern + "\\z", RegexOptions.CultureInvariant);
    internal static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static byte[] Decode(string value)
    {
        Require(value.Length != 0);
        var bytes = Base64Url.DecodeFromChars(value);
        Require(Base64Url.EncodeToString(bytes) == value);
        return bytes;
    }
    internal static byte[] Encode<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Json);
    internal static int TextSize(string value) => Utf8.GetByteCount(value);
    internal static void Require(bool condition)
    {
        if (!condition) throw new InvalidDataException();
    }
    internal static bool Invalid(Exception error) =>
        error is InvalidDataException or JsonException or FormatException or DecoderFallbackException;
}
