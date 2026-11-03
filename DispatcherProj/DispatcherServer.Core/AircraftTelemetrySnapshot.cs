using System.Text.Json.Serialization;

namespace DispatcherServer.Core;

public sealed record AircraftTelemetrySnapshot(
    int AircraftId,
    int FlightId,
    double Latitude,
    double Longitude,
    double AltitudeMeters,
    double SpeedKmh,
    double HeadingDegrees,
    string Status,
    DateTimeOffset TimestampUtc,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool IsHistorical = false);
