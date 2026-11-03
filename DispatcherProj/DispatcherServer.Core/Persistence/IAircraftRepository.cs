namespace DispatcherServer.Core.Persistence;

public interface IAircraftRepository
{
    Task<IReadOnlyList<AircraftRecord>> GetAllAsync(CancellationToken cancellationToken = default);
}
