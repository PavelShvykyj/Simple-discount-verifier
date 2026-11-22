using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sdv.Worker;

namespace WorkerChecks;

internal sealed class Fixtures(RSA key, Clock clock, RSA routerKey)
{
    internal JsonObject Input(Scope scope, Ingress source, string text = "00042",
        string branch = "101", DateTimeOffset? clientSent = null)
    {
        var issued = clock.GetUtcNow();
        var expiry = new[] { (clientSent ?? issued).AddSeconds(120), issued.AddSeconds(120) }.Min();
        var data = source == Ingress.Prepare ? "{}"u8.ToArray() : JsonSerializer.SerializeToUtf8Bytes(new { text });
        var body = new JsonObject
        {
            ["schemaVersion"] = 1, ["kind"] = source == Ingress.Prepare ? "session.prepare" : "turn.start",
            ["eventId"] = Id(), ["requestId"] = Id(), ["operationId"] = Id(), ["branchId"] = branch,
            ["issuedAt"] = Stamp(issued), ["expiresAt"] = Stamp(expiry), ["dataB64"] = B64(data)
        };
        Sign(body, scope);
        return body;
    }

    internal void Sign(JsonObject body, Scope scope, Action<JsonObject>? changeClaims = null, string kid = "fixture",
        string algorithm = "PS256")
    {
        var claims = new JsonObject
        {
            ["v"] = 1, ["iss"] = "sdv-swa", ["aud"] = body["kind"]!.ToString() == "session.prepare"
                ? "sdv-hosted-prepare" : "sdv-hosted-start",
            ["userKey"] = scope.Owner, ["agentSessionId"] = scope.Session,
            ["dataHash"] = Digest(Decode(body["dataB64"]!.ToString()))
        };
        foreach (var field in new[] { "branchId", "requestId", "operationId", "issuedAt", "expiresAt" })
            claims[field] = body[field]!.DeepClone();
        changeClaims?.Invoke(claims);
        var header = JsonSerializer.SerializeToUtf8Bytes(new { alg = algorithm, kid });
        var input = B64(header) + "." + B64(Bytes(claims));
        body["route"] = input + "." + B64(key.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    internal JsonObject Result(JsonObject command, object data)
    {
        var result = Copy(command);
        result["kind"] = "tool.result";
        result["eventId"] = Id();
        result["issuedAt"] = Stamp(clock.GetUtcNow());
        result["dataB64"] = B64(JsonSerializer.SerializeToUtf8Bytes(data));
        SignResult(result, Digest(Decode(command["dataB64"]!.ToString())));
        return result;
    }

    internal void SignResult(JsonObject result, string? commandHash = null)
    {
        commandHash ??= result["commandDataHash"]!.ToString();
        var route = result["route"]!.ToString();
        var source = JsonNode.Parse(Decode(route.Split('.')[1]))!.AsObject();
        var claims = new JsonObject
        {
            ["v"] = 1, ["iss"] = "sdv-router", ["aud"] = "sdv-hosted-tool-result",
            ["userKey"] = source["userKey"]!.DeepClone(), ["agentSessionId"] = source["agentSessionId"]!.DeepClone(),
            ["commandDataHash"] = commandHash,
            ["resultDataHash"] = Digest(Decode(result["dataB64"]!.ToString())),
            ["routeHash"] = Digest(Encoding.UTF8.GetBytes(route))
        };
        foreach (var field in new[] { "eventId", "branchId", "requestId", "operationId", "commandId", "toolCallId", "commandType", "issuedAt", "expiresAt" })
            claims[field] = result[field]!.DeepClone();
        var input = B64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "PS256", kid = "router" })) + "." + B64(Bytes(claims));
        result["schemaVersion"] = 2;
        result["resultProof"] = input + "." + B64(routerKey.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
    }

    internal static JsonObject CommandCheck(byte[] bytes, string expectedBranch, string number, Scope scope,
        RSA verifyKey, JsonObject start)
    {
        var command = JsonNode.Parse(bytes)!.AsObject();
        Need(command.Count == 14 && command["schemaVersion"]!.ToJsonString() == "2" &&
            command["kind"]!.ToString() == "tool.command" && command["commandType"]!.ToString() == "LookupOrder" &&
            command["eventId"]!.ToString() == command["commandId"]!.ToString() &&
            Guid.TryParseExact(command["eventId"]!.ToString(), "D", out _) &&
            command["toolCallId"]!.ToString().Length is > 0 and <= 128 &&
            command["branchId"]!.ToString() == expectedBranch, "S4 command envelope");
        foreach (var field in new[] { "branchId", "requestId", "operationId", "expiresAt" })
            Need(command[field]!.ToString() == start[field]!.ToString(), "S4 command inherited field");
        var data = Decode(command["dataB64"]!.ToString());
        Need(JsonNode.Parse(data)!.AsObject() is { Count: 1 } args && args["orderNumber"]!.ToString() == number,
            "S4 command order number preserves leading zeros");
        Need(command["commandDataHash"]!.ToString() == Digest(data), "Command hash covers exact payload bytes");
        var route = command["route"]!.ToString().Split('.');
        Need(route.Length == 3 && verifyKey.VerifyData(Encoding.ASCII.GetBytes(route[0] + "." + route[1]),
            Decode(route[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pss), "Preserved SWA PS256 route");
        var header = JsonNode.Parse(Decode(route[0]))!.AsObject();
        Need(header.Count == 2 && header["alg"]!.ToString() == "PS256" && header["kid"]!.ToString() == "fixture",
            "SWA protected header");
        var claims = JsonNode.Parse(Decode(route[1]))!.AsObject();
        Need(claims["iss"]!.ToString() == "sdv-swa" && claims["aud"]!.ToString() == "sdv-hosted-start" &&
            claims["userKey"]!.ToString() == scope.Owner && claims["agentSessionId"]!.ToString() == scope.Session &&
            command["route"]!.ToString() == start["route"]!.ToString(), "Original SWA route is preserved");
        return command;
    }

    internal static readonly object Missing = new { outcome = "NotFound", reasonCode = "OrderMissing" };
    internal static readonly object Unknown = new { outcome = "Failed", reasonCode = "ApiOutcomeUnknown" };
    internal static object Found(string number, string branch, string receipt = "Fiscalized") => new
    {
        outcome = "Found", reasonCode = "Success",
        order = new
        {
            orderNumber = number, orderStateCode = "Closed", receiptState = receipt,
            amount = new { value = "987.65", currency = "UAH" }, receiptNumber = $"TEST-{branch}-00042"
        }
    };
    internal static string Id() => Guid.NewGuid().ToString("D");
    internal static string Stamp(DateTimeOffset value) => value.UtcDateTime.ToString("O");
    internal static byte[] Bytes(JsonNode value) => Encoding.UTF8.GetBytes(value.ToJsonString());
    internal static JsonObject Copy(JsonObject value) => value.DeepClone().AsObject();
    private static string Digest(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    internal static string B64(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') +
        new string('=', (4 - value.Length % 4) % 4));
    internal static void Need(bool value, string name)
    {
        Xunit.Assert.True(value, name);
    }
}
