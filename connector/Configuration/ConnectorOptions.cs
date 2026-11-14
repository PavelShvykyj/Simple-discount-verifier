namespace Sdv.Connector.Configuration;

public sealed class ConnectorOptions
{
    public const string SectionName = "Connector";

    public string Environment { get; set; } = "";
    public string BranchId { get; set; } = "";
    public string ServiceName { get; set; } = "";
    public OneCApiOptions Api { get; set; } = new();
    public ServiceBusOptions ServiceBus { get; set; } = new();
    public TelegramOptions Telegram { get; set; } = new();
}

public sealed class OneCApiOptions
{
    public string BaseUri { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string PingPath { get; set; } = "";
    public string LookupPath { get; set; } = "";
}

public sealed class ServiceBusOptions
{
    public string CommandListenConnectionString { get; set; } = "";
    public string ResultSendConnectionString { get; set; } = "";
}

public sealed class TelegramOptions
{
    public bool Enabled { get; set; } = true;
    public string BotToken { get; set; } = "";
    public long ChatId { get; set; }
    public long[] AllowedUserIds { get; set; } = [];
}
