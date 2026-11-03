namespace DispatcherServer.Core.Persistence;

public sealed record AircraftRecord(
    int Id,
    string RegistrationNumber,
    string Model,
    string AircraftType);
