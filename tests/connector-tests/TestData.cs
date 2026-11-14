using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Sdv.Connector.Configuration;
using Sdv.Connector.Contracts;

namespace Sdv.Connector;

internal static class TestData
{
    internal static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-07T12:00:00Z");

    internal static ConnectorOptions Settings(string branch = "101")
    {
        var key = Convert.ToBase64String(new byte[32]);
        return new()
        {
            BranchId = branch,
            ServiceName = "Sdv.Connector",
            Api = new() { BaseUri = "http://127.0.0.1:18101", User = "svc", Password = "secret", PingPath = "/hs/ping", LookupPath = "/hs/orders/lookup" },
            ServiceBus = new()
            {
                CommandListenConnectionString = $"Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=connector-{branch}-listen;SharedAccessKey={key};EntityPath=commands{branch}",
                ResultSendConnectionString = $"Endpoint=sb://ns.servicebus.windows.net/;SharedAccessKeyName=connector-{branch}-send;SharedAccessKey={key};EntityPath=results"
            },
            Telegram = new() { Enabled = false }
        };
    }

    internal static IOptions<ConnectorOptions> Wrap(ConnectorOptions options) => Microsoft.Extensions.Options.Options.Create(options);

    internal static JsonObject Envelope(string branch = "101", string data = "{\"orderNumber\":\"00042\"}")
    {
        var id = Guid.NewGuid().ToString("D");
        return new()
        {
            ["schemaVersion"] = 1,
            ["kind"] = "tool.command",
            ["eventId"] = id,
            ["commandId"] = id,
            ["requestId"] = Guid.NewGuid().ToString("D"),
            ["operationId"] = Guid.NewGuid().ToString("D"),
            ["branchId"] = branch,
            ["toolCallId"] = "tool-1",
            ["commandType"] = "LookupOrder",
            ["issuedAt"] = StrictJson.Stamp(Now),
            ["expiresAt"] = StrictJson.Stamp(Now.AddSeconds(120)),
            ["route"] = "e30.e30.AA",
            ["dataB64"] = StrictJson.ToBase64Url(Encoding.UTF8.GetBytes(data))
        };
    }

    internal static byte[] Bytes(JsonObject value) => Encoding.UTF8.GetBytes(value.ToJsonString());

    internal static CommandRequest Command(string branch = "101") =>
        CommandParser.Parse(Bytes(Envelope(branch)), "application/json", branch, Now);
}

internal sealed class TestTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}
