namespace AircraftSimulator.Core;

public interface IAircraftModule
{
    int ExecutionOrder { get; }

    Task InitializeAsync(IAircraftSimulationContext context, CancellationToken cancellationToken);

    Task ExecuteAsync(IAircraftSimulationContext context, TimeSpan deltaTime, CancellationToken cancellationToken);

    Task ShutdownAsync(IAircraftSimulationContext context, CancellationToken cancellationToken);
}
