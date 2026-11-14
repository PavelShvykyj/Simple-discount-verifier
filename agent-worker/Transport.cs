using Azure;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.WebPubSub;

namespace Sdv.Worker;

internal interface ITransport
{
    Task<bool> Send(string branch, Command command, DateTimeOffset expiry, CancellationToken ct);
    Task Publish(Caller caller, byte[] body, CancellationToken ct);
}

internal sealed class AzureTransport : ITransport, IAsyncDisposable
{
    private readonly ServiceBusClient bus;
    private readonly Dictionary<string, ServiceBusSender> senders;
    private readonly WebPubSubServiceClient pubSub;
    private readonly TimeProvider clock;

    internal AzureTransport(TokenCredential credential, string busNamespace, Uri pubSubEndpoint, string hub,
        Settings settings, TimeProvider clock)
    {
        this.clock = clock;
        bus = new ServiceBusClient(busNamespace, credential,
            new ServiceBusClientOptions { RetryOptions = new ServiceBusRetryOptions { MaxRetries = 0 } });
        senders = settings.Queues.ToDictionary(branch => branch.Key, branch => bus.CreateSender(branch.Value));
        var options = new WebPubSubServiceClientOptions();
        options.Retry.MaxRetries = 0;
        options.Diagnostics.IsLoggingContentEnabled = false;
        options.Diagnostics.IsLoggingEnabled = false;
        options.Diagnostics.IsDistributedTracingEnabled = false;
        pubSub = new WebPubSubServiceClient(pubSubEndpoint, hub, credential, options);
    }

    public async Task<bool> Send(string branch, Command command, DateTimeOffset expiry, CancellationToken ct)
    {
        var ttl = expiry - clock.GetUtcNow();
        if (ttl <= TimeSpan.FromSeconds(15) || ct.IsCancellationRequested) return false;
        var message = new ServiceBusMessage(new BinaryData(command.Body))
        {
            MessageId = command.Id, CorrelationId = command.Id, ContentType = "application/json",
            TimeToLive = ttl <= TimeSpan.FromSeconds(120) ? ttl : TimeSpan.FromSeconds(120)
        };
        await senders[branch].SendMessageAsync(message, ct);
        return true;
    }

    public async Task Publish(Caller caller, byte[] body, CancellationToken ct)
    {
        using var content = RequestContent.Create(new BinaryData(body));
        await pubSub.SendToGroupAsync(Group(caller), content, ContentType.ApplicationJson,
            context: new RequestContext { CancellationToken = ct });
    }

    internal static string Group(Caller caller) => "sdv-worker-" + Wire.Hash(Wire.Encode(new[] { caller.Owner, caller.Session }));
    public ValueTask DisposeAsync() => bus.DisposeAsync();
}
