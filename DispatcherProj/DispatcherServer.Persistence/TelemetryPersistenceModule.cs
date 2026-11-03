using DispatcherServer.Core;
using DispatcherServer.Core.Persistence;
using System.Data.Common;
using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DispatcherServer.Persistence;

public sealed class TelemetryPersistenceModule(
    IFlightStatusPersistence flightStatusPersistence,
    IAircraftStateStore stateStore,
    ILogger<TelemetryPersistenceModule> logger) : IServerModule
{
    private static readonly Type[] EventTypes = [typeof(AircraftTelemetryReceived)];
    private readonly ConcurrentDictionary<int, string> _processedStatuses = new();

    public int ExecutionOrder => 200;
    public IReadOnlyCollection<Type> HandledEventTypes => EventTypes;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public async Task HandleEventAsync(IServerEvent serverEvent, CancellationToken cancellationToken)
    {
        if (serverEvent is not AircraftTelemetryReceived telemetry)
        {
            throw new ArgumentException($"Unsupported event type '{serverEvent.GetType().Name}'.", nameof(serverEvent));
        }

        if (!stateStore.TryGetByAircraftId(telemetry.AircraftId, out var latest)
            || latest is null
            || latest.FlightId != telemetry.FlightId
            || latest.TimestampUtc != telemetry.TimestampUtc.ToUniversalTime()
            || !string.Equals(latest.Status, telemetry.Status, StringComparison.Ordinal))
        {
            logger.LogDebug("Ignoring stale or superseded flight status telemetry for FlightId={FlightId}, AircraftId={AircraftId}.",
                telemetry.FlightId, telemetry.AircraftId);
            return;
        }

        if (telemetry.Status is not ("EnRoute" or "RouteCompleted" or "Landed"))
        {
            return;
        }

        if (_processedStatuses.TryGetValue(telemetry.FlightId, out var processedStatus)
            && string.Equals(processedStatus, telemetry.Status, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            if (await flightStatusPersistence.RecordStatusAsync(telemetry, cancellationToken))
            {
                _processedStatuses[telemetry.FlightId] = telemetry.Status;
            }
        }
        catch (Exception exception) when (exception is DbException or DbUpdateException
            || exception.InnerException is DbException or SqlException)
        {
            logger.LogError(exception, "Could not persist flight status for FlightId={FlightId}, AircraftId={AircraftId}; the message will be retried.",
                telemetry.FlightId, telemetry.AircraftId);
            throw new PersistenceUnavailableException("SQL persistence failed while recording a flight status.", exception);
        }
    }

    public Task ShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
