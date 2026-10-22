using System.Runtime.InteropServices;

namespace Controlability.Cli;

/// <summary>
/// Sleeps with a high-resolution waitable timer (about 0.5 ms granularity) instead of busy-spinning,
/// so a 1000 Hz poll loop stays cheap. Falls back to <see cref="Thread.Sleep(int)"/> if the timer is unavailable.
/// </summary>
internal sealed class HighResTimer : IDisposable
{
    private const uint CreateWaitableTimerHighResolution = 0x00000002;
    private const uint TimerAllAccess = 0x1F0003;

    private readonly IntPtr _handle;

    public HighResTimer()
    {
        _handle = CreateWaitableTimerExW(IntPtr.Zero, null, CreateWaitableTimerHighResolution, TimerAllAccess);
    }

    public void Sleep(double milliseconds)
    {
        if (_handle == IntPtr.Zero)
        {
            Thread.Sleep(Math.Max(1, (int)milliseconds));
            return;
        }

        // Negative due time = relative, in 100 ns units.
        var dueTime = -(long)(milliseconds * 10_000);
        if (SetWaitableTimer(_handle, ref dueTime, 0, IntPtr.Zero, IntPtr.Zero, false))
        {
            WaitForSingleObject(_handle, uint.MaxValue);
        }
        else
        {
            Thread.Sleep(Math.Max(1, (int)milliseconds));
        }
    }

    public void Dispose()
    {
        if (_handle != IntPtr.Zero)
        {
            CloseHandle(_handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWaitableTimerExW(IntPtr attributes, string? name, uint flags, uint desiredAccess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(
        IntPtr timer, ref long dueTime, int period, IntPtr completionRoutine, IntPtr argument, [MarshalAs(UnmanagedType.Bool)] bool resume);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);
}
