namespace AircraftSimulator.Core;

public sealed record Waypoint(int Id, double Latitude, double Longitude, double? TargetAltitudeMeters = null);
