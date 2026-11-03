using System.Text.Json;
using DispatcherServer.Core;

namespace DispatcherServer.Messaging;

public sealed class TelemetryMessageDeserializer
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public AircraftTelemetryReceived Deserialize(ReadOnlyMemory<byte> body)
    {
        AircraftTelemetryReceived? telemetry;
        try
        {
            telemetry = JsonSerializer.Deserialize<AircraftTelemetryReceived>(body.Span, SerializerOptions);
        }
        catch (JsonException exception)
        {
            throw new TelemetryDeserializationException("Telemetry message contains invalid JSON.", exception);
        }

        if (telemetry is null)
        {
            throw new TelemetryDeserializationException("Telemetry message is empty.");
        }

        if (telemetry.AircraftId <= 0 || telemetry.FlightId <= 0)
        {
            throw new TelemetryDeserializationException("AircraftId and FlightId must be greater than zero.");
        }

        if (!double.IsFinite(telemetry.Latitude) || telemetry.Latitude is < -90 or > 90
            || !double.IsFinite(telemetry.Longitude) || telemetry.Longitude is < -180 or > 180)
        {
            throw new TelemetryDeserializationException("Telemetry coordinates are invalid.");
        }

        if (!double.IsFinite(telemetry.AltitudeMeters) || telemetry.AltitudeMeters < 0
            || !double.IsFinite(telemetry.SpeedKmh) || telemetry.SpeedKmh < 0
            || !double.IsFinite(telemetry.HeadingDegrees) || telemetry.HeadingDegrees is < 0 or >= 360)
        {
            throw new TelemetryDeserializationException("Telemetry altitude, speed, or heading is invalid.");
        }

        if (string.IsNullOrWhiteSpace(telemetry.Status))
        {
            throw new TelemetryDeserializationException("Telemetry status must be provided.");
        }

        if (telemetry.TimestampUtc == default)
        {
            throw new TelemetryDeserializationException("Telemetry timestampUtc must be provided.");
        }

        return telemetry with { TimestampUtc = telemetry.TimestampUtc.ToUniversalTime() };
    }
}
