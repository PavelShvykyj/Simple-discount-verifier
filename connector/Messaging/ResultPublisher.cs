using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Sdv.Connector.Configuration;

namespace Sdv.Connector.Messaging;

internal interface IResultPublisher
{
    Task PublishAsync(byte[] body, string eventId, string commandId, TimeSpan timeToLive, CancellationToken cancel);
}

/// <summary>Sends result envelopes to the shared results queue. Retries are disabled: a send failure is reported, never repeated.</summary>
internal sealed class ResultPublisher(IOptions<ConnectorOptions> options) : IResultPublisher, IAsyncDisposable
{
    readonly object sync = new();
    ServiceBusClient? client;
    ServiceBusSender? sender;

    public Task PublishAsync(byte[] body, string eventId, string commandId, TimeSpan timeToLive, CancellationToken cancel)
    {
        var message = new ServiceBusMessage(new BinaryData(body))
        {
            MessageId = eventId,
            CorrelationId = commandId,
            ContentType = "application/json",
            TimeToLive = timeToLive
        };
        return GetSender().SendMessageAsync(message, cancel);
    }

    ServiceBusSender GetSender()
    {
        lock (sync)
        {
            if (sender is not null) return sender;
            client = new ServiceBusClient(options.Value.ServiceBus.ResultSendConnectionString, new ServiceBusClientOptions
            {
                TransportType = ServiceBusTransportType.AmqpTcp,
                RetryOptions = new ServiceBusRetryOptions { MaxRetries = 0 }
            });
            return sender = client.CreateSender("results");
        }
    }

    public async ValueTask DisposeAsync()
    {
        ServiceBusSender? s; ServiceBusClient? c;
        lock (sync) { s = sender; c = client; sender = null; client = null; }
        if (s is not null) await s.DisposeAsync();
        if (c is not null) await c.DisposeAsync();
    }
}
