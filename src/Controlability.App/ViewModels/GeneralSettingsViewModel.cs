using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Settings;
using Controlability.Engine;
using Controlability.Platform;
using Controlability.Platform.Logging;

namespace Controlability.App.ViewModels;

/// <summary>A value with the text a combo box shows for it.</summary>
public sealed record Choice<T>(string Label, T Value);

/// <summary>
/// Every field of <c>settings.json</c> that the user can change, plus unhide-all, the logs and the diagnostics. A change is
/// saved at once and pushed to the engine (<see cref="IEngine.ApplySettings"/>) so it applies without a restart where it can.
/// </summary>
public sealed partial class GeneralSettingsViewModel : ObservableObject
{
    private readonly IEngine _engine;
    private readonly Settings _settings;
    private readonly Action<Settings> _save;
    private readonly IAutostart _autostart;
    private readonly IAppearance _appearance;
    private readonly RollingFileLog _log;
    private readonly DiagnosticsService _diagnostics;
    private readonly IDialogs _dialogs;
    private readonly IUrlLauncher _launcher;
    private bool _loading;

    [ObservableProperty]
    private bool _startWithWindows;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private Choice<KeyboardMode> _mode;

    [ObservableProperty]
    private Choice<SlotPolicy> _slotPolicyChoice;

    [ObservableProperty]
    private Choice<int> _pollingRate;

    [ObservableProperty]
    private Choice<int> _gracePeriod;

    [ObservableProperty]
    private bool _hidHideEnabled;

    [ObservableProperty]
    private bool _unhideWhilePaused;

    [ObservableProperty]
    private Choice<AppTheme> _theme;

    [ObservableProperty]
    private Choice<string> _language;

    [ObservableProperty]
    private Choice<AppLogLevel> _logLevel;

    [ObservableProperty]
    private string _statusText = "";

    public GeneralSettingsViewModel(
        IEngine engine,
        Settings settings,
        Action<Settings> save,
        IAutostart autostart,
        IAppearance appearance,
        RollingFileLog log,
        DiagnosticsService diagnostics,
        IDialogs dialogs,
        IUrlLauncher launcher)
    {
        _engine = engine;
        _settings = settings;
        _save = save;
        _autostart = autostart;
        _appearance = appearance;
        _log = log;
        _diagnostics = diagnostics;
        _dialogs = dialogs;
        _launcher = launcher;

        _mode = ModeChoices[0];
        _slotPolicyChoice = SlotPolicyChoices[0];
        _pollingRate = PollingRateChoices[0];
        _gracePeriod = GracePeriodChoices[0];
        _theme = ThemeChoices[0];
        _language = LanguageChoices[0];
        _logLevel = LogLevelChoices[0];
        Load();
    }

    public IReadOnlyList<Choice<KeyboardMode>> ModeChoices { get; } =
    [
        new(Strings.ModeATitle, KeyboardMode.KeyboardFirst),
        new(Strings.ModeBTitle, KeyboardMode.KeyboardAsController),
    ];

    public IReadOnlyList<Choice<SlotPolicy>> SlotPolicyChoices { get; } =
    [
        new(Strings.PolicyAuto, SlotPolicy.Auto),
        new(Strings.PolicyRemember, SlotPolicy.Remember),
        new(Strings.PolicyManual, SlotPolicy.Manual),
    ];

    public IReadOnlyList<Choice<int>> PollingRateChoices { get; } =
    [
        new("1000 Hz", 1000),
        new("500 Hz", 500),
        new("250 Hz", 250),
    ];

    public IReadOnlyList<Choice<int>> GracePeriodChoices { get; } =
    [
        new(Strings.GraceNone, 0),
        new(Strings.Format(Strings.GraceSeconds, 5), 5),
        new(Strings.Format(Strings.GraceSeconds, 10), 10),
        new(Strings.Format(Strings.GraceSeconds, 20), 20),
        new(Strings.Format(Strings.GraceSeconds, 30), 30),
    ];

    public IReadOnlyList<Choice<AppTheme>> ThemeChoices { get; } =
    [
        new(Strings.ThemeSystem, AppTheme.System),
        new(Strings.ThemeLight, AppTheme.Light),
        new(Strings.ThemeDark, AppTheme.Dark),
    ];

    public IReadOnlyList<Choice<string>> LanguageChoices { get; } =
    [
        new(Strings.LanguageSystem, "system"),
        new("English", "en"),
    ];

    public IReadOnlyList<Choice<AppLogLevel>> LogLevelChoices { get; } =
    [
        new(Strings.LogDebug, AppLogLevel.Debug),
        new(Strings.LogInformation, AppLogLevel.Information),
        new(Strings.LogWarning, AppLogLevel.Warning),
        new(Strings.LogError, AppLogLevel.Error),
    ];

    public string VersionText => Strings.Format(Strings.AboutVersion, AppInfo.Version);

    public IReadOnlyList<ThirdPartyNotice> Notices => ThirdPartyNotices.All;

    public string LogsFolder => _log.Folder;

    /// <summary>Re-reads what the dashboard or the tray may have changed (the mode).</summary>
    public void Refresh()
    {
        _loading = true;
        Mode = ModeChoices.First(c => c.Value == _engine.Mode);
        StartWithWindows = _autostart.IsEnabled;
        _loading = false;
    }

    partial void OnStartWithWindowsChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        if (_autostart.Set(value))
        {
            _settings.StartWithWindows = value;
            _save(_settings);
            StatusText = "";
        }
        else
        {
            StatusText = Strings.AutostartFailed;
            _loading = true;
            StartWithWindows = _autostart.IsEnabled;
            _loading = false;
        }
    }

    partial void OnStartMinimizedChanged(bool value) => Change(() => _settings.StartMinimized = value);

    partial void OnModeChanged(Choice<KeyboardMode> value)
    {
        if (!_loading && value.Value != _engine.Mode)
        {
            _engine.SetMode(value.Value);
        }
    }

    partial void OnSlotPolicyChoiceChanged(Choice<SlotPolicy> value) => Change(() => _settings.SlotPolicy = value.Value, applyToEngine: true);

    partial void OnPollingRateChanged(Choice<int> value) => Change(() => _settings.PollingRateHz = value.Value, applyToEngine: true);

    partial void OnGracePeriodChanged(Choice<int> value) => Change(() => _settings.GracePeriodSeconds = value.Value, applyToEngine: true);

    partial void OnHidHideEnabledChanged(bool value) => Change(() => _settings.HidHideEnabled = value, applyToEngine: true);

    partial void OnUnhideWhilePausedChanged(bool value) => Change(() => _settings.UnhideWhilePaused = value, applyToEngine: true);

    partial void OnThemeChanged(Choice<AppTheme> value)
    {
        Change(() => _settings.Theme = value.Value);
        if (!_loading)
        {
            _appearance.Apply(value.Value);
        }
    }

    partial void OnLanguageChanged(Choice<string> value)
    {
        Change(() => _settings.Language = value.Value);
        if (!_loading)
        {
            StatusText = Strings.LanguageRestart;
        }
    }

    partial void OnLogLevelChanged(Choice<AppLogLevel> value)
    {
        Change(() => _settings.LogLevel = value.Value);
        _log.Level = value.Value;
    }

    [RelayCommand]
    private void UnhideAll()
    {
        if (!_dialogs.Confirm(Strings.UnhideAllTitle, Strings.UnhideAllConfirm))
        {
            return;
        }

        _engine.UnhideAll();
        StatusText = Strings.UnhideAllDone;
    }

    [RelayCommand]
    private void OpenLogs() => _launcher.Open(_log.Folder);

    [RelayCommand]
    private async Task CopyDiagnosticsAsync() => StatusText = await _diagnostics.CopyToClipboardAsync() ? Strings.DiagnosticsCopied : Strings.DiagnosticsCopyFailed;

    [RelayCommand]
    private void OpenLink(string? url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            _launcher.Open(url);
        }
    }

    private void Load()
    {
        _loading = true;
        StartWithWindows = _autostart.IsEnabled;
        StartMinimized = _settings.StartMinimized;
        Mode = ModeChoices.First(c => c.Value == _engine.Mode);
        SlotPolicyChoice = SlotPolicyChoices.FirstOrDefault(c => c.Value == _settings.SlotPolicy) ?? SlotPolicyChoices[0];
        PollingRate = PollingRateChoices.FirstOrDefault(c => c.Value == _settings.PollingRateHz) ?? PollingRateChoices[0];
        GracePeriod = GracePeriodChoices.OrderBy(c => Math.Abs(c.Value - _settings.GracePeriodSeconds)).First();
        HidHideEnabled = _settings.HidHideEnabled;
        UnhideWhilePaused = _settings.UnhideWhilePaused;
        Theme = ThemeChoices.First(c => c.Value == _settings.Theme);
        Language = LanguageChoices.FirstOrDefault(c => c.Value == _settings.Language) ?? LanguageChoices[0];
        LogLevel = LogLevelChoices.First(c => c.Value == _settings.LogLevel);
        _loading = false;
    }

    private void Change(Action apply, bool applyToEngine = false)
    {
        if (_loading)
        {
            return;
        }

        apply();
        _save(_settings);
        if (applyToEngine)
        {
            _engine.ApplySettings();
        }
    }
}
