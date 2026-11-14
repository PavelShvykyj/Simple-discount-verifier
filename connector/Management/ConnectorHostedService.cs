using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Sdv.Connector.Management;

/// <summary>Starts the listener on service start and closes the listener and the 1C session on service stop.</summary>
internal sealed class ConnectorHostedService(ManagementService management, ILogger<ConnectorHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await management.ReconcileAsync(cancellationToken);
        logger.LogInformation("Connector started: {Identity}", management.Identity);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try { await management.ShutdownAsync(cancellationToken); }
        catch (OperationCanceledException) { logger.LogWarning("Connector shutdown was interrupted"); }
        catch (Exception e) { logger.LogWarning("Connector shutdown cleanup failed: {ExceptionType}", e.GetType().Name); }
        logger.LogInformation("Connector stopped");
    }
}
