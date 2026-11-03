using System.Collections.Concurrent;

namespace AircraftSimulator.Core;

public sealed class AircraftCommandQueue : IAircraftCommandQueue
{
    private readonly ConcurrentQueue<AircraftCommandWorkItem> _commands = new();
    private readonly int _capacity;
    private int _count;

    public AircraftCommandQueue(int capacity = 256)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        _capacity = capacity;
    }

    public bool TryEnqueue(AircraftCommand command)
        => TryEnqueue(new AircraftCommandWorkItem(command));

    public bool TryEnqueue(AircraftCommandWorkItem workItem)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        while (true)
        {
            var count = Volatile.Read(ref _count);
            if (count >= _capacity)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _count, count + 1, count) == count)
            {
                _commands.Enqueue(workItem);
                return true;
            }
        }
    }

    public bool TryDequeueWorkItem(out AircraftCommandWorkItem? workItem)
    {
        if (_commands.TryDequeue(out workItem))
        {
            Interlocked.Decrement(ref _count);
            return true;
        }

        return false;
    }

    public bool TryDequeueCommand(out AircraftCommand? command)
    {
        if (TryDequeueWorkItem(out AircraftCommandWorkItem? workItem))
        {
            command = workItem?.Command;
            return true;
        }

        command = null;
        return false;
    }
}
