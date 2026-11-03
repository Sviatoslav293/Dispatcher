using System.Collections.Concurrent;
using AircraftSimulator.Core;
using AircraftSimulator.Plugins;
using Xunit;

namespace AircraftSimulator.Tests;

public sealed class CommandModuleTests
{
    [Fact]
    public async Task SetHeading_NormalizesAndAppliesTargetWithAppliedResult()
    {
        var harness = CreateHarness();
        var command = CreateCommand(AircraftCommandType.SetHeading, 450);

        harness.Queue.TryEnqueue(command);
        await harness.Module.ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(90, harness.Context.DesiredHeadingDegrees);
        Assert.NotNull(harness.Context.ManualHeadingExpiresAtUtc);
        var result = Assert.Single(harness.Publisher.Results);
        Assert.Equal(command.CommandId, result.CommandId);
        Assert.Equal(AircraftCommandResultStatus.Applied, result.Status);
    }

    [Fact]
    public async Task SetAltitude_SetsManualTargetAndMovementChangesGradually()
    {
        var harness = CreateHarness();
        harness.Context.State.AltitudeMeters = 1000;
        harness.Queue.TryEnqueue(CreateCommand(AircraftCommandType.SetAltitude, 2000));
        await harness.Module.ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);
        await new MovementModule(new MovementOptions(ClimbRateMetersPerSecond: 10))
            .ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(2000, harness.Context.DesiredAltitudeMeters);
        Assert.Equal(1010, harness.Context.State.AltitudeMeters);
    }

    [Fact]
    public async Task SetSpeed_SetsManualTargetAndMovementChangesGradually()
    {
        var harness = CreateHarness();
        harness.Context.State.SpeedKmh = 100;
        harness.Queue.TryEnqueue(CreateCommand(AircraftCommandType.SetSpeed, 200));
        await harness.Module.ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);
        await new MovementModule(new MovementOptions(AccelerationKmhPerSecond: 5))
            .ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.True(harness.Context.HasManualSpeedTarget);
        Assert.Equal(200, harness.Context.DesiredSpeedKmh);
        Assert.Equal(105, harness.Context.State.SpeedKmh);
    }

    [Fact]
    public async Task CommandWithWrongAircraftId_IsRejected()
    {
        var harness = CreateHarness();
        var command = CreateCommand(AircraftCommandType.SetSpeed, 200) with { AircraftId = 12 };
        harness.Queue.TryEnqueue(new AircraftCommandWorkItem(command, "AircraftId does not match this simulator."));

        Assert.Empty(harness.Publisher.Results);
        Assert.Null(harness.Context.DesiredSpeedKmh);

        await harness.Module.ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(AircraftCommandResultStatus.Rejected, Assert.Single(harness.Publisher.Results).Status);
        Assert.Null(harness.Context.DesiredSpeedKmh);
    }

    [Fact]
    public async Task ExpiredCommand_IsRejected()
    {
        var harness = CreateHarness();
        var expired = CreateCommand(AircraftCommandType.SetSpeed, 200) with
        {
            IssuedAtUtc = DateTime.UtcNow.AddMinutes(-2),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        harness.Queue.TryEnqueue(new AircraftCommandWorkItem(expired, "Command has expired."));

        Assert.Empty(harness.Publisher.Results);
        await harness.Module.ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(AircraftCommandResultStatus.Rejected, Assert.Single(harness.Publisher.Results).Status);
    }

    [Fact]
    public async Task DuplicateCommandId_IsAppliedOnlyOnceAndDuplicateIsRejected()
    {
        var harness = CreateHarness();
        var command = CreateCommand(AircraftCommandType.SetSpeed, 200);
        harness.Queue.TryEnqueue(command);
        harness.Queue.TryEnqueue(new AircraftCommandWorkItem(command, "Duplicate CommandId was already received."));

        Assert.Empty(harness.Publisher.Results);
        await harness.Module.ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);

        var results = harness.Publisher.Results.ToArray();
        Assert.Equal(2, results.Length);
        Assert.Equal(AircraftCommandResultStatus.Applied, results[0].Status);
        Assert.Equal(AircraftCommandResultStatus.Rejected, results[1].Status);
    }

    [Fact]
    public async Task ManualHeadingTarget_IsNotOverwrittenByNavigationUntilTimeout()
    {
        var harness = CreateHarness();
        var navigation = new NavigationModule(new NavigationOptions(), new MovementOptions());
        await navigation.InitializeAsync(harness.Context, CancellationToken.None);
        harness.Queue.TryEnqueue(CreateCommand(AircraftCommandType.SetHeading, 180));
        await harness.Module.ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);
        var commandedHeading = harness.Context.DesiredHeadingDegrees;

        await navigation.ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(commandedHeading, harness.Context.DesiredHeadingDegrees);
    }

    [Fact]
    public async Task MovementModule_TurnsGraduallyTowardCommandedHeading()
    {
        var harness = CreateHarness();
        harness.Context.DesiredHeadingDegrees = 90;
        await new MovementModule(new MovementOptions(MaximumTurnDegreesPerSecond: 3))
            .ExecuteAsync(harness.Context, TimeSpan.FromSeconds(2), CancellationToken.None);

        Assert.Equal(6, harness.Context.State.HeadingDegrees);
    }

    [Fact]
    public async Task ConcurrentEnqueue_DoesNotMutateStateUntilSimulationModuleRuns()
    {
        var harness = CreateHarness();
        var command = CreateCommand(AircraftCommandType.SetAltitude, 5000);
        await Task.Run(() => harness.Queue.TryEnqueue(command));

        Assert.Equal(1000, harness.Context.State.AltitudeMeters);
        Assert.Null(harness.Context.DesiredAltitudeMeters);

        await harness.Module.ExecuteAsync(harness.Context, TimeSpan.FromSeconds(1), CancellationToken.None);

        Assert.Equal(5000, harness.Context.DesiredAltitudeMeters);
        Assert.Equal(1000, harness.Context.State.AltitudeMeters);
    }

    [Fact]
    public async Task QueueAtCapacity_RejectsAdditionalCommand()
    {
        var queue = new AircraftCommandQueue(1);
        Assert.True(queue.TryEnqueue(CreateCommand(AircraftCommandType.SetSpeed, 200)));
        Assert.False(queue.TryEnqueue(CreateCommand(AircraftCommandType.SetSpeed, 210)));
    }

    private static AircraftCommand CreateCommand(AircraftCommandType type, double value)
    {
        var now = DateTime.UtcNow;
        return new AircraftCommand(Guid.NewGuid(), 10, 20, type, value, now, now.AddMinutes(1));
    }

    private static Harness CreateHarness()
    {
        var state = new AircraftState(10, 20, 0, 0, 1000, 100, 0, FlightStatus.EnRoute);
        var context = new AircraftSimulationContext(state, [new Waypoint(1, 1, 0)]);
        context.AdvanceToNextWaypoint();
        var queue = new AircraftCommandQueue();
        var publisher = new RecordingResultPublisher();
        var movement = new MovementOptions();
        var module = new CommandModule(queue, publisher, movement);
        module.InitializeAsync(context, CancellationToken.None).GetAwaiter().GetResult();
        return new Harness(context, queue, publisher, module);
    }

    private sealed record Harness(
        AircraftSimulationContext Context,
        AircraftCommandQueue Queue,
        RecordingResultPublisher Publisher,
        CommandModule Module);

    private sealed class RecordingResultPublisher : IAircraftCommandResultPublisher
    {
        public ConcurrentQueue<AircraftCommandResult> Results { get; } = new();
        public Task PublishAsync(AircraftCommandResult result, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Results.Enqueue(result);
            return Task.CompletedTask;
        }
    }
}
