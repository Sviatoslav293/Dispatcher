using DispatcherServer.Core;
using DispatcherServer.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DispatcherServer.Persistence;

public sealed class AircraftPositionPersistence(
    IDbContextFactory<DispatcherDbContext> contextFactory,
    ILogger<AircraftPositionPersistence> logger)
    : IAircraftPositionPersistence
{
    public async Task<IReadOnlyList<AircraftTelemetrySnapshot>> LoadLatestPositionsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.AircraftPositions
            .AsNoTracking()
            .OrderBy(position => position.AircraftId)
            .Select(position => new AircraftTelemetrySnapshot(
                position.AircraftId,
                0,
                (double)position.Latitude,
                (double)position.Longitude,
                (double)position.Altitude,
                (double)position.Speed,
                (double)position.Heading,
                "Historical",
                new DateTimeOffset(DateTime.SpecifyKind(position.UpdatedAt, DateTimeKind.Utc)),
                true))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveLatestPositionsAsync(
        IReadOnlyList<AircraftTelemetrySnapshot> positions,
        CancellationToken cancellationToken = default)
    {
        if (positions.Count == 0)
        {
            return;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;
        foreach (var position in positions
            .GroupBy(position => position.AircraftId)
            .Select(group => group.OrderByDescending(position => position.TimestampUtc).First())
            .OrderBy(position => position.AircraftId))
        {
            if (position.AircraftId <= 0 || !double.IsFinite(position.Latitude) || position.Latitude is < -90 or > 90
                || !double.IsFinite(position.Longitude) || position.Longitude is < -180 or > 180
                || !double.IsFinite(position.AltitudeMeters) || position.AltitudeMeters is < 0 or > 99_999_999.99
                || !double.IsFinite(position.SpeedKmh) || position.SpeedKmh is < 0 or > 999_999.99
                || !double.IsFinite(position.HeadingDegrees))
            {
                logger.LogWarning("Skipping invalid aircraft position for AircraftId={AircraftId}.", position.AircraftId);
                continue;
            }

            var utcTicks = position.TimestampUtc.ToUniversalTime().UtcTicks;
            var timestamp = new DateTime(utcTicks - utcTicks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);
            var latitude = decimal.Round((decimal)position.Latitude, 6, MidpointRounding.AwayFromZero);
            var longitude = decimal.Round((decimal)position.Longitude, 6, MidpointRounding.AwayFromZero);
            var altitude = decimal.Round((decimal)position.AltitudeMeters, 2, MidpointRounding.AwayFromZero);
            var speed = decimal.Round((decimal)position.SpeedKmh, 2, MidpointRounding.AwayFromZero);
            var normalizedHeading = ((position.HeadingDegrees % 360) + 360) % 360;
            var heading = decimal.Round((decimal)normalizedHeading, 2, MidpointRounding.AwayFromZero);
            if (heading >= 360m)
            {
                heading = 0m;
            }

            var existing = await context.AircraftPositions.SingleOrDefaultAsync(
                item => item.AircraftId == position.AircraftId, cancellationToken);
            if (existing is not null)
            {
                if (timestamp <= existing.UpdatedAt)
                {
                    continue;
                }

                existing.Latitude = latitude;
                existing.Longitude = longitude;
                existing.Altitude = altitude;
                existing.Speed = speed;
                existing.Heading = heading;
                existing.UpdatedAt = timestamp;
                continue;
            }

            var aircraftExists = await context.Aircraft.AnyAsync(
                aircraft => aircraft.Id == position.AircraftId, cancellationToken);
            if (!aircraftExists)
            {
                logger.LogWarning("Skipping aircraft position because AircraftId={AircraftId} is not present in dbo.Aircraft.", position.AircraftId);
                continue;
            }

            context.AircraftPositions.Add(new Entities.AircraftPosition
            {
                AircraftId = position.AircraftId,
                Latitude = latitude,
                Longitude = longitude,
                Altitude = altitude,
                Speed = speed,
                Heading = heading,
                UpdatedAt = timestamp
            });
        }

        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
