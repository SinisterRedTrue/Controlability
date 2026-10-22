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
    private long _periodTicks;
    private readonly Thread _thread;
    private volatile bool _stop;

    public InputLoop(Action tick, int rateHz, Action? onExit = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(rateHz, 1);
        _tick = tick;
        _onExit = onExit;
        Rate = rateHz;
        _thread = new Thread(Run)
        {
            Name = "Controlability input",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
    }

    public bool IsRunning => _thread.IsAlive;

    /// <summary>Ticks per second; can be changed while the loop runs and applies from the next tick.</summary>
    public int Rate
    {
        get => (int)Math.Round((double)Stopwatch.Frequency / Interlocked.Read(ref _periodTicks));
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
            Interlocked.Exchange(ref _periodTicks, Stopwatch.Frequency / value);
        }
    }

    public void Start() => _thread.Start();

    /// <summary>Asks the thread to finish and waits for it, including <c>onExit</c>.</summary>
    public void Stop()
    {
        _stop = true;
        if ((_thread.ThreadState & System.Threading.ThreadState.Unstarted) == 0 && Thread.CurrentThread != _thread)
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
            var next = Stopwatch.GetTimestamp() + Interlocked.Read(ref _periodTicks);
            while (!_stop)
            {
                _tick();

                var periodTicks = Interlocked.Read(ref _periodTicks);
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
