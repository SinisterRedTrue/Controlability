using System.ComponentModel;
using System.Windows;
using Controlability.App.Services;
using Controlability.App.Views;
using Controlability.Core.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Controlability.App;

public partial class MainWindow : FluentWindow
{
    private readonly Settings _settings;
    private readonly Action<Settings> _save;
    private readonly ShellController _shell;

    public MainWindow(Settings settings, Action<Settings> save, ShellController shell, DiPageProvider pages)
    {
        _settings = settings;
        _save = save;
        _shell = shell;
        InitializeComponent();
        RestorePlacement();

        RootNavigation.SetPageProviderService(pages);
        Loaded += (_, _) => RootNavigation.Navigate(typeof(DashboardPage));
        if (settings.Theme == AppTheme.System)
        {
            SystemThemeWatcher.Watch(this);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        SavePlacement();
        if (!_shell.IsExiting && _settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _shell.OnHiddenToTray();
        }
    }

    /// <summary>Puts the window where it was, unless that spot is no longer on any screen (a monitor was unplugged).</summary>
    private void RestorePlacement()
    {
        if (_settings.WindowPlacement is not { } placement || placement.Width < MinWidth || placement.Height < MinHeight)
        {
            return;
        }

        var bounds = new Rect(placement.Left, placement.Top, placement.Width, placement.Height);
        var screen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
        var visible = Rect.Intersect(bounds, screen);
        if (visible.IsEmpty || visible.Width < 100 || visible.Height < 60)
        {
            return;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = placement.Left;
        Top = placement.Top;
        Width = placement.Width;
        Height = placement.Height;
        if (placement.Maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private void SavePlacement()
    {
        // RestoreBounds is the normal-state rectangle even while maximized or minimized.
        var bounds = WindowState == WindowState.Normal ? new Rect(Left, Top, Width, Height) : RestoreBounds;
        if (bounds.IsEmpty || double.IsNaN(bounds.Left) || bounds.Width <= 0)
        {
            return;
        }

        var placement = new WindowPlacement(bounds.Left, bounds.Top, bounds.Width, bounds.Height, WindowState == WindowState.Maximized);
        if (placement == _settings.WindowPlacement)
        {
            return;
        }

        _settings.WindowPlacement = placement;
        _save(_settings);
    }
}
