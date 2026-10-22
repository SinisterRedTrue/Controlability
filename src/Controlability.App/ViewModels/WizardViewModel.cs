using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Core.Settings;
using Controlability.Engine;
using Controlability.Platform;

namespace Controlability.App.ViewModels;

public enum WizardStep
{
    Welcome,
    Drivers,
    Controller,
    Mode,
    Done,
}

/// <summary>
/// The first-run wizard: welcome, driver check (re-checked every 2 s so it turns green after the install), wait for a
/// controller, pick the mode, done. Finishing saves <c>settings.json</c>, so the wizard is shown only once.
/// </summary>
public sealed partial class WizardViewModel : ObservableObject, IDisposable
{
    public static readonly TimeSpan DriverRecheckInterval = TimeSpan.FromSeconds(2);

    private readonly IEngine _engine;
    private readonly IDriverProbe _drivers;
    private readonly Settings _settings;
    private readonly Action<Settings> _save;
    private readonly IAutostart _autostart;
    private readonly IUrlLauncher _launcher;
    private readonly IUiThread _ui;
    private readonly ThrottledAction _throttle;
    private IDisposable? _driverTimer;
    private bool _checking;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWelcome), nameof(IsDrivers), nameof(IsController), nameof(IsMode), nameof(IsDone), nameof(CanGoBack), nameof(StepNumberText), nameof(StepTitle), nameof(NextText))]
    private WizardStep _step;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViGEmText))]
    private bool _viGEmInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ViGEmText))]
    private string _viGEmVersion = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HidHideText))]
    private bool _hidHideInstalled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HidHideText))]
    private string _hidHideVersion = "";

    [ObservableProperty]
    private bool _driversChecked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasController), nameof(NoController), nameof(ControllerText))]
    private DeviceInfo? _controller;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ModeA))]
    private bool _modeB;

    [ObservableProperty]
    private bool _startWithWindows;

    public WizardViewModel(
        IEngine engine,
        IDriverProbe drivers,
        Settings settings,
        Action<Settings> save,
        IAutostart autostart,
        IUrlLauncher launcher,
        IUiThread ui,
        IClock clock)
    {
        _engine = engine;
        _drivers = drivers;
        _settings = settings;
        _save = save;
        _autostart = autostart;
        _launcher = launcher;
        _ui = ui;
        _throttle = new ThrottledAction(ui, clock, DashboardViewModel.RefreshInterval, RefreshController);
        Live = new LivePadViewModel(engine);
        ModeB = engine.Mode == KeyboardMode.KeyboardAsController;
        RefreshController();
        _engine.Changed += _throttle.Request;
    }

    /// <summary>Raised when the wizard is finished (or skipped); the window closes.</summary>
    public event Action? Completed;

    public LivePadViewModel Live { get; }

    /// <summary>The mirror of <see cref="ModeB"/> for the first radio button.</summary>
    public bool ModeA
    {
        get => !ModeB;
        set => ModeB = !value;
    }

    public bool IsWelcome => Step == WizardStep.Welcome;

    public bool IsDrivers => Step == WizardStep.Drivers;

    public bool IsController => Step == WizardStep.Controller;

    public bool IsMode => Step == WizardStep.Mode;

    public bool IsDone => Step == WizardStep.Done;

    public bool CanGoBack => Step != WizardStep.Welcome;

    public string NextText => Step == WizardStep.Done ? Strings.WizardFinish : Strings.WizardNext;

    public string StepNumberText => Strings.Format(Strings.WizardStepOf, (int)Step + 1, Enum.GetValues<WizardStep>().Length);

    public string StepTitle => Step switch
    {
        WizardStep.Welcome => Strings.WizardWelcomeTitle,
        WizardStep.Drivers => Strings.WizardDriversTitle,
        WizardStep.Controller => Strings.WizardControllerTitle,
        WizardStep.Mode => Strings.WizardModeTitle,
        _ => Strings.WizardDoneTitle,
    };

    public string ViGEmText => ViGEmInstalled ? Strings.Format(Strings.WizardDriverInstalled, ViGEmVersion) : Strings.WizardDriverMissing;

    public string HidHideText => HidHideInstalled ? Strings.Format(Strings.WizardDriverInstalled, HidHideVersion) : Strings.WizardDriverMissing;

    public bool HasController => Controller is not null;

    public bool NoController => Controller is null;

    public string ControllerText => Controller is { } device ? Strings.Format(Strings.WizardControllerFound, device.Name) : Strings.WizardControllerWaiting;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _engine.Changed -= _throttle.Request;
        StopDriverTimer();
    }

    partial void OnStepChanged(WizardStep value)
    {
        StopDriverTimer();
        if (value == WizardStep.Drivers)
        {
            _ = RecheckDriversAsync();
            _driverTimer = _ui.Every(DriverRecheckInterval, () => _ = RecheckDriversAsync());
        }

        Live.Device = value == WizardStep.Controller ? Controller?.Id : null;
    }

    partial void OnControllerChanged(DeviceInfo? value)
    {
        if (Step == WizardStep.Controller)
        {
            Live.Device = value?.Id;
        }
    }

    /// <summary>Reads the drivers off the UI thread (the HidHide CLI can take a moment) and shows the result.</summary>
    public async Task RecheckDriversAsync()
    {
        if (_checking || _disposed)
        {
            return;
        }

        _checking = true;
        try
        {
            var info = await Task.Run(_drivers.Read);
            if (_disposed)
            {
                return;
            }

            ViGEmInstalled = info.ViGEmInstalled;
            ViGEmVersion = info.ViGEmVersion;
            HidHideInstalled = info.HidHideInstalled;
            HidHideVersion = info.HidHideVersion;
            DriversChecked = true;
        }
        finally
        {
            _checking = false;
        }
    }

    [RelayCommand]
    private void Next()
    {
        if (Step == WizardStep.Done)
        {
            Finish();
            return;
        }

        Step++;
    }

    [RelayCommand]
    private void Back()
    {
        if (Step != WizardStep.Welcome)
        {
            Step--;
        }
    }

    [RelayCommand]
    private void Skip() => Finish();

    [RelayCommand]
    private void InstallViGEm() => _launcher.Open(DashboardViewModel.ViGEmReleasesUrl);

    [RelayCommand]
    private void InstallHidHide() => _launcher.Open(DashboardViewModel.HidHideReleasesUrl);

    /// <summary>Applies the choices, saves <c>settings.json</c> and closes the wizard.</summary>
    public void Finish()
    {
        var mode = ModeB ? KeyboardMode.KeyboardAsController : KeyboardMode.KeyboardFirst;
        if (mode != _engine.Mode)
        {
            _engine.SetMode(mode);
        }

        _settings.Mode = mode;
        if (StartWithWindows && _autostart.Set(true))
        {
            _settings.StartWithWindows = true;
        }

        _save(_settings);
        Completed?.Invoke();
    }

    private void StopDriverTimer()
    {
        _driverTimer?.Dispose();
        _driverTimer = null;
    }

    private void RefreshController()
    {
        if (_disposed)
        {
            return;
        }

        var devices = _engine.Devices.Where(device => device.Id != KeyboardPad.Id).ToList();
        Controller = devices.FirstOrDefault(device => Controller is { } current && current.Id == device.Id) ?? devices.FirstOrDefault();
    }
}
