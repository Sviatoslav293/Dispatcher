namespace DispatcherServer.Persistence.Entities;

public sealed class AircraftPosition
{
    public int Id { get; set; }
    public int AircraftId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal Altitude { get; set; }
    public decimal Speed { get; set; }
    public decimal Heading { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Aircraft Aircraft { get; set; } = null!;
}
