using DispatcherServer.Core;
using DispatcherServer.Core.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DispatcherServer.Api;

public sealed class AircraftPositionPersistenceHostedService(
    IAircraftStateStore stateStore,
    IAircraftPositionPersistence persistence,
    ILogger<AircraftPositionPersistenceHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PersistenceInterval = TimeSpan.FromSeconds(10);

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var retries = 0;
            while (true)
            {
                try
                {
                    var latestPositions = await persistence.LoadLatestPositionsAsync(cancellationToken);
                    foreach (var position in latestPositions)
                    {
                        stateStore.Restore(position);
                    }

                    logger.LogInformation("Restored {PositionCount} historical aircraft positions.", latestPositions.Count);
                    break;
                }
                catch (Exception exception) when (exception is not OperationCanceledException && retries < 5)
                {
                    retries++;
                    logger.LogWarning(exception, "Could not restore aircraft positions from SQL Server; retrying startup in 2 seconds ({Attempt}/5).", retries);
                    await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Could not restore aircraft positions from SQL Server.");
            throw;
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PersistenceInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await SaveLatestPositionsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Periodic aircraft position persistence failed; the next sample will retry it.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        using var shutdownTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        shutdownTimeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await SaveLatestPositionsAsync(shutdownTimeout.Token);
        }
        catch (OperationCanceledException) when (shutdownTimeout.IsCancellationRequested)
        {
            logger.LogCritical("Final aircraft position flush was interrupted; persisted positions may be stale.");
            throw;
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Final aircraft position flush failed; persisted positions may be stale.");
            throw;
        }
    }

    private async Task SaveLatestPositionsAsync(CancellationToken cancellationToken)
    {
        var latestPositions = stateStore.GetAll();
        if (latestPositions.Count == 0)
        {
            return;
        }

        try
        {
            await persistence.SaveLatestPositionsAsync(latestPositions, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not persist {PositionCount} latest aircraft positions.", latestPositions.Count);
            throw;
        }
    }
}
