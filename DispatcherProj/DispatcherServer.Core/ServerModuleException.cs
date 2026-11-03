namespace DispatcherServer.Core;

public sealed class ServerModuleException(string moduleName, string operation, Exception innerException)
    : Exception($"Server module '{moduleName}' failed during {operation}.", innerException)
{
    public string ModuleName { get; } = moduleName;
    public string Operation { get; } = operation;
}
