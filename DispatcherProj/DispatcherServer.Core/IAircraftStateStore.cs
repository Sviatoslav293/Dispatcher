namespace DispatcherServer.Core;

public interface IAircraftStateStore
{
    bool Update(AircraftTelemetryReceived telemetry);
    bool Restore(AircraftTelemetrySnapshot telemetry);
    IReadOnlyList<AircraftTelemetrySnapshot> GetAll();
    bool TryGetByAircraftId(int aircraftId, out AircraftTelemetrySnapshot? telemetry);
}
