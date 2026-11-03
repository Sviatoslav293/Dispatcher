namespace DispatcherServer.Core.Persistence;

public interface IAtcCommandRepository
{
    Task CreatePendingAsync(AircraftCommand command, CancellationToken cancellationToken);

    Task MarkSentAsync(Guid commandId, CancellationToken cancellationToken);

    Task MarkPublishFailedAsync(Guid commandId, string message, CancellationToken cancellationToken);

    Task<CommandResultPersistenceOutcome> ApplyResultAsync(
        AircraftCommandResult result,
        CancellationToken cancellationToken);

    Task<AtcCommandRecord?> GetByCommandIdAsync(Guid commandId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AtcCommandRecord>> GetByFlightIdAsync(int flightId, CancellationToken cancellationToken);
}
