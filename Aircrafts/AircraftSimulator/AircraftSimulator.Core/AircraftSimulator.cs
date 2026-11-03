namespace AircraftSimulator.Core;

public sealed class AircraftSimulator : IAircraftSimulator
{
    private readonly IAircraftConfigurationSource _configurationSource;
    private readonly IReadOnlyList<IAircraftModule> _modules;
    private readonly ISimulationClock _clock;

    public AircraftSimulator(
        IAircraftConfigurationSource configurationSource,
        IEnumerable<IAircraftModule> modules,
        ISimulationClock clock)
    {
        _configurationSource = configurationSource;
        _modules = modules.OrderBy(module => module.ExecutionOrder).ToArray();
        _clock = clock;
    }

    public async Task RunAsync(TimeSpan updateInterval, CancellationToken cancellationToken)
    {
        if (updateInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(updateInterval), "The simulation update interval must be greater than zero.");
        }

        var configuration = await _configurationSource.LoadAsync(cancellationToken);
        var context = new AircraftSimulationContext(configuration.InitialState, configuration.Route);
        var initializedModules = new List<IAircraftModule>();
        Exception? runError = null;
        var shutdownErrors = new List<Exception>();

        try
        {
            foreach (var module in _modules)
            {
                try
                {
                    await module.InitializeAsync(context, cancellationToken);
                    initializedModules.Add(module);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new AircraftModuleException(module.GetType().Name, "initialization", exception);
                }
            }

            while (!cancellationToken.IsCancellationRequested
                && context.State.Status is not (FlightStatus.Landed or FlightStatus.RouteCompleted))
            {
                var deltaTime = await _clock.WaitForNextTickAsync(updateInterval, cancellationToken);
                if (deltaTime < TimeSpan.Zero)
                {
                    throw new InvalidOperationException("The simulation clock returned a negative elapsed time.");
                }

                foreach (var module in _modules)
                {
                    try
                    {
                        await module.ExecuteAsync(context, deltaTime, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        throw new AircraftModuleException(module.GetType().Name, "execution", exception);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            runError = exception;
        }
        finally
        {
            for (var index = initializedModules.Count - 1; index >= 0; index--)
            {
                var module = initializedModules[index];
                try
                {
                    await module.ShutdownAsync(context, CancellationToken.None);
                }
                catch (Exception exception)
                {
                    shutdownErrors.Add(new AircraftModuleException(module.GetType().Name, "shutdown", exception));
                }
            }
        }

        if (runError is not null && shutdownErrors.Count > 0)
        {
            throw new AggregateException("The simulation failed and one or more modules also failed to shut down.", [runError, .. shutdownErrors]);
        }

        if (runError is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(runError).Throw();
        }

        if (shutdownErrors.Count > 0)
        {
            throw new AggregateException("One or more aircraft modules failed to shut down.", shutdownErrors);
        }
    }
}
