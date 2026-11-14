using System.Text.Json;
using Microsoft.Extensions.Options;
using Sdv.Connector.Configuration;
using Sdv.Connector.Telegram;

namespace Sdv.Connector;

public class ConnectorOptionsValidatorTests
{
    static ValidateOptionsResult Validate(ConnectorOptions options) => new ConnectorOptionsValidator().Validate(null, options);

    [Fact]
    public void ValidSettingsPass()
    {
        Assert.True(Validate(TestData.Settings()).Succeeded);
    }

    [Fact]
    public void TelegramIsNotValidatedWhenDisabled()
    {
        var options = TestData.Settings();
        options.Telegram = new() { Enabled = false };
        Assert.True(Validate(options).Succeeded);
    }

    [Fact]
    public void TelegramSettingsAreRequiredWhenEnabled()
    {
        var options = TestData.Settings();
        options.Telegram = new() { Enabled = true, BotToken = "bad", ChatId = 0, AllowedUserIds = [5, 5] };
        var failures = Validate(options).Failures!.ToArray();
        Assert.Equal(3, failures.Length);
    }

    [Theory]
    [InlineData(nameof(ConnectorOptions.BranchId), "10A")]
    [InlineData(nameof(ConnectorOptions.ServiceName), " ")]
    [InlineData("BaseUri", "http://example.com")]
    [InlineData("BaseUri", "http://127.0.0.1:18101/path?x=1")]
    [InlineData("User", "a:b")]
    [InlineData("User", "")]
    [InlineData("Password", "")]
    [InlineData("PingPath", "ping")]
    [InlineData("PingPath", "//host/ping")]
    [InlineData("LookupPath", "/lookup?x=1")]
    [InlineData("LookupPath", "")]
    public void InvalidSettingsAreReported(string setting, string value)
    {
        var options = TestData.Settings();
        switch (setting)
        {
            case nameof(ConnectorOptions.BranchId): options.BranchId = value; break;
            case nameof(ConnectorOptions.ServiceName): options.ServiceName = value; break;
            case "BaseUri": options.Api.BaseUri = value; break;
            case "User": options.Api.User = value; break;
            case "Password": options.Api.Password = value; break;
            case "PingPath": options.Api.PingPath = value; break;
            case "LookupPath": options.Api.LookupPath = value; break;
        }
        Assert.True(Validate(options).Failed);
    }

    [Fact]
    public void ServiceBusConnectionsMustMatchBranchQueuesPoliciesAndNamespace()
    {
        var wrongPolicy = TestData.Settings();
        wrongPolicy.ServiceBus.CommandListenConnectionString = wrongPolicy.ServiceBus.CommandListenConnectionString.Replace("-listen", "-send");
        Assert.True(Validate(wrongPolicy).Failed);

        var wrongQueue = TestData.Settings();
        wrongQueue.ServiceBus.ResultSendConnectionString = wrongQueue.ServiceBus.ResultSendConnectionString.Replace("EntityPath=results", "EntityPath=other");
        Assert.True(Validate(wrongQueue).Failed);

        var otherNamespace = TestData.Settings();
        otherNamespace.ServiceBus.ResultSendConnectionString = otherNamespace.ServiceBus.ResultSendConnectionString.Replace("ns.", "other.");
        Assert.Contains(Validate(otherNamespace).Failures!, f => f.Contains("same namespace"));
    }
}

public class TelegramUpdateFilterTests
{
    static readonly DateTimeOffset Startup = TestData.Now.AddMinutes(-5);
    static readonly HashSet<long> Allowed = [7];

    static JsonElement Update(string text = "/status", long chat = 100, long user = 7, bool bot = false, DateTimeOffset? at = null, string extra = "")
    {
        var date = (at ?? TestData.Now.AddSeconds(-1)).ToUnixTimeSeconds();
        return JsonDocument.Parse($"{{\"update_id\":1,\"message\":{{\"chat\":{{\"id\":{chat},\"type\":\"private\"}},\"from\":{{\"id\":{user},\"is_bot\":{bot.ToString().ToLowerInvariant()}}},\"date\":{date},\"text\":\"{text}\"{extra}}}}}").RootElement;
    }

    static string? Authorize(JsonElement update) => TelegramUpdateFilter.Authorize(update, 100, Allowed, TestData.Now, Startup);

    [Fact]
    public void ExactCommandFromAllowedUserIsAccepted() => Assert.Equal("/status", Authorize(Update()));

    [Theory]
    [InlineData("/status ")]
    [InlineData("/unknown")]
    [InlineData("status")]
    public void OtherTextIsIgnored(string text) => Assert.Null(Authorize(Update(text)));

    [Fact]
    public void ForeignChatUserBotAndForwardsAreIgnored()
    {
        Assert.Null(Authorize(Update(chat: 101)));
        Assert.Null(Authorize(Update(user: 8)));
        Assert.Null(Authorize(Update(bot: true)));
        Assert.Null(Authorize(Update(extra: ",\"forward_date\":1")));
    }

    [Fact]
    public void StaleOrPreStartupMessagesAreIgnored()
    {
        Assert.Null(Authorize(Update(at: TestData.Now.AddSeconds(-61))));
        Assert.Null(Authorize(Update(at: Startup)));
        Assert.Null(Authorize(Update(at: TestData.Now.AddSeconds(6))));
    }

    [Fact]
    public void MalformedUpdateIsIgnored()
    {
        Assert.Null(Authorize(JsonDocument.Parse("{\"message\":{\"chat\":1}}").RootElement));
    }

    [Fact]
    public void OffsetOnlyMovesForward()
    {
        long offset = 0;
        Assert.True(TelegramUpdateFilter.Advance(ref offset, 5));
        Assert.Equal(6, offset);
        Assert.False(TelegramUpdateFilter.Advance(ref offset, 5));
        Assert.False(TelegramUpdateFilter.Advance(ref offset, long.MaxValue));
    }
}
