using DispatcherServer.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DispatcherServer.Persistence.Repositories;

public sealed class RouteRepository(DispatcherDbContext dbContext) : IRouteRepository
{
    public Task<RouteRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Routes
            .AsNoTracking()
            .Where(route => route.Id == id)
            .Select(route => new RouteRecord(
                route.Id,
                route.Name,
                route.Description,
                route.RouteWaypoints
                    .OrderBy(routeWaypoint => routeWaypoint.SequenceNumber)
                    .Select(routeWaypoint => new RouteWaypointRecord(
                        routeWaypoint.WaypointId,
                        routeWaypoint.Waypoint.Name,
                        routeWaypoint.Waypoint.Latitude,
                        routeWaypoint.Waypoint.Longitude,
                        routeWaypoint.SequenceNumber))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
}
