using DispatcherServer.Core.Persistence;

namespace DispatcherServer.Tests;

internal sealed class FakeFlightRepository : IFlightRepository
{
    private static readonly FlightRecord Flight = new(
        1, 10, 20, "TEST123", "Active",
        new DateTime(2030, 1, 1, 10, 0, 0, DateTimeKind.Utc), null,
        new DateTime(2030, 1, 1, 12, 0, 0, DateTimeKind.Utc), null);
    private static readonly FlightRecord InactiveFlight = Flight with { Id = 2, Status = "Scheduled" };

    public Task<IReadOnlyList<FlightRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<FlightRecord>>([Flight]);

    public Task<FlightRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult<FlightRecord?>(id == Flight.Id ? Flight : id == InactiveFlight.Id ? InactiveFlight : null);
}

internal sealed class FakeRouteRepository : IRouteRepository
{
    public Task<RouteRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult<RouteRecord?>(id == 2
            ? new RouteRecord(2, "TEST ROUTE", null,
            [
                new RouteWaypointRecord(7, "FIRST", 10m, 20m, 1),
                new RouteWaypointRecord(8, "SECOND", 30m, 40m, 2)
            ])
            : null);
}
