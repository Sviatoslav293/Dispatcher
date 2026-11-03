namespace DispatcherServer.Persistence.Entities;

public sealed class Route
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public ICollection<Flight> Flights { get; set; } = new List<Flight>();
    public ICollection<RouteWaypoint> RouteWaypoints { get; set; } = new List<RouteWaypoint>();
}
