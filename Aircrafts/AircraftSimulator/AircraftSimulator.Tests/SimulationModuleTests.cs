using AircraftSimulator.Core;
using AircraftSimulator.Plugins;
using Xunit;

namespace AircraftSimulator.Tests;

public sealed class SimulationModuleTests
{
    [Fact]
    public void DistanceMeters_ReturnsSphericalDistance()
    {
        var distance = GeographicMath.DistanceMeters(0, 0, 0, 1);

        Assert.InRange(distance, 111_000, 112_000);
    }

    [Fact]
    public void BearingDegrees_ReturnsEastForEquatorialPoint()
    {
        var bearing = GeographicMath.BearingDegrees(0, 0, 0, 1);

        Assert.Equal(90, bearing, 5);
    }

    [Fact]
    public async Task MovementModule_UsesElapsedDeltaTimeToUpdatePosition()
    {
        var state = new AircraftState(1, 2, 0, 0, 1000, 360, 90, FlightStatus.EnRoute);
        var waypoint = new Waypoint(1, 1, 0);
        var context = new AircraftSimulationContext(state, [waypoint]);
        context.AdvanceToNextWaypoint();
        context.DesiredHeadingDegrees = 90;
        var movement = new MovementModule(new MovementOptions(
            TargetSpeedKmh: 360,
            AccelerationKmhPerSecond: 1,
            MaximumTurnDegreesPerSecond: 0.1,
            ClimbRateMetersPerSecond: 0));

        await movement.ExecuteAsync(context, TimeSpan.FromSeconds(2), CancellationToken.None);

        var traveled = GeographicMath.DistanceMeters(0, 0, state.Latitude, state.Longitude);
        Assert.InRange(traveled, 199.9, 200.1);
    }

    [Fact]
    public async Task NavigationModule_AdvancesToNextWaypointWhenCurrentIsReached()
    {
        var first = new Waypoint(10, 50.001, 30);
        var second = new Waypoint(11, 50.002, 30);
        var state = new AircraftState(1, 2, 50, 30, 1000, 500, 0, FlightStatus.EnRoute);
        var context = new AircraftSimulationContext(state, [first, second]);
        var navigation = new NavigationModule(
            new NavigationOptions(WaypointArrivalRadiusMeters: 1),
            new MovementOptions());
        await navigation.InitializeAsync(context, CancellationToken.None);

        state.Latitude = first.Latitude;
        state.Longitude = first.Longitude;
        await navigation.ExecuteAsync(context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(second.Id, state.TargetWaypointId);
        Assert.Same(second, context.CurrentWaypoint);
    }

    [Fact]
    public async Task NavigationModule_MarksAircraftLandedAfterFinalWaypoint()
    {
        var waypoint = new Waypoint(10, 50.001, 30);
        var state = new AircraftState(1, 2, 50, 30, 1000, 500, 0, FlightStatus.EnRoute);
        var context = new AircraftSimulationContext(state, [waypoint]);
        var navigation = new NavigationModule(
            new NavigationOptions(WaypointArrivalRadiusMeters: 1),
            new MovementOptions());
        await navigation.InitializeAsync(context, CancellationToken.None);

        state.Latitude = waypoint.Latitude;
        state.Longitude = waypoint.Longitude;
        await navigation.ExecuteAsync(context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(FlightStatus.RouteCompleted, state.Status);
        Assert.Null(state.TargetWaypointId);
    }

    [Fact]
    public async Task NavigationModule_SlowsForLargeInitialHeadingErrorAndMovementTurnsGradually()
    {
        var state = new AircraftState(1, 2, 0, 0, 1000, 650, 180, FlightStatus.EnRoute);
        var context = new AircraftSimulationContext(state, [new Waypoint(1, 1, 0)]);
        var movementOptions = new MovementOptions();
        var navigation = new NavigationModule(new NavigationOptions(), movementOptions);
        var movement = new MovementModule(movementOptions);
        await navigation.InitializeAsync(context, CancellationToken.None);

        await navigation.ExecuteAsync(context, TimeSpan.FromSeconds(1), CancellationToken.None);
        var oldHeading = state.HeadingDegrees;
        await movement.ExecuteAsync(context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(movementOptions.MaximumTurnDegreesPerSecond, GeographicMath.AbsoluteAngleDifferenceDegrees(oldHeading, state.HeadingDegrees), 5);
        Assert.Equal(160, context.DesiredSpeedKmh);
        Assert.True(state.SpeedKmh < 650);
    }

    [Fact]
    public async Task NavigationModule_CapturesPassedWaypointAndCompletesFollowingWaypoint()
    {
        var first = new Waypoint(10, 0, 0);
        var second = new Waypoint(11, 0.002, 0);
        var state = new AircraftState(1, 2, -0.001, 0, 1000, 0, 0, FlightStatus.EnRoute);
        var context = new AircraftSimulationContext(state, [first, second]);
        var navigation = new NavigationModule(
            new NavigationOptions(WaypointArrivalRadiusMeters: 20),
            new MovementOptions());
        await navigation.InitializeAsync(context, CancellationToken.None);

        state.Latitude = 0.0005;
        await navigation.ExecuteAsync(context, TimeSpan.FromSeconds(1), CancellationToken.None);
        Assert.Equal(second.Id, state.TargetWaypointId);

        state.Latitude = 0.003;
        await navigation.ExecuteAsync(context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(FlightStatus.RouteCompleted, state.Status);
    }

    [Fact]
    public async Task LocalConfigurationSource_RejectsUndefinedFlightStatus()
    {
        await AssertInvalidConfigurationAsync("""
            { "AircraftId": 1, "FlightId": 2, "Latitude": 0, "Longitude": 0,
              "AltitudeMeters": 1000, "SpeedKmh": 100, "HeadingDegrees": 0,
              "Status": 999, "Route": [{ "Id": 1, "Latitude": 1, "Longitude": 1 }] }
            """);
    }

    [Fact]
    public async Task LocalConfigurationSource_RejectsNullWaypoint()
    {
        await AssertInvalidConfigurationAsync("""
            { "AircraftId": 1, "FlightId": 2, "Latitude": 0, "Longitude": 0,
              "AltitudeMeters": 1000, "SpeedKmh": 100, "HeadingDegrees": 0,
              "Status": "EnRoute", "Route": [null] }
            """);
    }

    [Fact]
    public async Task NavigationModule_RejectsInvalidNavigationOptions()
    {
        var navigation = new NavigationModule(
            new NavigationOptions(WaypointArrivalRadiusMeters: 0),
            new MovementOptions());
        var context = new AircraftSimulationContext(
            new AircraftState(1, 2, 0, 0, 0, 0, 0, FlightStatus.EnRoute),
            [new Waypoint(1, 1, 1)]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => navigation.InitializeAsync(context, CancellationToken.None));
    }

    private static async Task AssertInvalidConfigurationAsync(string json)
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, json);
            var source = new LocalConfigurationSource(path);
            await Assert.ThrowsAsync<InvalidDataException>(() => source.LoadAsync(CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void TurnTowards_RespectsMaximumTurnAndTakesShortestDirection()
    {
        var turned = GeographicMath.TurnTowards(0, 90, 3);
        var acrossNorth = GeographicMath.TurnTowards(359, 1, 3);

        Assert.Equal(3, turned, 5);
        Assert.Equal(1, acrossNorth, 5);
    }
}
