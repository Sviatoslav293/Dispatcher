namespace DispatcherServer.Core.Persistence;

public interface IFlightStatusPersistence
{
    Task<bool> RecordStatusAsync(AircraftTelemetryReceived telemetry, CancellationToken cancellationToken = default);
}
