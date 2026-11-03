namespace AircraftSimulator.Core;

public sealed record AircraftCommand(
    Guid CommandId,
    int AircraftId,
    int FlightId,
    AircraftCommandType Type,
    double Value,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);
