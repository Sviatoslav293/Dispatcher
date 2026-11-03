using DispatcherServer.Core;

namespace DispatcherServer.Plugins;

public sealed class AircraftMonitoringModule(IAircraftStateStore stateStore) : IServerModule
{
    private static readonly Type[] EventTypes = [typeof(AircraftTelemetryReceived)];

    public int ExecutionOrder => 100;
    public IReadOnlyCollection<Type> HandledEventTypes => EventTypes;

    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task HandleEventAsync(IServerEvent serverEvent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (serverEvent is not AircraftTelemetryReceived telemetry)
        {
            throw new ArgumentException($"Unsupported event type '{serverEvent.GetType().Name}'.", nameof(serverEvent));
        }

        stateStore.Update(telemetry);
        return Task.CompletedTask;
    }

    public Task ShutdownAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
