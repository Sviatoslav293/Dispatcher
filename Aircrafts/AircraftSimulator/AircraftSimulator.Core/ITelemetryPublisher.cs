namespace AircraftSimulator.Core;

public sealed record AircraftTelemetry(
    int AircraftId,
    int FlightId,
    double Latitude,
    double Longitude,
    double AltitudeMeters,
    double SpeedKmh,
    double HeadingDegrees,
    FlightStatus Status,
    DateTimeOffset TimestampUtc);

public interface ITelemetryPublisher
{
    Task PublishAsync(AircraftTelemetry telemetry, CancellationToken cancellationToken);
}
