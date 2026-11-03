using AircraftSimulator.Core;
using Microsoft.Extensions.Logging;

namespace AircraftSimulator.Plugins;

public sealed class LoggerTelemetryPublisher(ILogger<LoggerTelemetryPublisher> logger) : ITelemetryPublisher
{
    public Task PublishAsync(AircraftTelemetry telemetry, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation(
            "Aircraft {AircraftId}, flight {FlightId}: lat={Latitude:F6}, lon={Longitude:F6}, altitude={AltitudeMeters:F0} m, speed={SpeedKmh:F1} km/h, heading={HeadingDegrees:F1}°, status={Status}, timestampUtc={TimestampUtc:O}",
            telemetry.AircraftId,
            telemetry.FlightId,
            telemetry.Latitude,
            telemetry.Longitude,
            telemetry.AltitudeMeters,
            telemetry.SpeedKmh,
            telemetry.HeadingDegrees,
            telemetry.Status,
            telemetry.TimestampUtc);
        return Task.CompletedTask;
    }
}
