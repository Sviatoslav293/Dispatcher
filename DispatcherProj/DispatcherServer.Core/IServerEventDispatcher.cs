namespace DispatcherServer.Core;

public interface IServerEventDispatcher
{
    Task DispatchAsync(IServerEvent serverEvent, CancellationToken cancellationToken);
}
