namespace DispatcherServer.Core;

public interface IServerModule
{
    int ExecutionOrder { get; }
    IReadOnlyCollection<Type> HandledEventTypes { get; }

    Task InitializeAsync(CancellationToken cancellationToken);

    Task HandleEventAsync(IServerEvent serverEvent, CancellationToken cancellationToken);

    Task ShutdownAsync(CancellationToken cancellationToken);
}
