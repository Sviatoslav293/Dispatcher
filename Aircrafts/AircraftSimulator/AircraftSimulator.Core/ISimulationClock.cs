namespace AircraftSimulator.Core;

public interface ISimulationClock
{
    ValueTask<TimeSpan> WaitForNextTickAsync(TimeSpan updateInterval, CancellationToken cancellationToken);
}
