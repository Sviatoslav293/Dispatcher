using AircraftSimulator.Core;

namespace AircraftSimulator.Plugins;

public sealed class MovementModule(MovementOptions? options = null) : IAircraftModule
{
    private readonly MovementOptions _options = options ?? new MovementOptions();

    public int ExecutionOrder => 200;

    public Task InitializeAsync(IAircraftSimulationContext context, CancellationToken cancellationToken)
    {
        _options.Validate();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task ExecuteAsync(IAircraftSimulationContext context, TimeSpan deltaTime, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.State.Status != FlightStatus.EnRoute || deltaTime <= TimeSpan.Zero)
        {
            return Task.CompletedTask;
        }

        var seconds = deltaTime.TotalSeconds;
        var targetSpeedKmh = context.DesiredSpeedKmh ?? _options.TargetSpeedKmh;
        var speedChangeRate = targetSpeedKmh < context.State.SpeedKmh
            ? _options.DecelerationKmhPerSecond
            : _options.AccelerationKmhPerSecond;
        context.State.SpeedKmh = MoveTowards(
            context.State.SpeedKmh,
            targetSpeedKmh,
            speedChangeRate * seconds);
        if (context.DesiredHeadingDegrees is { } desiredHeading)
        {
            context.State.HeadingDegrees = GeographicMath.TurnTowards(
                context.State.HeadingDegrees,
                desiredHeading,
                _options.MaximumTurnDegreesPerSecond * seconds);
        }

        var targetAltitude = context.HasManualAltitudeTarget
            ? context.DesiredAltitudeMeters
            : context.CurrentWaypoint?.TargetAltitudeMeters;
        if (targetAltitude is { } altitude)
        {
            context.State.AltitudeMeters = MoveTowards(
                context.State.AltitudeMeters,
                altitude,
                _options.ClimbRateMetersPerSecond * seconds);
        }

        if (context.CurrentWaypoint is not { } waypoint)
        {
            return Task.CompletedTask;
        }

        var distanceMeters = context.State.SpeedKmh / 3.6 * seconds;
        var distanceToWaypoint = GeographicMath.DistanceMeters(
            context.State.Latitude,
            context.State.Longitude,
            waypoint.Latitude,
            waypoint.Longitude);
        distanceMeters = Math.Min(distanceMeters, distanceToWaypoint);
        var destination = GeographicMath.DestinationPoint(
            context.State.Latitude,
            context.State.Longitude,
            context.State.HeadingDegrees,
            distanceMeters);
        context.State.Latitude = destination.Latitude;
        context.State.Longitude = destination.Longitude;
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(IAircraftSimulationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    private static double MoveTowards(double current, double target, double maximumDelta)
    {
        if (Math.Abs(target - current) <= maximumDelta)
        {
            return target;
        }

        return current + Math.Sign(target - current) * maximumDelta;
    }
}
