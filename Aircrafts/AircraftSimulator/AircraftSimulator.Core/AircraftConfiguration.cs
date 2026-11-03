namespace AircraftSimulator.Core;

public sealed record AircraftConfiguration(AircraftState InitialState, IReadOnlyList<Waypoint> Route);
