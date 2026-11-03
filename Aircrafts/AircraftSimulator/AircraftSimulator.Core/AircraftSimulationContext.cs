namespace AircraftSimulator.Core;

public sealed class AircraftSimulationContext : IAircraftSimulationContext
{
    private int _currentWaypointIndex = -1;

    public AircraftSimulationContext(AircraftState state, IReadOnlyList<Waypoint> route)
    {
        State = state;
        Route = route;
    }

    public AircraftState State { get; }
    public IReadOnlyList<Waypoint> Route { get; }
    public Waypoint? CurrentWaypoint => _currentWaypointIndex >= 0 && _currentWaypointIndex < Route.Count
        ? Route[_currentWaypointIndex]
        : null;
    public double? DesiredHeadingDegrees { get; set; }
    public double? DesiredAltitudeMeters { get; set; }
    public bool HasManualAltitudeTarget { get; set; }
    public double? DesiredSpeedKmh { get; set; }
    public bool HasManualSpeedTarget { get; set; }
    public DateTimeOffset? ManualHeadingExpiresAtUtc { get; set; }

    public bool AdvanceToNextWaypoint()
    {
        _currentWaypointIndex++;
        if (_currentWaypointIndex >= Route.Count)
        {
            State.TargetWaypointId = null;
            return false;
        }

        State.TargetWaypointId = Route[_currentWaypointIndex].Id;
        return true;
    }
}
