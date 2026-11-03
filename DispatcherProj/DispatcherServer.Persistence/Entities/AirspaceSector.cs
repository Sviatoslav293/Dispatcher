namespace DispatcherServer.Persistence.Entities;

public sealed class AirspaceSector
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public int MinAltitude { get; set; }
    public int MaxAltitude { get; set; }
    public string BoundaryData { get; set; } = null!;
}
