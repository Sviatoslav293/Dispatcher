namespace DispatcherServer.Core.Persistence;

public sealed record RouteRecord(
    int Id,
    string Name,
    string? Description,
    IReadOnlyList<RouteWaypointRecord> Waypoints);
