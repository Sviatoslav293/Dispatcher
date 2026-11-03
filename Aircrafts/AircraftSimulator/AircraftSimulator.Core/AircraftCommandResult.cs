namespace AircraftSimulator.Core;

public sealed record AircraftCommandResult(
    Guid CommandId,
    int AircraftId,
    int FlightId,
    AircraftCommandResultStatus Status,
    string Message,
    DateTime TimestampUtc);
