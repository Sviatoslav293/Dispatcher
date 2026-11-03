using DispatcherServer.Core;
using DispatcherServer.Core.Persistence;

namespace DispatcherServer.Tests;

internal sealed class FakeAircraftCommandPublisher : IAircraftCommandPublisher
{
    private readonly object _gate = new();
    private readonly List<AircraftCommand> _publishedCommands = [];

    public Exception? Failure { get; set; }

    public IReadOnlyList<AircraftCommand> PublishedCommands
    {
        get
        {
            lock (_gate)
            {
                return _publishedCommands.ToArray();
            }
        }
    }

    public Task PublishAsync(AircraftCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (Failure is not null)
            {
                throw Failure;
            }

            _publishedCommands.Add(command);
        }

        return Task.CompletedTask;
    }
}

internal sealed class InMemoryAtcCommandRepository : IAtcCommandRepository
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, AtcCommandRecord> _commands = [];

    public Task CreatePendingAsync(AircraftCommand command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _commands.Add(command.CommandId, new AtcCommandRecord(
                command.CommandId,
                command.FlightId,
                command.AircraftId,
                command.Type,
                command.Value,
                command.IssuedAtUtc,
                command.ExpiresAtUtc,
                AtcCommandStatus.Pending,
                null));
        }

        return Task.CompletedTask;
    }

    public Task MarkSentAsync(Guid commandId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_commands.TryGetValue(commandId, out var command) && command.Status == AtcCommandStatus.Pending)
            {
                _commands[commandId] = command with { Status = AtcCommandStatus.Sent };
            }
        }

        return Task.CompletedTask;
    }

    public Task MarkPublishFailedAsync(Guid commandId, string message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_commands.TryGetValue(commandId, out var command) && command.Status == AtcCommandStatus.Pending)
            {
                _commands[commandId] = command with { Status = AtcCommandStatus.Failed, ResultMessage = message };
            }
        }

        return Task.CompletedTask;
    }

    public Task<CommandResultPersistenceOutcome> ApplyResultAsync(
        AircraftCommandResult result,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_commands.TryGetValue(result.CommandId, out var command))
            {
                return Task.FromResult(CommandResultPersistenceOutcome.NotFound);
            }

            if (command.AircraftId != result.AircraftId)
            {
                return Task.FromResult(CommandResultPersistenceOutcome.AircraftMismatch);
            }

            if (command.FlightId != result.FlightId)
            {
                return Task.FromResult(CommandResultPersistenceOutcome.FlightMismatch);
            }

            if (command.Status is AtcCommandStatus.Acknowledged or AtcCommandStatus.Failed)
            {
                return Task.FromResult(CommandResultPersistenceOutcome.AlreadyFinal);
            }

            var status = result.Status == AircraftCommandResultStatus.Applied
                ? AtcCommandStatus.Acknowledged
                : AtcCommandStatus.Failed;
            _commands[result.CommandId] = command with { Status = status, ResultMessage = result.Message };
            return Task.FromResult(CommandResultPersistenceOutcome.Applied);
        }
    }

    public Task<AtcCommandRecord?> GetByCommandIdAsync(Guid commandId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            _commands.TryGetValue(commandId, out var command);
            return Task.FromResult(command);
        }
    }

    public Task<IReadOnlyList<AtcCommandRecord>> GetByFlightIdAsync(int flightId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<AtcCommandRecord>>(_commands.Values
                .Where(command => command.FlightId == flightId)
                .OrderByDescending(command => command.IssuedAtUtc)
                .ToArray());
        }
    }
}
