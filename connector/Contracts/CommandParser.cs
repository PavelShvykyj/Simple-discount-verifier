using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using Sdv.Connector.Configuration;

namespace Sdv.Connector.Contracts;

internal static class CommandParser
{
    internal const int MaxEnvelopeBytes = 65536;
    static readonly string[] EnvelopeFields = ["schemaVersion", "kind", "eventId", "requestId", "operationId", "commandId", "toolCallId", "commandType", "branchId", "issuedAt", "expiresAt", "route", "dataB64"];

    /// <exception cref="InvalidDataException">The envelope is malformed or outside the allowed time window.</exception>
    internal static CommandRequest Parse(byte[] bytes, string? mime, string branchId, DateTimeOffset now)
    {
        if (mime != "application/json") throw new InvalidDataException();
        var e = StrictJson.Parse(bytes, MaxEnvelopeBytes);
        StrictJson.Shape(e, EnvelopeFields, "commandDataHash");
        var version = e.GetProperty("schemaVersion").GetRawText();
        StrictJson.Shape(e, version == "2" ? [.. EnvelopeFields, "commandDataHash"] : EnvelopeFields);
        if (version is not ("1" or "2") || StrictJson.Text(e.GetProperty("kind")) != "tool.command") throw new InvalidDataException();
        foreach (var name in new[] { "eventId", "requestId", "operationId", "commandId" })
        {
            var id = StrictJson.Text(e.GetProperty(name));
            if (id.Length != 36 || !Guid.TryParseExact(id, "D", out _)) throw new InvalidDataException();
        }
        if (StrictJson.Text(e.GetProperty("eventId")) != StrictJson.Text(e.GetProperty("commandId"))) throw new InvalidDataException();
        var tool = StrictJson.Text(e.GetProperty("toolCallId"));
        if (tool.Length is < 1 or > 128 || tool.Any(c => c > 127)) throw new InvalidDataException();
        var branch = StrictJson.Text(e.GetProperty("branchId"));
        if (!Patterns.BranchId().IsMatch(branch)) throw new InvalidDataException();
        var issued = StrictJson.Time(e.GetProperty("issuedAt"));
        var expiry = StrictJson.Time(e.GetProperty("expiresAt"));
        if (issued > now.AddSeconds(5) || issued >= expiry || expiry - issued > TimeSpan.FromSeconds(120) || now >= expiry) throw new InvalidDataException();
        if (!Patterns.Route().IsMatch(StrictJson.Text(e.GetProperty("route")))) throw new InvalidDataException();
        var commandType = StrictJson.Text(e.GetProperty("commandType"));
        var data = StrictJson.Text(e.GetProperty("dataB64"));
        if (version == "2" && StrictJson.Text(e.GetProperty("commandDataHash")) !=
            Convert.ToHexStringLower(SHA256.HashData(StrictJson.FromBase64Url(data))))
            throw new InvalidDataException();

        var request = new CommandRequest(e, expiry, null, null, null);
        if (branch != branchId) return request with { Rejection = ReasonCodes.AccessDenied };
        if (commandType != "LookupOrder") return request with { Rejection = ReasonCodes.InvalidArguments };
        try
        {
            var arguments = StrictJson.Parse(StrictJson.FromBase64Url(data), MaxEnvelopeBytes);
            StrictJson.Shape(arguments, ["orderNumber"], "amountHint");
            var number = StrictJson.Text(arguments.GetProperty("orderNumber"));
            if (!Patterns.OrderNumber().IsMatch(number)) throw new InvalidDataException();
            JsonElement? hint = null;
            if (arguments.TryGetProperty("amountHint", out var h)) { OrderContract.ValidateMoney(h); hint = h; }
            return request with { OrderNumber = number, AmountHint = hint };
        }
        catch (Exception x) when (x is JsonException or InvalidDataException or FormatException or DecoderFallbackException)
        {
            return request with { Rejection = ReasonCodes.InvalidArguments };
        }
    }
}
