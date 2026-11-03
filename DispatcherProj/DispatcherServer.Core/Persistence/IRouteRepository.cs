namespace DispatcherServer.Core.Persistence;

public interface IRouteRepository
{
    Task<RouteRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
