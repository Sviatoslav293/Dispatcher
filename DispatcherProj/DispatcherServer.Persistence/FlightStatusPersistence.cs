using DispatcherServer.Core;
using DispatcherServer.Core.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DispatcherServer.Persistence;

public sealed class FlightStatusPersistence(
    IDbContextFactory<DispatcherDbContext> contextFactory,
    ILogger<FlightStatusPersistence> logger) : IFlightStatusPersistence
{
    public async Task<bool> RecordStatusAsync(AircraftTelemetryReceived telemetry, CancellationToken cancellationToken = default)
    {
        if (telemetry.FlightId <= 0 || telemetry.AircraftId <= 0)
        {
            logger.LogWarning("Ignoring flight status event with invalid flight or aircraft ID. FlightId={FlightId}, AircraftId={AircraftId}",
                telemetry.FlightId, telemetry.AircraftId);
            return false;
        }

        var targetStatus = MapFlightStatus(telemetry.Status);
        if (targetStatus is null)
        {
            return true;
        }

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken)
            : null;

        var flight = await context.Flights.SingleOrDefaultAsync(
            candidate => candidate.Id == telemetry.FlightId && candidate.AircraftId == telemetry.AircraftId,
            cancellationToken);
        if (flight is null)
        {
            logger.LogWarning("Ignoring flight status event because matching flight/aircraft was not found. FlightId={FlightId}, AircraftId={AircraftId}",
                telemetry.FlightId, telemetry.AircraftId);
            return false;
        }

        var previousStatus = flight.Status;

        if (flight.Status == "Cancelled" || flight.Status == "Completed")
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        }

        if (flight.Status == "Scheduled" && targetStatus == "Active")
        {
            flight.Status = "Active";
            if (flight.ActualDeparture is null)
            {
                flight.ActualDeparture = telemetry.TimestampUtc.ToUniversalTime().UtcDateTime;
            }
        }
        else if (targetStatus == "Completed")
        {
            flight.Status = "Completed";
            flight.ActualArrival ??= telemetry.TimestampUtc.ToUniversalTime().UtcDateTime;
        }
        else
        {
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return true;
        }

        context.SystemEvents.Add(new Entities.SystemEvent
        {
            FlightId = flight.Id,
            EventType = targetStatus == "Completed" ? "FlightCompleted" : "FlightStarted",
            Description = $"Flight status changed from {previousStatus} to {targetStatus}.",
            OccurredAt = telemetry.TimestampUtc.ToUniversalTime().UtcDateTime
        });

        await context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static string? MapFlightStatus(string telemetryStatus) => telemetryStatus switch
    {
        "EnRoute" => "Active",
        "RouteCompleted" or "Landed" => "Completed",
        "Grounded" => "Scheduled",
        _ => null
    };
}
