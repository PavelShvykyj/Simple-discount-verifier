using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Sdv.Connector.Api;
using Sdv.Connector.Contracts;
using Sdv.Connector.Management;
using Sdv.Connector.Messaging;

namespace Sdv.Connector;

sealed class FakeApi : IOneCApi
{
    public bool Enabled { get; set; } = true;
    public int Lookups;
    public Func<LookupResult> OnLookup = () => new(Outcomes.NotFound, ReasonCodes.OrderMissing);
    public ApiSnapshot Snapshot() => new(Enabled, Enabled ? ApiState.Active : ApiState.Disabled, null, null, ApiErrors.None);
    public Task<LookupResult> LookupAsync(CommandRequest command, CancellationToken stop) { Lookups++; return Task.FromResult(OnLookup()); }
    public Task<string> StartAsync(CancellationToken stop) { Enabled = true; return Task.FromResult("Started"); }
    public Task<string> PingAsync(CancellationToken stop) => Task.FromResult("Active");
    public Task<string> LogoutAsync(CancellationToken stop) => Task.FromResult("Closed");
    public Task<string> DisableAsync(CancellationToken stop) { Enabled = false; return Task.FromResult("Closed"); }
}

sealed class FakePublisher : IResultPublisher
{
    public List<(byte[] Body, string EventId, string CommandId, TimeSpan Ttl)> Sent = [];
    public bool Fail;
    public Task PublishAsync(byte[] body, string eventId, string commandId, TimeSpan timeToLive, CancellationToken cancel)
    {
        if (Fail) throw new InvalidOperationException();
        Sent.Add((body, eventId, commandId, timeToLive));
        return Task.CompletedTask;
    }
}

sealed class FakeListener : ICommandListener
{
    public bool Running;
    public bool FailStart;
    public int Resets;
    public ListenerHealth? Health;
    public Task StartAsync(CancellationToken cancel)
    {
        if (FailStart) throw new InvalidOperationException();
        Running = true; Health?.SetState(ListenerState.Listening); return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancel) { Running = false; return Task.CompletedTask; }
    public Task ResetAsync(CancellationToken cancel) { Resets++; Running = false; return Task.CompletedTask; }
}

sealed class TestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Production";
    public string ApplicationName { get; set; } = "Connector";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

public class CommandProcessorTests
{
    readonly TestTime time = new(TestData.Now);
    readonly FakeApi api = new();
    readonly FakePublisher publisher = new();
    readonly ListenerHealth health;
    readonly CommandProcessor processor;

    public CommandProcessorTests()
    {
        health = new ListenerHealth(time);
        processor = new CommandProcessor(TestData.Wrap(TestData.Settings()), api, publisher, health, time, NullLogger<CommandProcessor>.Instance);
    }

    [Fact]
    public async Task ValidCommandProducesOneResultWithMatchingIds()
    {
        var envelope = TestData.Envelope();
        await processor.ProcessAsync(TestData.Bytes(envelope), "application/json", default);
        var sent = Assert.Single(publisher.Sent);
        Assert.Equal(envelope["commandId"]!.GetValue<string>(), sent.CommandId);
        var result = StrictJson.Parse(sent.Body, CommandParser.MaxEnvelopeBytes);
        Assert.Equal(sent.EventId, result.GetProperty("eventId").GetString());
        Assert.Equal(TimeSpan.FromSeconds(120), sent.Ttl);
        Assert.False(health.ActiveLookup);
    }

    [Fact]
    public async Task InvalidEnvelopeIsDiscardedWithoutLookupOrResult()
    {
        await processor.ProcessAsync("{}"u8.ToArray(), "application/json", default);
        Assert.Equal(0, api.Lookups);
        Assert.Empty(publisher.Sent);
    }

    [Fact]
    public async Task ForeignBranchGetsAccessDeniedWithoutCallingOneC()
    {
        await processor.ProcessAsync(TestData.Bytes(TestData.Envelope("102")), "application/json", default);
        Assert.Equal(0, api.Lookups);
        var data = StrictJson.Parse(StrictJson.FromBase64Url(StrictJson.Parse(Assert.Single(publisher.Sent).Body, CommandParser.MaxEnvelopeBytes).GetProperty("dataB64").GetString()!), CommandParser.MaxEnvelopeBytes);
        Assert.Equal(ReasonCodes.AccessDenied, data.GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task ResultIsNotSentWhenTheCommandExpiredDuringLookup()
    {
        api.OnLookup = () => { time.Now = TestData.Now.AddSeconds(121); return new(Outcomes.NotFound, ReasonCodes.OrderMissing); };
        await processor.ProcessAsync(TestData.Bytes(TestData.Envelope()), "application/json", default);
        Assert.Empty(publisher.Sent);
    }

    [Fact]
    public async Task CommandCancelledByShutdownStillGetsAnUnknownOutcomeResult()
    {
        using var stop = new CancellationTokenSource();
        api.OnLookup = () => { stop.Cancel(); throw new OperationCanceledException(stop.Token); };
        await processor.ProcessAsync(TestData.Bytes(TestData.Envelope()), "application/json", stop.Token);
        var data = StrictJson.Parse(StrictJson.FromBase64Url(StrictJson.Parse(Assert.Single(publisher.Sent).Body, CommandParser.MaxEnvelopeBytes).GetProperty("dataB64").GetString()!), CommandParser.MaxEnvelopeBytes);
        Assert.Equal(ReasonCodes.ApiOutcomeUnknown, data.GetProperty("reasonCode").GetString());
    }

    [Fact]
    public async Task SendFailureIsRecordedAndNotRetried()
    {
        publisher.Fail = true;
        await processor.ProcessAsync(TestData.Bytes(TestData.Envelope()), "application/json", default);
        Assert.Equal(1, api.Lookups);
        Assert.Equal("SendUnknown", health.Snapshot().SendErrorCode);
    }
}

public class ManagementServiceTests
{
    readonly TestTime time = new(TestData.Now);
    readonly FakeApi api = new();
    readonly FakeListener listener = new();
    readonly ListenerHealth health;
    readonly ManagementService management;

    public ManagementServiceTests()
    {
        health = new ListenerHealth(time);
        listener.Health = health;
        management = new ManagementService(TestData.Wrap(TestData.Settings()), new TestEnvironment(), api, listener, health, NullLogger<ManagementService>.Instance);
    }

    [Fact]
    public async Task StatusIsAvailableWhileBusyAndControlCommandsAreRefused()
    {
        health.SetActiveLookup(true);
        Assert.StartsWith("🟡 Занято", await management.ExecuteAsync("/stoplistener", default));
        Assert.Contains("⏳", await management.ExecuteAsync("/status", default));
    }

    [Fact]
    public async Task StatusShowsOnlyTheCurrentStateOfTheQueueAndTheSession()
    {
        health.SetState(ListenerState.Listening);
        Assert.Equal("🟢 Очередь: слушаю\n🟢 Сессия 1С: активна", await management.ExecuteAsync("/status", default));
        Assert.Equal("🟢 Очередь: слушаю", await management.ExecuteAsync("/statuslistener", default));
        api.Enabled = false;
        health.SetState(ListenerState.PausedForApi);
        Assert.Equal("🔴 Сессия 1С: отключена", await management.ExecuteAsync("/statusapi", default));
        Assert.Contains("🟡 Очередь: на паузе", await management.ExecuteAsync("/status", default));
    }

    [Fact]
    public async Task ListenerCanBeStoppedAndStartedExplicitly()
    {
        await management.ReconcileAsync(default);
        Assert.True(listener.Running);
        Assert.StartsWith("✅", await management.ExecuteAsync("/stoplistener", default));
        Assert.False(listener.Running);
        Assert.StartsWith("🟡", await management.ExecuteAsync("/stoplistener", default));
        Assert.Equal(ListenerState.Stopped, health.Snapshot().State);
        Assert.StartsWith("✅", await management.ExecuteAsync("/startlistener", default));
        Assert.True(listener.Running);
    }

    [Fact]
    public async Task StoppingTheApiPausesTheListenerUntilTheApiIsStarted()
    {
        await management.ReconcileAsync(default);
        Assert.StartsWith("✅ Сессия 1С закрыта", await management.ExecuteAsync("/stopapi", default));
        Assert.False(listener.Running);
        Assert.Equal(ListenerState.PausedForApi, health.Snapshot().State);
        Assert.Equal("✅ Сессия 1С открыта", await management.ExecuteAsync("/startapi", default));
        Assert.True(listener.Running);
    }

    [Fact]
    public async Task StartingTheListenerWhileTheApiIsDisabledWarnsInsteadOfClaimingSuccess()
    {
        await management.ExecuteAsync("/stoplistener", default);
        await api.DisableAsync(default);
        Assert.StartsWith("🟡", await management.ExecuteAsync("/startlistener", default));
    }

    [Fact]
    public async Task RestartResetsTheConnectionAndResumesListening()
    {
        Assert.StartsWith("✅", await management.ExecuteAsync("/restartlistener", default));
        Assert.Equal(1, listener.Resets);
        Assert.True(listener.Running);
    }

    [Fact]
    public async Task FailedStartDoesNotLeaveTheListenerWishChanged()
    {
        await management.ExecuteAsync("/stoplistener", default);
        listener.FailStart = true;
        Assert.StartsWith("🔴", await management.ExecuteAsync("/startlistener", default));
        listener.FailStart = false;
        Assert.StartsWith("✅", await management.ExecuteAsync("/startlistener", default));
        Assert.True(listener.Running);
    }

    [Fact]
    public void ReconnectingStatusClearsAfterQuietPeriodAndLateErrorsDoNotOverrideOperatorStates()
    {
        health.SetState(ListenerState.Listening);
        health.MarkReceiveError("ReceiveFailed", faulted: false);
        Assert.Equal(ListenerState.Reconnecting, health.Snapshot().State);
        time.Now = TestData.Now + ListenerHealth.ReconnectQuietPeriod;
        Assert.Equal(ListenerState.Listening, health.Snapshot().State);

        health.SetState(ListenerState.Stopped);
        health.MarkReceiveError("ReceiveFailed", faulted: false);
        Assert.Equal(ListenerState.Stopped, health.Snapshot().State);
    }

    [Fact]
    public async Task UnknownCommandIsReported()
    {
        Assert.StartsWith("🔴", await management.ExecuteAsync("/nothing", default));
    }

    [Fact]
    public void IdentityUsesConfiguredEnvironmentOrHostEnvironment()
    {
        Assert.Equal("Production · филиал 101", management.Identity);
        var options = TestData.Settings();
        options.Environment = "Pilot";
        var named = new ManagementService(TestData.Wrap(options), new TestEnvironment(), api, listener, health, NullLogger<ManagementService>.Instance);
        Assert.Equal("Pilot · филиал 101", named.Identity);
    }
}
