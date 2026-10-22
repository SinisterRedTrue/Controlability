using System.Runtime.InteropServices;
using System.Text;

namespace Controlability.Input.Keyboard;

/// <summary>Reports the process name of the foreground window; refreshed by <see cref="Refresh"/> (cheap, call it every ~100 ms).</summary>
public sealed class ForegroundWatcher
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    private volatile string? _current;

    /// <summary>Process name without ".exe", or null when unknown (secure desktop, access denied, no window).</summary>
    public string? Current => _current;

    public void Refresh() => _current = Query();

    private static string? Query()
    {
        var window = GetForegroundWindow();
        if (window == 0 || GetWindowThreadProcessId(window, out var processId) == 0)
        {
            return null;
        }

        var process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process == 0)
        {
            return null;
        }

        try
        {
            var buffer = new StringBuilder(1024);
            var size = (uint)buffer.Capacity;
            return QueryFullProcessImageName(process, 0, buffer, ref size)
                ? Path.GetFileNameWithoutExtension(buffer.ToString(0, (int)size))
                : null;
        }
        finally
        {
            CloseHandle(process);
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [DllImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}
