namespace DispatcherServer.Persistence.Entities;

public sealed class Waypoint
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public ICollection<RouteWaypoint> RouteWaypoints { get; set; } = new List<RouteWaypoint>();
}
