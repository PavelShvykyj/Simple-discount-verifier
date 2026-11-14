using System.Text.Json;

namespace Sdv.Connector.Contracts;

internal static class ResultEnvelope
{
    /// <exception cref="InvalidDataException">The serialized result exceeds the envelope size limit.</exception>
    internal static byte[] Create(CommandRequest command, LookupResult result, DateTimeOffset now, string eventId)
    {
        var fields = command.Envelope.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
        fields["kind"] = "tool.result";
        fields["eventId"] = eventId;
        fields["issuedAt"] = StrictJson.Stamp(now);
        var data = new Dictionary<string, object?> { ["outcome"] = result.Outcome, ["reasonCode"] = result.Reason };
        if (result.Order is not null) data["order"] = result.Order;
        fields["dataB64"] = StrictJson.ToBase64Url(JsonSerializer.SerializeToUtf8Bytes(data));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(fields);
        if (bytes.Length > CommandParser.MaxEnvelopeBytes) throw new InvalidDataException();
        return bytes;
    }
}
