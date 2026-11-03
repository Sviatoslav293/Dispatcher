namespace AircraftSimulator.Core;

public interface IAircraftSimulationContext
{
    AircraftState State { get; }
    IReadOnlyList<Waypoint> Route { get; }
    Waypoint? CurrentWaypoint { get; }
    double? DesiredHeadingDegrees { get; set; }
    double? DesiredAltitudeMeters { get; set; }
    bool HasManualAltitudeTarget { get; set; }
    double? DesiredSpeedKmh { get; set; }
    bool HasManualSpeedTarget { get; set; }
    DateTimeOffset? ManualHeadingExpiresAtUtc { get; set; }
    bool AdvanceToNextWaypoint();
}
