namespace DispatcherServer.Persistence.Entities;

public sealed class RouteWaypoint
{
    public int RouteId { get; set; }
    public int WaypointId { get; set; }
    public int SequenceNumber { get; set; }
    public Route Route { get; set; } = null!;
    public Waypoint Waypoint { get; set; } = null!;
}
