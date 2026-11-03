using System.Collections.Concurrent;
using DispatcherServer.Core;

namespace DispatcherServer.Plugins;

public sealed class InMemoryAircraftStateStore : IAircraftStateStore
{
    private readonly ConcurrentDictionary<int, AircraftTelemetrySnapshot> _states = new();

    public bool Update(AircraftTelemetryReceived telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var snapshot = new AircraftTelemetrySnapshot(
            telemetry.AircraftId,
            telemetry.FlightId,
            telemetry.Latitude,
            telemetry.Longitude,
            telemetry.AltitudeMeters,
            telemetry.SpeedKmh,
            telemetry.HeadingDegrees,
            telemetry.Status,
            telemetry.TimestampUtc.ToUniversalTime(),
            false);

        while (true)
        {
            if (_states.TryGetValue(snapshot.AircraftId, out var current))
            {
                if (snapshot.TimestampUtc <= current.TimestampUtc)
                {
                    return false;
                }

                if (_states.TryUpdate(snapshot.AircraftId, snapshot, current))
                {
                    return true;
                }

                continue;
            }

            if (_states.TryAdd(snapshot.AircraftId, snapshot))
            {
                return true;
            }
        }
    }

    public bool Restore(AircraftTelemetrySnapshot telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);
        var historical = telemetry with
        {
            Status = "Historical",
            TimestampUtc = telemetry.TimestampUtc.ToUniversalTime(),
            IsHistorical = true
        };

        while (true)
        {
            if (_states.TryGetValue(historical.AircraftId, out var current))
            {
                if (historical.TimestampUtc <= current.TimestampUtc)
                {
                    return false;
                }

                if (_states.TryUpdate(historical.AircraftId, historical, current))
                {
                    return true;
                }

                continue;
            }

            if (_states.TryAdd(historical.AircraftId, historical))
            {
                return true;
            }
        }
    }

    public IReadOnlyList<AircraftTelemetrySnapshot> GetAll()
        => _states.Values.OrderBy(state => state.AircraftId).ToArray();

    public bool TryGetByAircraftId(int aircraftId, out AircraftTelemetrySnapshot? telemetry)
        => _states.TryGetValue(aircraftId, out telemetry);
}
