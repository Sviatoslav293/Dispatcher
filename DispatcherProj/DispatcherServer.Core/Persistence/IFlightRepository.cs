namespace DispatcherServer.Core.Persistence;

public interface IFlightRepository
{
    Task<IReadOnlyList<FlightRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FlightRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
