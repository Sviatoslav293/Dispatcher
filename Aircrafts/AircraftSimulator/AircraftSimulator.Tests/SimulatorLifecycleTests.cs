using AircraftSimulator.Core;
using Xunit;

namespace AircraftSimulator.Tests;

public sealed class SimulatorLifecycleTests
{
    [Fact]
    public async Task Simulator_ExecutesModulesByOrderAndShutsThemDownInReverseOrder()
    {
        var events = new List<string>();
        var simulator = new AircraftSimulator.Core.AircraftSimulator(
            new TestConfigurationSource(),
            [new TrackingModule(3, events), new TrackingModule(1, events), new TrackingModule(2, events)],
            new FixedSimulationClock());

        await simulator.RunAsync(TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(
            ["initialize-1", "initialize-2", "initialize-3", "execute-1", "execute-2", "execute-3", "shutdown-3", "shutdown-2", "shutdown-1"],
            events);
    }

    [Fact]
    public async Task Simulator_ShutsDownAllInitializedModulesAfterExecutionFailure()
    {
        var events = new List<string>();
        var simulator = new AircraftSimulator.Core.AircraftSimulator(
            new TestConfigurationSource(),
            [new TrackingModule(1, events), new FailingModule(events), new TrackingModule(3, events)],
            new FixedSimulationClock());

        var exception = await Assert.ThrowsAsync<AircraftModuleException>(
            () => simulator.RunAsync(TimeSpan.FromSeconds(1), CancellationToken.None));

        Assert.Equal(nameof(FailingModule), exception.ModuleName);
        Assert.Equal(
            ["initialize-1", "initialize-2", "initialize-3", "execute-1", "execute-2", "shutdown-3", "shutdown-2", "shutdown-1"],
            events);
    }

    private sealed class TestConfigurationSource : IAircraftConfigurationSource
    {
        public Task<AircraftConfiguration> LoadAsync(CancellationToken cancellationToken)
        {
            var state = new AircraftState(1, 2, 0, 0, 1000, 0, 0, FlightStatus.EnRoute);
            return Task.FromResult(new AircraftConfiguration(state, []));
        }
    }

    private sealed class FixedSimulationClock : ISimulationClock
    {
        public ValueTask<TimeSpan> WaitForNextTickAsync(TimeSpan updateInterval, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(TimeSpan.FromSeconds(1));
        }
    }

    private sealed class TrackingModule(int executionOrder, List<string> events) : IAircraftModule
    {
        public int ExecutionOrder => executionOrder;

        public Task InitializeAsync(IAircraftSimulationContext context, CancellationToken cancellationToken)
        {
            events.Add($"initialize-{executionOrder}");
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(IAircraftSimulationContext context, TimeSpan deltaTime, CancellationToken cancellationToken)
        {
            events.Add($"execute-{executionOrder}");
            if (executionOrder == 3)
            {
                context.State.Status = FlightStatus.RouteCompleted;
            }

            return Task.CompletedTask;
        }

        public Task ShutdownAsync(IAircraftSimulationContext context, CancellationToken cancellationToken)
        {
            events.Add($"shutdown-{executionOrder}");
            return Task.CompletedTask;
        }
    }

    private sealed class FailingModule(List<string> events) : IAircraftModule
    {
        public int ExecutionOrder => 2;

        public Task InitializeAsync(IAircraftSimulationContext context, CancellationToken cancellationToken)
        {
            events.Add("initialize-2");
            return Task.CompletedTask;
        }

        public Task ExecuteAsync(IAircraftSimulationContext context, TimeSpan deltaTime, CancellationToken cancellationToken)
        {
            events.Add("execute-2");
            return Task.FromException(new InvalidOperationException("Expected test failure."));
        }

        public Task ShutdownAsync(IAircraftSimulationContext context, CancellationToken cancellationToken)
        {
            events.Add("shutdown-2");
            return Task.CompletedTask;
        }
    }
}
