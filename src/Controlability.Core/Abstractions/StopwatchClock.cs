using System.Diagnostics;

namespace Controlability.Core.Abstractions;

/// <summary><see cref="IClock"/> backed by <see cref="Stopwatch"/> (high-resolution, monotonic).</summary>
public sealed class StopwatchClock : IClock
{
    public long Ticks => Stopwatch.GetTimestamp();

    public double ToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
}
