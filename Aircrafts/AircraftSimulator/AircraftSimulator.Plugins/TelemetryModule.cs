using AircraftSimulator.Core;

namespace AircraftSimulator.Plugins;

public sealed class TelemetryModule(ITelemetryPublisher publisher) : IAircraftModule
{
    public int ExecutionOrder => 300;

    public Task InitializeAsync(IAircraftSimulationContext context, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task ExecuteAsync(IAircraftSimulationContext context, TimeSpan deltaTime, CancellationToken cancellationToken)
    {
        var state = context.State;
        var telemetry = new AircraftTelemetry(
            state.AircraftId,
            state.FlightId,
            state.Latitude,
            state.Longitude,
            state.AltitudeMeters,
            state.SpeedKmh,
            state.HeadingDegrees,
            state.Status,
            DateTimeOffset.UtcNow);
        return publisher.PublishAsync(telemetry, cancellationToken);
    }

    public Task ShutdownAsync(IAircraftSimulationContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}
