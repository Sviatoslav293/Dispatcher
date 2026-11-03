namespace DispatcherServer.Core;

public sealed record AircraftCommandResultReceived(
    AircraftCommandResult Result,
    TaskCompletionSource<CommandResultPersistenceOutcome> Completion) : IServerEvent;
