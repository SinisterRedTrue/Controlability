using System.ComponentModel;
using Controlability.App.Services;
using Controlability.App.Views;
using Controlability.Core.Settings;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Controlability.App;

public partial class MainWindow : FluentWindow
{
    private readonly Settings _settings;
    private readonly ShellController _shell;

    public MainWindow(Settings settings, ShellController shell, DiPageProvider pages)
    {
        _settings = settings;
        _shell = shell;
        InitializeComponent();

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
        if (!_shell.IsExiting && _settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
            _shell.OnHiddenToTray();
        }
    }
}
