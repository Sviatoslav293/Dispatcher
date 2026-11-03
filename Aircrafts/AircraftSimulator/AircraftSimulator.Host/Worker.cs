using AircraftSimulator.Core;

namespace AircraftSimulator.Host
{
    public class Worker(
        IAircraftSimulator simulator,
        IConfiguration configuration,
        IHostApplicationLifetime applicationLifetime,
        ILogger<Worker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalSeconds = configuration.GetValue<double>("Simulation:UpdateIntervalSeconds", 1);
            if (!double.IsFinite(intervalSeconds) || intervalSeconds <= 0)
            {
                throw new InvalidOperationException("Simulation:UpdateIntervalSeconds must be a finite value greater than zero.");
            }

            logger.LogInformation("Starting aircraft simulation with update interval {UpdateIntervalSeconds} seconds.", intervalSeconds);
            try
            {
                await simulator.RunAsync(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                logger.LogInformation("Aircraft simulation completed.");
                applicationLifetime.StopApplication();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Aircraft simulation is shutting down.");
            }
            catch (Exception exception)
            {
                logger.LogCritical(exception, "Aircraft simulation stopped because of an unrecoverable error.");
                throw;
            }
        }
    }
}
