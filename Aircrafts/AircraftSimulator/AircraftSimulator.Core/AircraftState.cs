namespace AircraftSimulator.Core;

public sealed class AircraftState
{
    public AircraftState(int aircraftId, int flightId, double latitude, double longitude, double altitudeMeters, double speedKmh, double headingDegrees, FlightStatus status)
    {
        AircraftId = aircraftId;
        FlightId = flightId;
        Latitude = latitude;
        Longitude = longitude;
        AltitudeMeters = altitudeMeters;
        SpeedKmh = speedKmh;
        HeadingDegrees = headingDegrees;
        Status = status;
    }

    public int AircraftId { get; }
    public int FlightId { get; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double AltitudeMeters { get; set; }
    public double SpeedKmh { get; set; }
    public double HeadingDegrees { get; set; }
    public FlightStatus Status { get; set; }
    public int? TargetWaypointId { get; set; }
}
