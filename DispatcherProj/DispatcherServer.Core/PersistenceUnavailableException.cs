namespace DispatcherServer.Core;

public sealed class PersistenceUnavailableException(string message, Exception innerException)
    : Exception(message, innerException)
{
}
