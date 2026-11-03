namespace DispatcherServer.Persistence.Entities;

public sealed class Aircraft
{
    public int Id { get; set; }
    public string RegistrationNumber { get; set; } = null!;
    public string Model { get; set; } = null!;
    public string AircraftType { get; set; } = null!;
    public AircraftPosition? AircraftPosition { get; set; }
    public ICollection<Flight> Flights { get; set; } = new List<Flight>();
}
