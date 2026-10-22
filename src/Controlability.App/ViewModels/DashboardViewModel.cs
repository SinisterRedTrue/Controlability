using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Core.Settings;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

/// <summary>
/// The dashboard: four player cards, the detected devices, the mode switch, the pause toggle and the driver state.
/// It only talks to <see cref="IEngine"/>; engine changes arrive on the input thread and are folded into at most one
/// UI refresh per <see cref="RefreshInterval"/>.
/// </summary>
public sealed partial class DashboardViewModel : ObservableObject, IDisposable
{
    public const string ViGEmReleasesUrl = "https://github.com/nefarius/ViGEmBus/releases";
    public const string HidHideReleasesUrl = "https://github.com/nefarius/HidHide/releases";

    /// <summary>About 20 Hz.</summary>
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(50);

    /// <summary>Battery levels do not raise <see cref="IEngine.Changed"/>, so they are polled slowly.</summary>
    private static readonly TimeSpan BatteryPollInterval = TimeSpan.FromSeconds(5);

    private readonly IEngine _engine;
    private readonly IProfileCatalog _profiles;
    private readonly IUrlLauncher _launcher;
    private readonly ThrottledAction _throttle;
    private readonly IDisposable _batteryTimer;
    private readonly Dictionary<DeviceId, DeviceInfo> _knownDevices = [];
    private IReadOnlyList<ProfileChoice> _profileChoices;
    private bool _refreshing;
    private bool _disposed;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModeA), nameof(IsModeB))]
    private KeyboardMode _mode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStyleXbox), nameof(IsStyleKeyboard))]
    private KeyboardInputStyle _keyboardStyle;

    [ObservableProperty]
    private bool _viGEmAvailable = true;

    [ObservableProperty]
    private bool _hidHideAvailable = true;

    public DashboardViewModel(IEngine engine, IProfileCatalog profiles, IUiThread ui, IClock clock, IUrlLauncher launcher)
    {
        _engine = engine;
        _profiles = profiles;
        _launcher = launcher;
        _profileChoices = profiles.GetProfiles();

        Cards = [.. Enumerable.Range(0, 4).Select(index => new PlayerCardViewModel(index, engine))];
        Devices = [];
        _throttle = new ThrottledAction(ui, clock, RefreshInterval, Refresh);

        Refresh();
        _engine.Changed += _throttle.Request;
        _batteryTimer = ui.Every(BatteryPollInterval, Refresh);
    }

    public IReadOnlyList<PlayerCardViewModel> Cards { get; }

    public ObservableCollection<DeviceItemViewModel> Devices { get; }

    public bool HasDevices => Devices.Count > 0;

    public bool HasNoDevices => Devices.Count == 0;

    public bool IsModeA => Mode == KeyboardMode.KeyboardFirst;

    public bool IsModeB => Mode == KeyboardMode.KeyboardAsController;

    public bool IsStyleXbox => KeyboardStyle == KeyboardInputStyle.XboxController;

    public bool IsStyleKeyboard => KeyboardStyle == KeyboardInputStyle.NormalKeyboard;

    /// <summary>"Game profile: eldenring" while a game with its own profile has the focus, else empty.</summary>
    public string ActiveGameText { get; private set; } = "";

    public bool HasActiveGame => ActiveGameText.Length > 0;

    public bool ShowViGEmBanner => !ViGEmAvailable;

    public bool ShowHidHideBanner => !HidHideAvailable;

    public string ViGEmChipText => $"ViGEmBus {(ViGEmAvailable ? "✓" : "✗")}";

    public string HidHideChipText => $"HidHide {(HidHideAvailable ? "✓" : "✗")}";

    public string ViGEmChipAutomationName => Strings.Format(ViGEmAvailable ? Strings.DriverInstalled : Strings.DriverMissing, "ViGEmBus");

    public string HidHideChipAutomationName => Strings.Format(HidHideAvailable ? Strings.DriverInstalled : Strings.DriverMissing, "HidHide");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _engine.Changed -= _throttle.Request;
        _batteryTimer.Dispose();
    }

    /// <summary>Re-reads the profile list, for when profiles were added or renamed.</summary>
    public void ReloadProfiles()
    {
        _profileChoices = _profiles.GetProfiles();
        Refresh();
    }

    /// <summary>Drag and drop target: gives <paramref name="device"/> to the slot.</summary>
    public void AssignDevice(DeviceId device, int slot) => _engine.Assign(device, slot);

    [RelayCommand]
    private void SetMode(KeyboardMode mode) => _engine.SetMode(mode);

    [RelayCommand]
    private void SetKeyboardStyle(KeyboardInputStyle style) => _engine.SetKeyboardStyle(style);

    [RelayCommand]
    private void OpenViGEmPage() => _launcher.Open(ViGEmReleasesUrl);

    [RelayCommand]
    private void OpenHidHidePage() => _launcher.Open(HidHideReleasesUrl);

    partial void OnIsPausedChanged(bool value)
    {
        // A refresh copies the engine's state into the toggle; that must not be sent back to the engine.
        if (!_refreshing)
        {
            _engine.Pause(value);
        }
    }

    /// <summary>Copies the engine's state into the view. UI thread only.</summary>
    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        _refreshing = true;
        try
        {
            var devices = _engine.Devices;
            var slots = _engine.Slots;
            foreach (var info in devices)
            {
                _knownDevices[info.Id] = info;
            }

            for (var i = 0; i < Cards.Count; i++)
            {
                var slot = slots[i];
                DeviceInfo? info = null;
                BatteryInfo? battery = null;
                if (slot.Device is { } id)
                {
                    info = _knownDevices.GetValueOrDefault(id);
                    if (_engine.TryGetLiveState(id, out var raw, out _))
                    {
                        battery = raw.Battery;
                    }
                }

                Cards[i].Update(slot, info, battery, _profileChoices);
            }

            RefreshDevices(devices, slots);

            ActiveGameText = _engine.ActiveGame is { Length: > 0 } game ? Strings.Format(Strings.DashboardActiveGame, game) : "";
            OnPropertyChanged(nameof(ActiveGameText));
            OnPropertyChanged(nameof(HasActiveGame));

            IsPaused = _engine.IsPaused;
            Mode = _engine.Mode;
            KeyboardStyle = _engine.KeyboardStyle;
            var drivers = _engine.DriverStatus;
            ViGEmAvailable = drivers.ViGEmAvailable;
            HidHideAvailable = drivers.HidHideAvailable;
            OnPropertyChanged(nameof(ShowViGEmBanner));
            OnPropertyChanged(nameof(ShowHidHideBanner));
            OnPropertyChanged(nameof(ViGEmChipText));
            OnPropertyChanged(nameof(HidHideChipText));
            OnPropertyChanged(nameof(ViGEmChipAutomationName));
            OnPropertyChanged(nameof(HidHideChipAutomationName));
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void RefreshDevices(IReadOnlyList<DeviceInfo> devices, IReadOnlyList<SlotStatus> slots)
    {
        var before = Devices.Count;
        for (var i = Devices.Count - 1; i >= 0; i--)
        {
            if (!devices.Any(info => info.Id == Devices[i].Id))
            {
                Devices.RemoveAt(i);
            }
        }

        foreach (var info in devices)
        {
            var slot = slots.FirstOrDefault(s => s.Device == info.Id)?.Index;
            var existing = Devices.FirstOrDefault(item => item.Id == info.Id);
            if (existing is null)
            {
                existing = new DeviceItemViewModel(info, _engine);
                Devices.Add(existing);
            }

            existing.Update(info, slot);
        }

        if (before != Devices.Count || before == 0)
        {
            OnPropertyChanged(nameof(HasDevices));
            OnPropertyChanged(nameof(HasNoDevices));
        }
    }
}
