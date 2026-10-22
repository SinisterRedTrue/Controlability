using System.Windows;
using Controlability.App.Resources;
using Microsoft.Extensions.DependencyInjection;

namespace Controlability.App.Services;

/// <summary>Window show/hide and exit, shared by the main window and the tray. UI thread only.</summary>
public sealed class ShellController(IServiceProvider services) : IShell
{
    private bool _toldAboutTray;

    public bool IsExiting { get; private set; }

    public void ShowMainWindow()
    {
        var window = services.GetRequiredService<MainWindow>();
        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }

    public void Exit()
    {
        IsExiting = true;
        Application.Current.Shutdown();
    }

    /// <summary>Tells the user once per run that closing the window did not quit the app.</summary>
    public void OnHiddenToTray()
    {
        if (_toldAboutTray)
        {
            return;
        }

        _toldAboutTray = true;

        // Resolved late: the tray needs this controller, so it cannot be a constructor argument.
        services.GetRequiredService<INotifier>().Notify(Strings.TrayStillRunningTitle, Strings.TrayStillRunningText);
    }
}
