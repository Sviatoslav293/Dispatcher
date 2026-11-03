namespace DispatcherServer.Core.Persistence;

public sealed record FlightRecord(
    int Id,
    int AircraftId,
    int RouteId,
    string Callsign,
    string Status,
    DateTime ScheduledDeparture,
    DateTime? ActualDeparture,
    DateTime ScheduledArrival,
    DateTime? ActualArrival);
