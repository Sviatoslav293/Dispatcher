namespace AircraftSimulator.Core;

public sealed class AircraftModuleException(string moduleName, string operation, Exception innerException)
    : Exception($"Aircraft module '{moduleName}' failed during {operation}.", innerException)
{
    public string ModuleName { get; } = moduleName;
    public string Operation { get; } = operation;
}
