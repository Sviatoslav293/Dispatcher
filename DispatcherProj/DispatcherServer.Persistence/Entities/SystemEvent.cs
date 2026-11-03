namespace DispatcherServer.Persistence.Entities;

public sealed class SystemEvent
{
    public int Id { get; set; }
    public int? FlightId { get; set; }
    public string EventType { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime OccurredAt { get; set; }
    public Flight? Flight { get; set; }
}
