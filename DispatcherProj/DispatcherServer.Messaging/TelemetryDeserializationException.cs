namespace DispatcherServer.Messaging;

public sealed class TelemetryDeserializationException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
}
