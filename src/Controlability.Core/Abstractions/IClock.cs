namespace Controlability.Core.Abstractions;

/// <summary>A monotonic clock, so timing code (grace periods, turbo, decay) can be tested without waiting.</summary>
public interface IClock
{
    /// <summary>Current time in clock ticks; only differences between two readings mean anything.</summary>
    long Ticks { get; }

    /// <summary>Converts a tick difference to milliseconds.</summary>
    double ToMs(long ticks);
}
