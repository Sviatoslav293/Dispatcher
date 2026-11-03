using AircraftSimulator.Core;
using Microsoft.Extensions.Logging;

namespace AircraftSimulator.Plugins;

public sealed class CommandModule(
    IAircraftCommandQueue commandQueue,
    IAircraftCommandResultPublisher resultPublisher,
    MovementOptions movementOptions,
    CommandModuleOptions? commandOptions = null,
    ILogger<CommandModule>? logger = null) : IAircraftModule
{
    private readonly CommandModuleOptions _commandOptions = commandOptions ?? CommandModuleOptions.Default;
    private readonly ILogger<CommandModule> _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<CommandModule>.Instance;
    private readonly HashSet<Guid> _processedCommandIds = [];
    private int _aircraftId;
    private int _flightId;

    public int ExecutionOrder => 50;

    public Task InitializeAsync(IAircraftSimulationContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        movementOptions.Validate();
        _commandOptions.Validate();
        _aircraftId = context.State.AircraftId;
        _flightId = context.State.FlightId;
        return Task.CompletedTask;
    }

    public async Task ExecuteAsync(IAircraftSimulationContext context, TimeSpan deltaTime, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        while (commandQueue.TryDequeueWorkItem(out var workItem))
        {
            if (workItem is null) continue;
            var command = workItem.Command;
            AircraftCommandResult result;
            try
            {
                result = workItem.RejectionReason is { } rejectionReason
                    ? CreateResult(command, AircraftCommandResultStatus.Rejected, rejectionReason)
                    : ApplyCommand(context, command);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Command {CommandId} could not be applied.", command.CommandId);
                result = CreateResult(command, AircraftCommandResultStatus.Rejected, "Command could not be applied due to an internal error.");
            }

            await resultPublisher.PublishAsync(result, cancellationToken);
        }
    }

    public Task ShutdownAsync(IAircraftSimulationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    private AircraftCommandResult ApplyCommand(IAircraftSimulationContext context, AircraftCommand command)
    {
        string? rejection = null;
        if (command.CommandId == Guid.Empty) rejection = "CommandId must not be empty.";
        else if (!_processedCommandIds.Add(command.CommandId)) rejection = "Duplicate CommandId was already processed.";
        else if (command.AircraftId != _aircraftId) rejection = "AircraftId does not match this aircraft.";
        else if (command.FlightId != _flightId) rejection = "FlightId does not match the active flight.";
        else if (!double.IsFinite(command.Value)) rejection = "Command value must be finite.";
        else if (command.IssuedAtUtc.Kind != DateTimeKind.Utc || command.ExpiresAtUtc.Kind != DateTimeKind.Utc)
            rejection = "Command timestamps must be UTC.";
        else if (command.ExpiresAtUtc <= DateTime.UtcNow) rejection = "Command has expired.";
        else if (command.ExpiresAtUtc <= command.IssuedAtUtc) rejection = "ExpiresAtUtc must be later than IssuedAtUtc.";
        else if (context.State.Status is FlightStatus.Landed or FlightStatus.RouteCompleted)
            rejection = "The aircraft is no longer active.";
        else
        {
            rejection = ApplyTarget(context, command);
        }

        if (rejection is not null)
        {
            _logger.LogWarning("Rejected aircraft command {CommandId}: {Reason}", command.CommandId, rejection);
        }

        return CreateResult(command,
            rejection is null ? AircraftCommandResultStatus.Applied : AircraftCommandResultStatus.Rejected,
            rejection ?? "Command target applied; aircraft will move toward it within the configured kinematic limits.");
    }

    private static AircraftCommandResult CreateResult(AircraftCommand command, AircraftCommandResultStatus status, string message) =>
        new(command.CommandId, command.AircraftId, command.FlightId, status, message, DateTime.UtcNow);

    private string? ApplyTarget(IAircraftSimulationContext context, AircraftCommand command)
    {
        switch (command.Type)
        {
            case AircraftCommandType.SetHeading:
                context.DesiredHeadingDegrees = ((command.Value % 360) + 360) % 360;
                context.ManualHeadingExpiresAtUtc = DateTimeOffset.UtcNow.Add(_commandOptions.ManualHeadingDuration);
                return null;
            case AircraftCommandType.SetAltitude:
                if (command.Value < _commandOptions.MinimumAltitudeMeters || command.Value > _commandOptions.MaximumAltitudeMeters)
                    return $"Altitude must be between {_commandOptions.MinimumAltitudeMeters} and {_commandOptions.MaximumAltitudeMeters} meters.";
                context.DesiredAltitudeMeters = command.Value;
                context.HasManualAltitudeTarget = true;
                return null;
            case AircraftCommandType.SetSpeed:
                if (command.Value < 0 || command.Value > _commandOptions.MaximumSpeedKmh
                    || command.Value > movementOptions.TargetSpeedKmh)
                    return $"Speed must be between 0 and {Math.Min(_commandOptions.MaximumSpeedKmh, movementOptions.TargetSpeedKmh)} km/h.";
                context.DesiredSpeedKmh = command.Value;
                context.HasManualSpeedTarget = true;
                return null;
            default:
                return "Command type is not supported.";
        }
    }
}
