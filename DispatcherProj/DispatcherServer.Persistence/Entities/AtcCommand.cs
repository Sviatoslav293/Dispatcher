namespace DispatcherServer.Persistence.Entities;

public sealed class AtcCommand
{
    public int Id { get; set; }
    public Guid? CommandId { get; set; }
    public int FlightId { get; set; }
    public string CommandType { get; set; } = null!;
    public string? Parameters { get; set; }
    public DateTime IssuedAt { get; set; }
    public string Status { get; set; } = null!;
    public Flight Flight { get; set; } = null!;
}
