namespace AircraftSimulator.Core;

public sealed record AircraftCommandWorkItem(AircraftCommand Command, string? RejectionReason = null);
