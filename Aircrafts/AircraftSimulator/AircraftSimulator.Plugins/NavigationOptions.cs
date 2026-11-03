namespace AircraftSimulator.Plugins;

public sealed record NavigationOptions(
    double WaypointArrivalRadiusMeters = 200,
    double MaximumTurnRadiusCaptureFactor = 1.25,
    double ApproachDistanceMeters = 5_000,
    double MinimumApproachSpeedKmh = 160)
{
    public void Validate(MovementOptions movementOptions)
    {
        if (!double.IsFinite(WaypointArrivalRadiusMeters) || WaypointArrivalRadiusMeters <= 0
            || !double.IsFinite(MaximumTurnRadiusCaptureFactor) || MaximumTurnRadiusCaptureFactor < 1
            || !double.IsFinite(ApproachDistanceMeters) || ApproachDistanceMeters <= WaypointArrivalRadiusMeters
            || !double.IsFinite(MinimumApproachSpeedKmh) || MinimumApproachSpeedKmh <= 0
            || MinimumApproachSpeedKmh > movementOptions.TargetSpeedKmh)
        {
            throw new InvalidOperationException("Navigation options require a positive arrival radius, capture factor >= 1, approach distance greater than the arrival radius, and minimum approach speed between zero and target speed.");
        }
    }
}
