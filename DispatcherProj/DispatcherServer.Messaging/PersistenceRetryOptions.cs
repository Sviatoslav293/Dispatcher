namespace DispatcherServer.Messaging;

public sealed class PersistenceRetryOptions
{
    public TimeSpan Delay { get; set; } = TimeSpan.FromSeconds(2);
}
