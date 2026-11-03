namespace AircraftSimulator.Core;

public interface IAircraftCommandQueue
{
    bool TryEnqueue(AircraftCommand command);
    bool TryEnqueue(AircraftCommandWorkItem workItem);
    bool TryDequeueWorkItem(out AircraftCommandWorkItem? workItem);
    bool TryDequeueCommand(out AircraftCommand? command);
}
