using AircraftSimulator.Core;

namespace AircraftSimulator.Plugins;

public sealed class NavigationModule(
    NavigationOptions? navigationOptions = null,
    MovementOptions? movementOptions = null) : IAircraftModule
{
    private readonly NavigationOptions _navigationOptions = navigationOptions ?? new NavigationOptions();
    private readonly MovementOptions _movementOptions = movementOptions ?? new MovementOptions();
    private double _previousLatitude;
    private double _previousLongitude;
    private double _previousRouteLatitude;
    private double _previousRouteLongitude;

    public int ExecutionOrder => 100;

    public Task InitializeAsync(IAircraftSimulationContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _movementOptions.Validate();
        _navigationOptions.Validate(_movementOptions);
        _previousLatitude = context.State.Latitude;
        _previousLongitude = context.State.Longitude;
        _previousRouteLatitude = context.State.Latitude;
        _previousRouteLongitude = context.State.Longitude;
        if (context.Route.Count == 0)
        {
            context.State.Status = FlightStatus.RouteCompleted;
            return Task.CompletedTask;
        }

        context.AdvanceToNextWaypoint();
        if (context.State.Status == FlightStatus.Grounded)
        {
            context.State.Status = FlightStatus.EnRoute;
        }

        return Task.CompletedTask;
    }

    public Task ExecuteAsync(IAircraftSimulationContext context, TimeSpan deltaTime, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (context.State.Status != FlightStatus.EnRoute)
        {
            return Task.CompletedTask;
        }

        var manualHeadingActive = false;
        if (context.ManualHeadingExpiresAtUtc is { } manualHeadingExpiresAtUtc)
        {
            if (DateTimeOffset.UtcNow < manualHeadingExpiresAtUtc)
            {
                manualHeadingActive = true;
            }
            else
            {
                context.ManualHeadingExpiresAtUtc = null;
            }
        }

        while (context.CurrentWaypoint is { } waypoint)
        {
            var distance = GeographicMath.DistanceMeters(
                context.State.Latitude,
                context.State.Longitude,
                waypoint.Latitude,
                waypoint.Longitude);
            var turnRateRadiansPerSecond = _movementOptions.MaximumTurnDegreesPerSecond * Math.PI / 180;
            var turnRadiusMeters = context.State.SpeedKmh / 3.6 / turnRateRadiansPerSecond;
            var passedCorridorMeters = Math.Max(
                _navigationOptions.WaypointArrivalRadiusMeters,
                turnRadiusMeters * _navigationOptions.MaximumTurnRadiusCaptureFactor);
            var crossedWaypointWithinArrivalRadius = GeographicMath.DistanceToSegmentMeters(
                waypoint.Latitude,
                waypoint.Longitude,
                _previousLatitude,
                _previousLongitude,
                context.State.Latitude,
                context.State.Longitude) <= _navigationOptions.WaypointArrivalRadiusMeters;
            var passedWaypoint = IsPastWaypoint(context.State, waypoint, passedCorridorMeters);

            if (distance > _navigationOptions.WaypointArrivalRadiusMeters
                && !crossedWaypointWithinArrivalRadius
                && !passedWaypoint)
            {
                break;
            }

            _previousRouteLatitude = waypoint.Latitude;
            _previousRouteLongitude = waypoint.Longitude;
            if (!context.AdvanceToNextWaypoint())
            {
                context.State.Status = FlightStatus.RouteCompleted;
                context.DesiredHeadingDegrees = null;
                context.DesiredSpeedKmh = null;
                _previousLatitude = context.State.Latitude;
                _previousLongitude = context.State.Longitude;
                return Task.CompletedTask;
            }
        }

        if (context.CurrentWaypoint is { } target)
        {
            var distance = GeographicMath.DistanceMeters(
                context.State.Latitude,
                context.State.Longitude,
                target.Latitude,
                target.Longitude);
            var desiredHeading = GeographicMath.BearingDegrees(
                context.State.Latitude,
                context.State.Longitude,
                target.Latitude,
                target.Longitude);
            var headingError = GeographicMath.AbsoluteAngleDifferenceDegrees(context.State.HeadingDegrees, desiredHeading);
            var distanceFactor = Math.Clamp(distance / _navigationOptions.ApproachDistanceMeters, 0, 1);
            var turnFactor = Math.Clamp((90 - headingError) / 90, 0, 1);

            if (!manualHeadingActive)
            {
                context.DesiredHeadingDegrees = desiredHeading;
            }
            if (!context.HasManualSpeedTarget)
            {
                context.DesiredSpeedKmh = _navigationOptions.MinimumApproachSpeedKmh
                    + (_movementOptions.TargetSpeedKmh - _navigationOptions.MinimumApproachSpeedKmh) * Math.Min(distanceFactor, turnFactor);
            }
        }

        _previousLatitude = context.State.Latitude;
        _previousLongitude = context.State.Longitude;
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(IAircraftSimulationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    private bool IsPastWaypoint(AircraftState state, Waypoint waypoint, double crossTrackCorridorMeters)
    {
        var inboundBearing = GeographicMath.BearingDegrees(
            _previousRouteLatitude,
            _previousRouteLongitude,
            waypoint.Latitude,
            waypoint.Longitude);
        var distanceFromWaypoint = GeographicMath.DistanceMeters(
            waypoint.Latitude,
            waypoint.Longitude,
            state.Latitude,
            state.Longitude);
        var bearingFromWaypoint = GeographicMath.BearingDegrees(
            waypoint.Latitude,
            waypoint.Longitude,
            state.Latitude,
            state.Longitude);
        var angleFromInbound = (bearingFromWaypoint - inboundBearing) * Math.PI / 180;
        var alongTrackMeters = distanceFromWaypoint * Math.Cos(angleFromInbound);
        var crossTrackMeters = Math.Abs(distanceFromWaypoint * Math.Sin(angleFromInbound));
        return alongTrackMeters > _navigationOptions.WaypointArrivalRadiusMeters
            && crossTrackMeters <= crossTrackCorridorMeters;
    }
}
