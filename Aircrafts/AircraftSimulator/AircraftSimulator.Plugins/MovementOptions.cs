namespace AircraftSimulator.Plugins;

public sealed record MovementOptions(
    double TargetSpeedKmh = 800,
    double AccelerationKmhPerSecond = 5,
    double MaximumTurnDegreesPerSecond = 3,
    double ClimbRateMetersPerSecond = 10,
    double DecelerationKmhPerSecond = 15)
{
    public void Validate()
    {
        if (!double.IsFinite(TargetSpeedKmh) || TargetSpeedKmh <= 0
            || !double.IsFinite(AccelerationKmhPerSecond) || AccelerationKmhPerSecond <= 0
            || !double.IsFinite(MaximumTurnDegreesPerSecond) || MaximumTurnDegreesPerSecond is <= 0 or > 30
            || !double.IsFinite(ClimbRateMetersPerSecond) || ClimbRateMetersPerSecond < 0
            || !double.IsFinite(DecelerationKmhPerSecond) || DecelerationKmhPerSecond <= 0)
        {
            throw new InvalidOperationException("Movement options require positive target speed, acceleration, deceleration, and turn rate (at most 30 degrees/second); climb rate must be non-negative.");
        }
    }
}
