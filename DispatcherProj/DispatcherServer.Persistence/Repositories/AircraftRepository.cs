using DispatcherServer.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DispatcherServer.Persistence.Repositories;

public sealed class AircraftRepository(DispatcherDbContext dbContext) : IAircraftRepository
{
    public async Task<IReadOnlyList<AircraftRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Aircraft
            .AsNoTracking()
            .OrderBy(aircraft => aircraft.Id)
            .Select(aircraft => new AircraftRecord(
                aircraft.Id,
                aircraft.RegistrationNumber,
                aircraft.Model,
                aircraft.AircraftType))
            .ToListAsync(cancellationToken);
}
