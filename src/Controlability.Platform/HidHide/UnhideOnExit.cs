using Controlability.Core.Abstractions;

namespace Controlability.Platform.HidHide;

/// <summary>
/// Calls <see cref="IDeviceHider.UnhideAll"/> when the process exits or dies on an unhandled exception, so a crash that
/// still runs managed code does not leave the controllers hidden. A hard kill is covered by the journal on the next start.
/// </summary>
public sealed class UnhideOnExit : IDisposable
{
    private readonly IDeviceHider _hider;
    private readonly Action<string>? _log;
    private bool _disposed;

    public UnhideOnExit(IDeviceHider hider, Action<string>? log = null)
    {
        _hider = hider;
        _log = log;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
    }

    private void OnProcessExit(object? sender, EventArgs e) => UnhideAll();

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) => UnhideAll();

    private void UnhideAll()
    {
        try
        {
            _hider.UnhideAll();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Could not unhide the controllers on exit: {ex.Message}");
        }
    }
}
