namespace DispatcherServer.Core;

public sealed class ServerMicrokernel : IServerEventDispatcher, IAsyncDisposable
{
    private readonly IReadOnlyList<IServerModule> _modules;
    private readonly List<IServerModule> _initializedModules = [];
    private readonly Dictionary<Type, List<IServerModule>> _eventHandlers = [];
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private bool _initialized;
    private bool _disposed;

    public ServerMicrokernel(IEnumerable<IServerModule> modules)
    {
        _modules = modules.OrderBy(module => module.ExecutionOrder).ToArray();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_initialized)
            {
                throw new InvalidOperationException("The server microkernel is already initialized.");
            }

            foreach (var module in _modules)
            {
                try
                {
                    await module.InitializeAsync(cancellationToken);
                    _initializedModules.Add(module);
                    RegisterHandlers(module);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    await RollbackInitializationAsync();
                    throw;
                }
                catch (Exception exception)
                {
                    var moduleException = new ServerModuleException(module.GetType().Name, "initialization", exception);
                    var shutdownErrors = await ShutdownModulesAsync();
                    if (shutdownErrors.Count > 0)
                    {
                        throw new AggregateException(
                            "Server module initialization failed and one or more initialized modules failed to shut down.",
                            [moduleException, .. shutdownErrors]);
                    }

                    throw moduleException;
                }
            }

            _initialized = true;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task DispatchAsync(IServerEvent serverEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(serverEvent);
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_initialized)
            {
                throw new InvalidOperationException("The server microkernel is not initialized.");
            }

            if (!_eventHandlers.TryGetValue(serverEvent.GetType(), out var handlers) || handlers.Count == 0)
            {
                throw new InvalidOperationException($"No server module handles event '{serverEvent.GetType().Name}'.");
            }

            foreach (var module in handlers)
            {
                try
                {
                    await module.HandleEventAsync(serverEvent, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    throw new ServerModuleException(module.GetType().Name, "event handling", exception);
                }
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            var shutdownErrors = await ShutdownModulesAsync();
            if (shutdownErrors.Count > 0)
            {
                throw new AggregateException("One or more server modules failed to shut down.", shutdownErrors);
            }
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await ShutdownAsync(CancellationToken.None);
        _disposed = true;
        _lifecycleGate.Dispose();
    }

    private void RegisterHandlers(IServerModule module)
    {
        foreach (var eventType in module.HandledEventTypes)
        {
            if (!typeof(IServerEvent).IsAssignableFrom(eventType))
            {
                throw new InvalidOperationException(
                    $"Module '{module.GetType().Name}' declared non-event type '{eventType.Name}' as a handled event.");
            }

            if (!_eventHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers = [];
                _eventHandlers.Add(eventType, handlers);
            }

            handlers.Add(module);
        }
    }

    private async Task RollbackInitializationAsync()
    {
        var shutdownErrors = await ShutdownModulesAsync();
        if (shutdownErrors.Count > 0)
        {
            throw new AggregateException("Module initialization was cancelled and cleanup failed.", shutdownErrors);
        }
    }

    private async Task<List<Exception>> ShutdownModulesAsync()
    {
        _initialized = false;
        _eventHandlers.Clear();
        var errors = new List<Exception>();
        for (var index = _initializedModules.Count - 1; index >= 0; index--)
        {
            var module = _initializedModules[index];
            try
            {
                await module.ShutdownAsync(CancellationToken.None);
            }
            catch (Exception exception)
            {
                errors.Add(new ServerModuleException(module.GetType().Name, "shutdown", exception));
            }
        }

        _initializedModules.Clear();
        return errors;
    }
}
