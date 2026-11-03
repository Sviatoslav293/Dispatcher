using AircraftSimulator.Core;

namespace AircraftSimulator.Plugins;

public sealed class SystemSimulationClock : ISimulationClock
{
    public async ValueTask<TimeSpan> WaitForNextTickAsync(
        TimeSpan updateInterval,
        CancellationToken cancellationToken)
    {
        await Task.Delay(updateInterval, cancellationToken);
        return updateInterval;
    }
}
