using System.Diagnostics;

namespace Controlability.Engine;

/// <summary>
/// The dedicated input thread: calls <c>tick</c> at a fixed rate, sleeping on a high-resolution waitable timer.
/// <c>tick</c> must not throw; <c>onExit</c> runs on the thread after the last tick.
/// </summary>
public sealed class InputLoop : IDisposable
{
    private readonly Action _tick;
    private readonly Action? _onExit;
    private readonly double _periodMs;
    private readonly Thread _thread;
    private volatile bool _stop;

    public InputLoop(Action tick, int rateHz, Action? onExit = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rateHz, 1);
        _tick = tick;
        _onExit = onExit;
        _periodMs = 1000.0 / rateHz;
        _thread = new Thread(Run)
        {
            Name = "Controlability input",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
    }

    public bool IsRunning => _thread.IsAlive;

    public void Start() => _thread.Start();

    /// <summary>Asks the thread to finish and waits for it, including <c>onExit</c>.</summary>
    public void Stop()
    {
        _stop = true;
        if (_thread.ThreadState != System.Threading.ThreadState.Unstarted && Thread.CurrentThread != _thread)
        {
            _thread.Join();
        }
    }

    public void Dispose() => Stop();

    private void Run()
    {
        try
        {
            using var timer = new HighResTimer();
            var periodTicks = (long)(_periodMs * Stopwatch.Frequency / 1000.0);
            var next = Stopwatch.GetTimestamp() + periodTicks;
            while (!_stop)
            {
                _tick();

                var now = Stopwatch.GetTimestamp();
                var remaining = next - now;
                if (remaining > 0)
                {
                    timer.Sleep(remaining * 1000.0 / Stopwatch.Frequency);
                    next += periodTicks;
                }
                else
                {
                    // Fell behind (stall or long tick): do not burst to catch up.
                    next = now + periodTicks;
                }
            }
        }
        finally
        {
            _onExit?.Invoke();
        }
    }
}
