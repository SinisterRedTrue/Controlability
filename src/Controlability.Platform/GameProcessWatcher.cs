using System.Runtime.InteropServices;
using System.Text;
using Controlability.Core.Abstractions;

namespace Controlability.Platform;

/// <summary>
/// Checks the foreground window's process once a second and raises <see cref="ForegroundProcessChanged"/> when it
/// differs from the last check. Picking the profile for that process is the engine's job.
/// </summary>
public sealed class GameProcessWatcher : IGameProcessWatcher
{
    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(1);

    private const uint ProcessQueryLimitedInformation = 0x1000;

    private readonly Func<string?> _query;
    private readonly TimeSpan _interval;
    private readonly object _gate = new();
    private Timer? _timer;
    private string? _current;
    private bool _disposed;

    /// <param name="query">Replaces the Win32 lookup (tests). Returns the process name or null.</param>
    public GameProcessWatcher(Func<string?>? query = null, TimeSpan? interval = null)
    {
        _query = query ?? QueryForeground;
        _interval = interval ?? DefaultInterval;
    }

    public event Action<string?>? ForegroundProcessChanged;

    /// <summary>The process seen by the last check; null until the first one.</summary>
    public string? Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _timer ??= new Timer(_ => Poll(), null, TimeSpan.Zero, _interval);
        }
    }

    public void Stop()
    {
        Timer? timer;
        lock (_gate)
        {
            timer = _timer;
            _timer = null;
            _current = null;
        }

        timer?.Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
        }

        Stop();
    }

    /// <summary>One check. The timer calls it; tests call it directly.</summary>
    public void Poll()
    {
        string? name;
        try
        {
            name = Normalize(_query());
        }
        catch (Exception ex) when (ex is InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            name = null;
        }

        lock (_gate)
        {
            if (_disposed || string.Equals(_current, name, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _current = name;
        }

        ForegroundProcessChanged?.Invoke(name);
    }

    private static string? Normalize(string? name)
    {
        var text = name?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? text[..^4] : text;
    }

    private static string? QueryForeground()
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
