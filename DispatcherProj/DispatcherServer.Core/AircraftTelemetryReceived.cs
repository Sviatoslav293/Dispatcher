namespace DispatcherServer.Core;

public sealed record AircraftTelemetryReceived(
    int AircraftId,
    int FlightId,
    double Latitude,
    double Longitude,
    double AltitudeMeters,
    double SpeedKmh,
    double HeadingDegrees,
    string Status,
    DateTimeOffset TimestampUtc) : IServerEvent;
