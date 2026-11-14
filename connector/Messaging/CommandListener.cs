using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sdv.Connector.Configuration;

namespace Sdv.Connector.Messaging;

internal interface ICommandListener
{
    Task StartAsync(CancellationToken cancel);
    Task StopAsync(CancellationToken cancel);
    /// <summary>Stops listening and drops the Service Bus connection; the next start reconnects.</summary>
    Task ResetAsync(CancellationToken cancel);
}

/// <summary>
/// Receives commands with a ServiceBusProcessor in ReceiveAndDelete mode, one message at a time.
/// ReceiveAndDelete means a received command is never redelivered, so it is processed exactly once or not at all.
/// </summary>
internal sealed class CommandListener(
    IOptions<ConnectorOptions> options,
    CommandProcessor processor,
    ListenerHealth health,
    IHostApplicationLifetime lifetime,
    ILogger<CommandListener> logger) : ICommandListener, IAsyncDisposable
{
    readonly SemaphoreSlim gate = new(1, 1);
    ServiceBusClient? client;
    ServiceBusProcessor? busProcessor;

    public async Task StartAsync(CancellationToken cancel)
    {
        await gate.WaitAsync(cancel);
        try
        {
            if (busProcessor is null)
            {
                var settings = options.Value;
                client = new ServiceBusClient(settings.ServiceBus.CommandListenConnectionString,
                    new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpTcp });
                busProcessor = client.CreateProcessor("commands" + settings.BranchId, new ServiceBusProcessorOptions
                {
                    ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
                    PrefetchCount = 0,
                    MaxConcurrentCalls = 1,
                    AutoCompleteMessages = false
                });
                busProcessor.ProcessMessageAsync += HandleMessageAsync;
                busProcessor.ProcessErrorAsync += HandleErrorAsync;
            }
            if (!busProcessor.IsProcessing)
            {
                health.SetState(ListenerState.Starting);
                await busProcessor.StartProcessingAsync(cancel);
                logger.LogInformation("Command listener started");
            }
            health.SetState(ListenerState.Listening);
        }
        finally { gate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancel)
    {
        await gate.WaitAsync(cancel);
        try
        {
            if (busProcessor is { IsProcessing: true })
            {
                await busProcessor.StopProcessingAsync(cancel);
                logger.LogInformation("Command listener stopped");
            }
        }
        finally { gate.Release(); }
    }

    public async Task ResetAsync(CancellationToken cancel)
    {
        await gate.WaitAsync(cancel);
        try { await DisposeCoreAsync(); }
        finally { gate.Release(); }
    }

    Task HandleMessageAsync(ProcessMessageEventArgs args)
    {
        health.MarkReceived();
        // The application token is used instead of args.CancellationToken so that stopping the listener
        // lets an already received (and deleted) command finish.
        return processor.ProcessAsync(args.Message.Body.ToArray(), args.Message.ContentType, lifetime.ApplicationStopping);
    }

    Task HandleErrorAsync(ProcessErrorEventArgs args)
    {
        if (args.Exception is OperationCanceledException) return Task.CompletedTask;
        if (args.Exception is UnauthorizedAccessException)
        {
            health.MarkReceiveError("AccessDenied", faulted: true);
            logger.LogCritical("Service Bus access denied for the command queue; stopping the Connector");
            Environment.ExitCode = 1;
            lifetime.StopApplication();
        }
        else
        {
            health.MarkReceiveError("ReceiveFailed", faulted: false);
            logger.LogWarning("Command receive failed in {ErrorSource}: {ExceptionType}", args.ErrorSource, args.Exception.GetType().Name);
        }
        return Task.CompletedTask;
    }

    async Task DisposeCoreAsync()
    {
        var p = busProcessor; var c = client;
        busProcessor = null; client = null;
        if (p is not null) await p.DisposeAsync();
        if (c is not null) await c.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await gate.WaitAsync();
        try { await DisposeCoreAsync(); }
        finally { gate.Release(); }
    }
}
