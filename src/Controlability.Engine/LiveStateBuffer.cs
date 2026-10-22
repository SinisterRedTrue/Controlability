using Controlability.Core.Models;

namespace Controlability.Engine;

/// <summary>
/// Single-writer seqlock for the live test panel: the input thread writes without locks or allocations, readers on
/// other threads retry if they overlapped a write.
/// </summary>
internal sealed class LiveStateBuffer
{
    private int _sequence;
    private InputState _raw;
    private XboxState _mapped;

    public void Write(in InputState raw, in XboxState mapped)
    {
        var next = _sequence + 1;
        Volatile.Write(ref _sequence, next); // odd = write in progress
        Interlocked.MemoryBarrier();
        _raw = raw;
        _mapped = mapped;
        Interlocked.MemoryBarrier();
        Volatile.Write(ref _sequence, next + 1);
    }

    public bool TryRead(out InputState raw, out XboxState mapped)
    {
        var spin = new SpinWait();
        while (true)
        {
            var before = Volatile.Read(ref _sequence);
            if (before == 0)
            {
                raw = default;
                mapped = default;
                return false;
            }

            if ((before & 1) == 0)
            {
                var r = _raw;
                var m = _mapped;
                Interlocked.MemoryBarrier();
                if (Volatile.Read(ref _sequence) == before)
                {
                    raw = r;
                    mapped = m;
                    return true;
                }
            }

            spin.SpinOnce(sleep1Threshold: -1);
        }
    }
}
