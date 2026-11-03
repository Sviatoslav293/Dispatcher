namespace DispatcherServer.Core;

public interface IAircraftCommandPublisher
{
    Task PublishAsync(AircraftCommand command, CancellationToken cancellationToken);
}
