namespace DispatcherServer.Persistence.Entities;

public sealed class Flight
{
    public int Id { get; set; }
    public int AircraftId { get; set; }
    public int RouteId { get; set; }
    public string Callsign { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime ScheduledDeparture { get; set; }
    public DateTime? ActualDeparture { get; set; }
    public DateTime ScheduledArrival { get; set; }
    public DateTime? ActualArrival { get; set; }
    public Aircraft Aircraft { get; set; } = null!;
    public Route Route { get; set; } = null!;
    public ICollection<AtcCommand> AtcCommands { get; set; } = new List<AtcCommand>();
    public ICollection<SystemEvent> SystemEvents { get; set; } = new List<SystemEvent>();
}
