namespace AircraftSimulator.Core;

public interface IAircraftSimulator
{
    Task RunAsync(TimeSpan updateInterval, CancellationToken cancellationToken);
}
