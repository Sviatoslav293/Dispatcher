namespace AircraftSimulator.Core;

public interface IAircraftCommandResultPublisher
{
    Task PublishAsync(AircraftCommandResult result, CancellationToken cancellationToken);
}
