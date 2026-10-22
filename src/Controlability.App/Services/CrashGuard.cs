using Microsoft.Extensions.Logging;

namespace Controlability.App.Services;

/// <summary>
/// Last line of defence. A crash on the UI thread or any other thread is logged, the controllers are given back, the
/// virtual pads are removed and the user gets a dialog with "Copy diagnostics". An unobserved task exception is only
/// logged: since .NET 4.5 it no longer ends the process, so tearing the pads down for it would end a harmless failed
/// fire-and-forget call and the user's game session with it.
/// </summary>
public sealed class CrashGuard(ILogger logger, Action releaseEverything, Action<Exception> showDialog)
{
    private int _handling;

    public void Install(System.Windows.Application app)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            e.Handled = true;
            OnFatal(e.Exception, "UI thread");
            app.Shutdown();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            OnFatal(e.ExceptionObject as Exception ?? new InvalidOperationException(e.ExceptionObject?.ToString()), "background thread");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            OnUnobserved(e.Exception);
        };
    }

    public void OnUnobserved(Exception exception)
        => logger.LogError(exception, "Unobserved task exception (ignored).");

    /// <summary>Logs, releases everything and shows the dialog. Runs once; later crashes during the dialog are only logged.</summary>
    public void OnFatal(Exception exception, string where)
    {
        logger.LogCritical(exception, "Unhandled exception on the {Where}.", where);
        if (Interlocked.Exchange(ref _handling, 1) == 1)
        {
            return;
        }

        try
        {
            releaseEverything();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not release the controllers after a crash.");
        }

        try
        {
            showDialog(exception);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not show the crash dialog.");
        }
    }
}
