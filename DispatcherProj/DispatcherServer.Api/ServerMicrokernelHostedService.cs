using DispatcherServer.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DispatcherServer.Api;

public sealed class ServerMicrokernelHostedService(
    ServerMicrokernel microkernel,
    ILogger<ServerMicrokernelHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await microkernel.InitializeAsync(cancellationToken);
            logger.LogInformation("DispatcherServer microkernel initialized.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "DispatcherServer microkernel initialization failed.");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await microkernel.ShutdownAsync(cancellationToken);
            logger.LogInformation("DispatcherServer microkernel shut down.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "DispatcherServer microkernel shutdown failed.");
            throw;
        }
    }
}
