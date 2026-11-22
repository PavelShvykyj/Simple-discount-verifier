using System.Globalization;
using Azure;
using Azure.Core;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.WebPubSub;

namespace Sdv.Worker;

internal interface ITransport
{
    // False means the send was skipped. A rejected or unconfirmed attempt still returns true.
    Task<bool> Send(string branch, Command command, DateTimeOffset expiry, CancellationToken ct);
    Task Publish(Caller caller, byte[] body, CancellationToken ct);
}

internal sealed class AzureTransport : ITransport, IAsyncDisposable
{
    private readonly ServiceBusClient bus;
    private readonly Dictionary<string, ServiceBusSender> senders;
    private readonly WebPubSubServiceClient pubSub;
    private readonly TimeProvider clock;
    private readonly ILogger<AzureTransport> logger;

    internal AzureTransport(TokenCredential credential, string busNamespace, Uri pubSubEndpoint, string hub,
        WorkerOptions settings, TimeProvider clock, ILogger<AzureTransport> logger)
    {
        this.clock = clock;
        this.logger = logger;
        bus = new ServiceBusClient(busNamespace, credential,
            new ServiceBusClientOptions { RetryOptions = new ServiceBusRetryOptions { MaxRetries = 0 } });
        senders = settings.Queues.ToDictionary(branch => branch.Key, branch => bus.CreateSender(branch.Value));
        var options = new WebPubSubServiceClientOptions();
        options.Retry.MaxRetries = 0;
        options.Diagnostics.IsLoggingContentEnabled = false;
        pubSub = new WebPubSubServiceClient(pubSubEndpoint, hub, credential, options);
    }

    public async Task<bool> Send(string branch, Command command, DateTimeOffset expiry, CancellationToken ct)
    {
        var ttl = expiry - clock.GetUtcNow();
        if (ttl <= LookupTiming.SendReserve || ct.IsCancellationRequested)
        {
            WorkerLog.CommandSkipped(logger, command.Id);
            return false;
        }
        await TryEffect(logger, EffectKind.SendCommand, async () =>
        {
            var message = new ServiceBusMessage(new BinaryData(command.Body))
            {
                MessageId = command.Id, CorrelationId = command.Id, ContentType = "application/json",
                TimeToLive = ttl <= LookupTiming.MaxTurnDuration ? ttl : LookupTiming.MaxTurnDuration
            };
            await senders[branch].SendMessageAsync(message, ct);
            WorkerLog.CommandAccepted(logger, command.Id);
        });
        return true;
    }

    public Task Publish(Caller caller, byte[] body, CancellationToken ct) =>
        TryEffect(logger, EffectKind.PublishFinal, async () =>
        {
            using var content = RequestContent.Create(new BinaryData(body));
            await pubSub.SendToGroupAsync(Group(caller), content, ContentType.ApplicationJson,
                context: new RequestContext { CancellationToken = ct });
            WorkerLog.NotificationAccepted(logger);
        });

    internal static async Task TryEffect(ILogger logger, EffectKind kind, Func<Task> effect)
    {
        try { await effect(); }
        catch (Exception error) when (error is OperationCanceledException or IOException or RequestFailedException or
            System.ClientModel.ClientResultException or ServiceBusException or HttpRequestException or
            Azure.Identity.AuthenticationFailedException or TimeoutException)
        {
            switch (error)
            {
                case OperationCanceledException:
                    WorkerLog.EffectCancelled(logger, kind);
                    break;
                case ServiceBusException { Reason: ServiceBusFailureReason.MessagingEntityNotFound
                    or ServiceBusFailureReason.MessagingEntityDisabled or ServiceBusFailureReason.MessageSizeExceeded } rejectedSend
                    when kind == EffectKind.SendCommand:
                    WorkerLog.EffectRejected(logger, kind, rejectedSend.Reason.ToString(), rejectedSend);
                    break;
                case RequestFailedException { Status: StatusCodes.Status400BadRequest or StatusCodes.Status401Unauthorized
                    or StatusCodes.Status403Forbidden or StatusCodes.Status404NotFound } rejectedPublish
                    when kind == EffectKind.PublishFinal:
                    WorkerLog.EffectRejected(logger, kind, rejectedPublish.Status.ToString(CultureInfo.InvariantCulture), rejectedPublish);
                    break;
                default:
                    WorkerLog.EffectUnconfirmed(logger, kind, error);
                    break;
            }
            // An unsuccessful call never authorizes a second effect.
        }
    }

    internal static string Group(Caller caller) => "sdv-worker-" + Wire.Hash(Wire.Encode(new[] { caller.Owner, caller.Session }));
    public ValueTask DisposeAsync() => bus.DisposeAsync();
}
