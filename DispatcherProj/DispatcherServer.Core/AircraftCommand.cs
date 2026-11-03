namespace DispatcherServer.Core;

public enum AircraftCommandType
{
    SetHeading,
    SetAltitude,
    SetSpeed
}

public sealed record AircraftCommand(
    Guid CommandId,
    int AircraftId,
    int FlightId,
    AircraftCommandType Type,
    double Value,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc);

public enum AircraftCommandResultStatus
{
    Applied,
    Rejected
}

public sealed record AircraftCommandResult(
    Guid CommandId,
    int AircraftId,
    int FlightId,
    AircraftCommandResultStatus Status,
    string? Message,
    DateTime TimestampUtc);

public enum AtcCommandStatus
{
    Pending,
    Sent,
    Acknowledged,
    Failed
}

public enum CommandResultPersistenceOutcome
{
    Applied,
    NotFound,
    AircraftMismatch,
    FlightMismatch,
    AlreadyFinal
}

public sealed record AtcCommandRecord(
    Guid CommandId,
    int FlightId,
    int AircraftId,
    AircraftCommandType Type,
    double Value,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    AtcCommandStatus Status,
    string? ResultMessage);

public enum AircraftCommandRequestStatus
{
    Accepted,
    Invalid,
    FlightNotFound,
    FlightNotControllable,
    PublishFailed
}

public sealed record AircraftCommandRequestOutcome(
    AircraftCommandRequestStatus Status,
    Guid? CommandId = null,
    string? Message = null);
