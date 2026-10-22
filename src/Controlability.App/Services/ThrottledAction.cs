using Controlability.Core.Abstractions;

namespace Controlability.App.Services;

/// <summary>
/// Turns a burst of <see cref="Request"/> calls from any thread into at most one run of <c>action</c> on the UI thread
/// per <c>minInterval</c>. Requests made while a run is scheduled are folded into it; a request made during the run
/// schedules the next one.
/// </summary>
public sealed class ThrottledAction(IUiThread ui, IClock clock, TimeSpan minInterval, Action action)
{
    private int _pending;
    private long _lastRun;
    private bool _hasRun;

    public void Request()
    {
        if (Interlocked.Exchange(ref _pending, 1) == 1)
        {
            return;
        }

        var delay = TimeSpan.Zero;
        if (Volatile.Read(ref _hasRun))
        {
            var sinceMs = clock.ToMs(clock.Ticks - Volatile.Read(ref _lastRun));
            delay = TimeSpan.FromMilliseconds(Math.Max(0, minInterval.TotalMilliseconds - sinceMs));
        }

        ui.Post(Run, delay);
    }

    private void Run()
    {
        Volatile.Write(ref _lastRun, clock.Ticks);
        Volatile.Write(ref _hasRun, true);
        Interlocked.Exchange(ref _pending, 0);
        action();
    }
}
