namespace DispatcherServer.Core;

public sealed record AircraftCommandRequested(
    int FlightId,
    string Type,
    double Value,
    TaskCompletionSource<AircraftCommandRequestOutcome> Completion) : IServerEvent;
