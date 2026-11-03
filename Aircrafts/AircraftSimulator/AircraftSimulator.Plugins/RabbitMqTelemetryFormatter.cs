using System.Text.Json;
using AircraftSimulator.Core;

namespace AircraftSimulator.Plugins;

public static class RabbitMqTelemetryFormatter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static string CreateRoutingKey(int aircraftId)
    {
        if (aircraftId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(aircraftId), "AircraftId must be greater than zero.");
        }

        return FormattableString.Invariant($"aircraft.{aircraftId}.telemetry");
    }

    public static byte[] Serialize(AircraftTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var message = new TelemetryMessage(
            telemetry.AircraftId,
            telemetry.FlightId,
            telemetry.Latitude,
            telemetry.Longitude,
            telemetry.AltitudeMeters,
            telemetry.SpeedKmh,
            telemetry.HeadingDegrees,
            telemetry.Status.ToString(),
            telemetry.TimestampUtc.ToUniversalTime());
        return JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
    }

    private sealed record TelemetryMessage(
        int AircraftId,
        int FlightId,
        double Latitude,
        double Longitude,
        double AltitudeMeters,
        double SpeedKmh,
        double HeadingDegrees,
        string Status,
        DateTimeOffset TimestampUtc);
}
