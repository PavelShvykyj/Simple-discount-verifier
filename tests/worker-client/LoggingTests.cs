using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json.Nodes;
using Azure;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using Sdv.Worker;
using WorkerChecks;
using Xunit;

public sealed class LoggingTests
{
    [Fact]
    public async Task InvalidInputIsDiagnosedWithoutLoggingItsContentOrRoute()
    {
        await using var h = new Harness();
        await h.Start();
        var input = h.Fixtures.Input(h.Alice, Ingress.Start, text: "private-chat-do-not-log");
        Assert.Equal("Discarded", await h.Invoke(h.Alice, input));
        var entries = h.Logs.WorkerEntries;
        var rejected = Assert.Single(entries.Where(entry => entry.Event.Name == nameof(WorkerLog.EventRejected)));
        Assert.Equal(nameof(RejectionReason.InvalidSignedEnvelope), rejected.Fields["Reason"]?.ToString());
        Assert.Null(rejected.Exception);
        Assert.All(entries, entry =>
        {
            Assert.DoesNotContain("private-chat-do-not-log", entry.Message);
            Assert.DoesNotContain(input["route"]!.ToString(), entry.Message);
            Assert.DoesNotContain("private-chat-do-not-log", string.Join(" ", entry.Fields.Values));
            Assert.DoesNotContain(input["route"]!.ToString(), string.Join(" ", entry.Fields.Values));
        });
    }

    [Fact]
    public async Task InternalFailureRetainsItsExceptionAndEventCorrelation()
    {
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        await using var h = new Harness();
        await h.Start();
        Assert.Equal("Processed", await h.Prepare(h.Alice));
        var failure = new IOException("State Store could not be reached.");
        h.Store.AfterRead = (_, _, _, _) => throw failure;
        var input = h.Fixtures.Input(h.Alice, Ingress.Start);
        Assert.Equal("500", await h.StartLookup(h.Alice, input));
        var entry = Assert.Single(h.Logs.WorkerEntries.Where(item => item.Event.Name == nameof(WorkerLog.InvocationFailed)));
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(failure, entry.Exception);
        Assert.Equal(input["eventId"]!.ToString(), entry.Fields["MessageEventId"]);
        Assert.NotEqual(default(ActivityTraceId), entry.TraceId);
    }

    [Fact]
    public async Task UnconfirmedSendIsLoggedOnceWithoutRetryOrLostContext()
    {
        await using var h = new Harness();
        await h.Start();
        Assert.Equal("Processed", await h.Prepare(h.Alice));
        var failure = new IOException("Service Bus acknowledgment was lost.");
        h.Transport.OnSend = _ => throw failure;
        var input = h.Fixtures.Input(h.Alice, Ingress.Start);
        Assert.Equal("Processed", await h.StartLookup(h.Alice, input));
        Assert.Equal("Duplicate", await h.StartLookup(h.Alice, input));
        var sent = Assert.Single(h.Transport.Commands);
        var entry = Assert.Single(h.Logs.WorkerEntries.Where(item => item.Event.Name == nameof(WorkerLog.EffectUnconfirmed)));
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Same(failure, entry.Exception);
        Assert.Equal(nameof(EffectKind.SendCommand), entry.Fields["Effect"]?.ToString());
        Assert.Equal(input["requestId"]!.ToString(), entry.Fields["RequestId"]);
        Assert.Equal(input["operationId"]!.ToString(), entry.Fields["OperationId"]);
        Assert.Equal(sent.Command.Id, entry.Fields["CommandId"]);
        Assert.Equal(Phase.WaitingTool, (await h.State(h.Alice)).Phase);
    }

    [Theory]
    [InlineData(ServiceBusFailureReason.MessagingEntityNotFound, nameof(WorkerLog.EffectRejected))]
    [InlineData(ServiceBusFailureReason.ServiceTimeout, nameof(WorkerLog.EffectUnconfirmed))]
    public async Task SendDistinguishesRejectionFromTimeoutWithoutRetry(ServiceBusFailureReason reason, string expectedEvent)
    {
        await using var h = new Harness();
        await h.Start();
        Assert.Equal("Processed", await h.Prepare(h.Alice));
        h.Transport.OnSend = _ => throw new ServiceBusException("Synthetic Service Bus failure.", reason);
        var input = h.Fixtures.Input(h.Alice, Ingress.Start);

        Assert.Equal("Processed", await h.StartLookup(h.Alice, input));
        Assert.Equal("Duplicate", await h.StartLookup(h.Alice, input));
        var command = Assert.Single(h.Transport.Commands);
        var diagnostic = Assert.Single(h.Logs.WorkerEntries.Where(entry =>
            entry.Event.Name is nameof(WorkerLog.EffectRejected) or nameof(WorkerLog.EffectUnconfirmed)));
        Assert.Equal(expectedEvent, diagnostic.Event.Name);
        Assert.Equal(command.Command.Id, diagnostic.Fields["CommandId"]);
        if (expectedEvent == nameof(WorkerLog.EffectRejected))
            Assert.Equal(reason.ToString(), diagnostic.Fields["Reason"]);
        Assert.Equal(Phase.WaitingTool, (await h.State(h.Alice)).Phase);
    }

    [Theory]
    [InlineData(403, nameof(WorkerLog.EffectRejected))]
    [InlineData(503, nameof(WorkerLog.EffectUnconfirmed))]
    public async Task PublishDistinguishesRejectionFromUnavailableWithoutRetry(int status, string expectedEvent)
    {
        await using var h = new Harness();
        await h.Start();
        Assert.Equal("Processed", await h.Prepare(h.Alice));
        Assert.Equal("Processed", await h.StartLookup(h.Alice, h.Fixtures.Input(h.Alice, Ingress.Start)));
        var command = JsonNode.Parse(Assert.Single(h.Transport.Commands).Command.Body)!.AsObject();
        var result = h.Fixtures.Result(command, Fixtures.Missing);
        h.Transport.PublicationFailure = new RequestFailedException(status, "Synthetic Web PubSub failure.");

        Assert.Equal("Processed", await h.Invoke(h.Alice, result));
        Assert.Equal("Duplicate", await h.Invoke(h.Alice, result));
        Assert.Single(h.Transport.Attempts);
        var diagnostic = Assert.Single(h.Logs.WorkerEntries.Where(entry =>
            entry.Event.Name is nameof(WorkerLog.EffectRejected) or nameof(WorkerLog.EffectUnconfirmed)));
        Assert.Equal(expectedEvent, diagnostic.Event.Name);
        Assert.Equal(nameof(EffectKind.PublishFinal), diagnostic.Fields["Effect"]?.ToString());
        Assert.Equal(Phase.IdleSuccess, (await h.State(h.Alice)).Phase);
    }

    [Fact]
    public async Task OrderMissingIsAnInformationalOutcomeAndFinalizationDoesNotLogTheAnswer()
    {
        await using var h = new Harness();
        await h.Start();
        Assert.Equal("Processed", await h.Prepare(h.Alice));
        var input = h.Fixtures.Input(h.Alice, Ingress.Start);
        Assert.Equal("Processed", await h.StartLookup(h.Alice, input));
        var command = JsonNode.Parse(Assert.Single(h.Transport.Commands).Command.Body)!.AsObject();
        Assert.Equal("Processed", await h.Invoke(h.Alice, h.Fixtures.Result(command, Fixtures.Missing)));
        var outcome = Assert.Single(h.Logs.WorkerEntries.Where(entry => entry.Event.Name == nameof(WorkerLog.ToolResultClaimed)));
        Assert.Equal(LogLevel.Information, outcome.Level);
        Assert.Equal("NotFound", outcome.Fields["Outcome"]);
        Assert.Equal("OrderMissing", outcome.Fields["ReasonCode"]);
        Assert.Contains(h.Logs.WorkerEntries, entry => entry.Event.Name == nameof(WorkerLog.TurnFinalized));
        Assert.DoesNotContain(h.Logs.WorkerEntries, entry => entry.Level >= LogLevel.Warning);
        Assert.All(h.Logs.WorkerEntries, entry => Assert.DoesNotContain("Заказ не найден", entry.Message));
    }
}

namespace WorkerChecks
{
    internal sealed record TestLog(string Category, LogLevel Level, EventId Event, string Message,
        Exception? Exception, Dictionary<string, object?> Fields, ActivityTraceId TraceId);

    internal sealed class TestLogProvider : ILoggerProvider, ISupportExternalScope
    {
        private readonly ConcurrentQueue<TestLog> entries = new();
        private IExternalScopeProvider scopes = new LoggerExternalScopeProvider();
        internal TestLog[] WorkerEntries => entries.Where(entry => entry.Category.StartsWith("Sdv.Worker.", StringComparison.Ordinal)).ToArray();
        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => scopes = scopeProvider;
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this, categoryName);
        public void Dispose() { }

        private sealed class CaptureLogger(TestLogProvider provider, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var fields = new Dictionary<string, object?>();
                provider.scopes.ForEachScope((scope, values) => AddFields(scope, values), fields);
                AddFields(state, fields);
                provider.entries.Enqueue(new(category, logLevel, eventId, formatter(state, exception), exception,
                    fields, Activity.Current?.TraceId ?? default));
            }

            private static void AddFields(object? state, Dictionary<string, object?> fields)
            {
                if (state is IEnumerable<KeyValuePair<string, object?>> values)
                    foreach (var value in values) fields[value.Key] = value.Value;
            }
        }
    }
}
