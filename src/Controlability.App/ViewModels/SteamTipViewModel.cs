using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Controlability.App.Services;
using Controlability.Core.Settings;

namespace Controlability.App.ViewModels;

/// <summary>
/// The dashboard tip shown while Steam is running: Steam's PlayStation configuration support can grab the DualShock
/// before this app does and shows the game a second, unmapped pad. Dismissing it is remembered in the settings.
/// </summary>
public sealed partial class SteamTipViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(10);

    private readonly IRunningApps _apps;
    private readonly Settings _settings;
    private readonly Action<Settings> _save;
    private readonly IDisposable _timer;

    [ObservableProperty]
    private bool _isVisible;

    /// <summary>"Review setup" shows the full instructions under the summary.</summary>
    [ObservableProperty]
    private bool _isExpanded;

    public SteamTipViewModel(IRunningApps apps, Settings settings, Action<Settings> save, IUiThread ui)
    {
        _apps = apps;
        _settings = settings;
        _save = save;
        Refresh();
        _timer = ui.Every(CheckInterval, Refresh);
    }

    public void Refresh()
        => IsVisible = !_settings.SteamTipDismissed && _apps.IsRunning("steam");

    public void Dispose() => _timer.Dispose();

    [RelayCommand]
    private void ToggleDetails() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void Dismiss()
    {
        _settings.SteamTipDismissed = true;
        _save(_settings);
        IsVisible = false;
    }
}
