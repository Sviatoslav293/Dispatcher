namespace DispatcherServer.Core.Persistence;

public interface IAircraftPositionPersistence
{
    Task<IReadOnlyList<AircraftTelemetrySnapshot>> LoadLatestPositionsAsync(CancellationToken cancellationToken = default);
    Task SaveLatestPositionsAsync(IReadOnlyList<AircraftTelemetrySnapshot> positions, CancellationToken cancellationToken = default);
}
