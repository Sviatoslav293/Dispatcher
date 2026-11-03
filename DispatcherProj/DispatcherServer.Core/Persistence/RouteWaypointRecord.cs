namespace DispatcherServer.Core.Persistence;

public sealed record RouteWaypointRecord(
    int WaypointId,
    string Name,
    decimal Latitude,
    decimal Longitude,
    int SequenceNumber);
