using System.Collections.Concurrent;
using AircraftSimulator.Core;
using AircraftSimulator.Plugins;
using Xunit;

namespace AircraftSimulator.Tests;

public sealed class SimulationIntegrationTests
{
    [Fact]
    public async Task Simulator_CompletesRouteWithScriptedTimeWithoutRealDelay()
    {
        var state = new AircraftState(1, 2, 0, 0, 1000, 36, 0, FlightStatus.EnRoute);
        var configuration = new AircraftConfiguration(state, [new Waypoint(1, 0.0001, 0)]);
        var publisher = new CollectingTelemetryPublisher();
        var movementOptions = new MovementOptions(
            TargetSpeedKmh: 36,
            AccelerationKmhPerSecond: 5,
            MaximumTurnDegreesPerSecond: 30,
            ClimbRateMetersPerSecond: 0,
            DecelerationKmhPerSecond: 15);
        var navigationOptions = new NavigationOptions(
            WaypointArrivalRadiusMeters: 5,
            ApproachDistanceMeters: 100,
            MinimumApproachSpeedKmh: 10);
        var simulator = new AircraftSimulator.Core.AircraftSimulator(
            new FixedConfigurationSource(configuration),
            [
                new NavigationModule(navigationOptions, movementOptions),
                new MovementModule(movementOptions),
                new TelemetryModule(publisher)
            ],
            new ScriptedSimulationClock(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)));

        await simulator.RunAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(FlightStatus.RouteCompleted, state.Status);
        Assert.Null(state.TargetWaypointId);
        Assert.Equal(3, publisher.Messages.Count);
        Assert.NotEqual(0, state.Latitude);
    }

    private sealed class FixedConfigurationSource(AircraftConfiguration configuration) : IAircraftConfigurationSource
    {
        public Task<AircraftConfiguration> LoadAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(configuration);
        }
    }

    private sealed class ScriptedSimulationClock(params TimeSpan[] elapsedTicks) : ISimulationClock
    {
        private readonly ConcurrentQueue<TimeSpan> _elapsedTicks = new(elapsedTicks);

        public ValueTask<TimeSpan> WaitForNextTickAsync(TimeSpan updateInterval, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _elapsedTicks.TryDequeue(out var elapsed)
                ? ValueTask.FromResult(elapsed)
                : ValueTask.FromException<TimeSpan>(new InvalidOperationException("The scripted simulation clock ran out of ticks."));
        }
    }

    private sealed class CollectingTelemetryPublisher : ITelemetryPublisher
    {
        public List<AircraftTelemetry> Messages { get; } = [];

        public Task PublishAsync(AircraftTelemetry telemetry, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Messages.Add(telemetry);
            return Task.CompletedTask;
        }
    }
}
