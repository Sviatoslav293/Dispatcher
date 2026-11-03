using DispatcherServer.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DispatcherServer.Persistence.Repositories;

public sealed class FlightRepository(DispatcherDbContext dbContext) : IFlightRepository
{
    public async Task<IReadOnlyList<FlightRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Flights
            .AsNoTracking()
            .OrderBy(flight => flight.Id)
            .Select(flight => new FlightRecord(
                flight.Id,
                flight.AircraftId,
                flight.RouteId,
                flight.Callsign,
                flight.Status,
                flight.ScheduledDeparture,
                flight.ActualDeparture,
                flight.ScheduledArrival,
                flight.ActualArrival))
            .ToListAsync(cancellationToken);

    public Task<FlightRecord?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        dbContext.Flights
            .AsNoTracking()
            .Where(flight => flight.Id == id)
            .Select(flight => new FlightRecord(
                flight.Id,
                flight.AircraftId,
                flight.RouteId,
                flight.Callsign,
                flight.Status,
                flight.ScheduledDeparture,
                flight.ActualDeparture,
                flight.ScheduledArrival,
                flight.ActualArrival))
            .SingleOrDefaultAsync(cancellationToken);
}
