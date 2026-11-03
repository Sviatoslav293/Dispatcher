namespace AircraftSimulator.Core;

public interface IAircraftConfigurationSource
{
    Task<AircraftConfiguration> LoadAsync(CancellationToken cancellationToken);
}
