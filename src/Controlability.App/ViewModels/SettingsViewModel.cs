using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Abstractions;
using Controlability.Core.Keyboard;
using Controlability.Core.Mapping;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

/// <summary>One line of the keyboard table: an Xbox control and the key(s) that drive it.</summary>
public sealed partial class KeyBindingRow(SettingsViewModel owner, KeyboardControl control) : ObservableObject
{
    [ObservableProperty]
    private string _keyText = "";

    [ObservableProperty]
    private bool _isCapturing;

    public KeyboardControl Control { get; } = control;

    public string Label { get; } = ControlLabels.Xbox(control);

    public IRelayCommand ChangeCommand { get; } = new RelayCommand(() => owner.BeginCapture(control));

    public IRelayCommand ClearCommand { get; } = new RelayCommand(() => owner.ClearKey(control));
}

/// <summary>One line of the controller table: an Xbox control and the physical button that sends it.</summary>
public sealed record ControllerRow(string Label, string Button);

/// <summary>
/// The settings page: close-to-tray, the game that Mode B listens for, the keyboard keys and a read-only list of what
/// the controller's buttons send. Key edits go to the keyboard profile and are applied to the engine at once.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject, IDisposable
{
    private const string DefaultKeyboardProfileId = "default-keyboard";
    private const string DefaultControllerProfileId = "default-playstation";

    private static readonly KeyboardControl[] Controls = [.. Enum.GetValues<KeyboardControl>().Where(c => c != KeyboardControl.None)];

    private readonly IEngine _engine;
    private readonly Settings _settings;
    private readonly Action<Settings> _save;
    private readonly IProfileEditor _profiles;
    private readonly IRunningApps _apps;
    private readonly ThrottledAction _throttle;
    private bool _loading;
    private bool _disposed;
    private string? _keyboardProfileId;
    private int? _keyboardSlot;
    private KeyboardControl? _capturing;

    [ObservableProperty]
    private bool _closeToTray;

    [ObservableProperty]
    private string _newProcessName = "";

    [ObservableProperty]
    private string? _selectedRunningApp;

    [ObservableProperty]
    private string _capturePrompt = "";

    [ObservableProperty]
    private string _keyboardProfileText = "";

    [ObservableProperty]
    private string _controllerSummary = "";

    public SettingsViewModel(
        IEngine engine,
        Settings settings,
        Action<Settings> save,
        IProfileEditor profiles,
        IRunningApps apps,
        IUiThread ui,
        IClock clock)
    {
        _engine = engine;
        _settings = settings;
        _save = save;
        _profiles = profiles;
        _apps = apps;

        _loading = true;
        CloseToTray = settings.CloseToTray;
        _loading = false;
        foreach (var name in settings.KeyboardTargetProcesses)
        {
            TargetProcesses.Add(name);
        }

        KeyRows = [.. Controls.Select(control => new KeyBindingRow(this, control))];
        _throttle = new ThrottledAction(ui, clock, DashboardViewModel.RefreshInterval, Refresh);
        RefreshRunningApps();
        Refresh();
        _engine.Changed += _throttle.Request;
    }

    public ObservableCollection<string> TargetProcesses { get; } = [];

    public ObservableCollection<string> RunningApps { get; } = [];

    public IReadOnlyList<KeyBindingRow> KeyRows { get; }

    public ObservableCollection<ControllerRow> ControllerRows { get; } = [];

    public bool HasTargets => TargetProcesses.Count > 0;

    public bool HasNoTargets => TargetProcesses.Count == 0;

    public bool IsCapturing => _capturing is not null;

    public bool HasKeyboardProfile => _keyboardProfileId is not null && _profiles.Get(_keyboardProfileId) is not null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _engine.Changed -= _throttle.Request;
    }

    partial void OnCloseToTrayChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        _settings.CloseToTray = value;
        _save(_settings);
    }

    [RelayCommand]
    private void AddProcess()
    {
        AddTarget(NewProcessName);
        NewProcessName = "";
    }

    [RelayCommand]
    private void AddSelectedProcess()
    {
        if (SelectedRunningApp is { } name)
        {
            AddTarget(name);
        }
    }

    [RelayCommand]
    private void RemoveProcess(string? name)
    {
        if (name is not null && TargetProcesses.Remove(name))
        {
            ApplyTargets();
        }
    }

    [RelayCommand]
    private void RefreshRunningApps()
    {
        RunningApps.Clear();
        foreach (var name in _apps.GetNames())
        {
            RunningApps.Add(name);
        }
    }

    [RelayCommand]
    private void ResetKeys()
    {
        if (_keyboardProfileId is not { } id || _profiles.Get(id) is not { } profile || _profiles.GetShipped(id) is not { } shipped)
        {
            return;
        }

        CancelCapture();
        profile.Keyboard = shipped.Keyboard with { Keys = new Dictionary<string, string>(shipped.Keyboard.Keys) };
        SaveKeyboardProfile(profile);
    }

    /// <summary>Starts waiting for the key or mouse button that should drive <paramref name="control"/>.</summary>
    public void BeginCapture(KeyboardControl control)
    {
        _capturing = control;
        CapturePrompt = Strings.Format(Strings.KeyPressPrompt, ControlLabels.Xbox(control));
        foreach (var row in KeyRows)
        {
            row.IsCapturing = row.Control == control;
        }

        OnPropertyChanged(nameof(IsCapturing));
    }

    [RelayCommand]
    public void CancelCapture()
    {
        _capturing = null;
        CapturePrompt = "";
        foreach (var row in KeyRows)
        {
            row.IsCapturing = false;
        }

        OnPropertyChanged(nameof(IsCapturing));
    }

    /// <summary>
    /// Binds <paramref name="inputName"/> (a profile key or mouse button name such as "F" or "MouseLeft") to the control
    /// being captured. The input is taken away from any other control and the control's old keys are dropped.
    /// </summary>
    public bool ApplyCapture(string inputName)
    {
        if (_capturing is not { } control || _keyboardProfileId is not { } id || _profiles.Get(id) is not { } profile)
        {
            return false;
        }

        var keys = profile.Keyboard.Keys;
        foreach (var existing in keys.Keys.ToList())
        {
            if (SameInput(existing, inputName) || (KeyboardControls.TryParse(keys[existing], out var bound) && bound == control))
            {
                keys.Remove(existing);
            }
        }

        keys[inputName] = control.ToString();
        CancelCapture();
        SaveKeyboardProfile(profile);
        return true;
    }

    /// <summary>Removes every key bound to <paramref name="control"/>.</summary>
    public void ClearKey(KeyboardControl control)
    {
        if (_keyboardProfileId is not { } id || _profiles.Get(id) is not { } profile)
        {
            return;
        }

        foreach (var existing in profile.Keyboard.Keys.Keys.ToList())
        {
            if (KeyboardControls.TryParse(profile.Keyboard.Keys[existing], out var bound) && bound == control)
            {
                profile.Keyboard.Keys.Remove(existing);
            }
        }

        SaveKeyboardProfile(profile);
    }

    /// <summary>Re-reads the engine and the profiles into the tables. UI thread only.</summary>
    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var slots = _engine.Slots;
        var keyboardSlot = slots.FirstOrDefault(slot => slot.Device == KeyboardPad.Id);
        _keyboardSlot = keyboardSlot?.Index;
        _keyboardProfileId = keyboardSlot?.ProfileId ?? DefaultKeyboardProfileId;
        RefreshKeyRows();
        RefreshControllerRows(slots);
        OnPropertyChanged(nameof(HasKeyboardProfile));
    }

    internal static bool SameInput(string a, string b)
    {
        var aIsMouse = MouseButtonNames.TryParse(a, out var mouseA);
        var bIsMouse = MouseButtonNames.TryParse(b, out var mouseB);
        if (aIsMouse || bIsMouse)
        {
            return aIsMouse && bIsMouse && mouseA == mouseB;
        }

        return KeyCodes.TryParse(a, out var keyA) && KeyCodes.TryParse(b, out var keyB) && keyA == keyB;
    }

    private void AddTarget(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[..^4];
        }

        if (trimmed.Length == 0 || TargetProcesses.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        TargetProcesses.Add(trimmed);
        ApplyTargets();
    }

    private void ApplyTargets()
    {
        OnPropertyChanged(nameof(HasTargets));
        OnPropertyChanged(nameof(HasNoTargets));
        _engine.SetKeyboardTargets([.. TargetProcesses]);
    }

    private void SaveKeyboardProfile(Profile profile)
    {
        _profiles.Save(profile);
        if (_keyboardSlot is { } slot)
        {
            _engine.SetProfile(slot, profile.Id);
        }

        RefreshKeyRows();
    }

    private void RefreshKeyRows()
    {
        var profile = _keyboardProfileId is null ? null : _profiles.Get(_keyboardProfileId);
        KeyboardProfileText = profile is null ? Strings.KeysNoProfile : Strings.Format(Strings.KeysProfile, profile.Name);
        foreach (var row in KeyRows)
        {
            var keys = profile is null
                ? []
                : profile.Keyboard.Keys
                    .Where(pair => KeyboardControls.TryParse(pair.Value, out var control) && control == row.Control)
                    .Select(pair => pair.Key)
                    .ToList();
            row.KeyText = keys.Count == 0 ? Strings.KeyNotBound : string.Join(", ", keys);
        }
    }

    private void RefreshControllerRows(IReadOnlyList<SlotStatus> slots)
    {
        var padSlot = slots.FirstOrDefault(slot => slot.Device is { } device && device != KeyboardPad.Id);
        var info = padSlot?.Device is { } id ? _engine.Devices.FirstOrDefault(d => d.Id == id) : null;
        var type = info?.Type ?? ControllerType.PS4;
        var profile = (padSlot?.ProfileId is { } profileId ? _profiles.Get(profileId) : null)
            ?? _profiles.Get(DefaultControllerProfileId)
            ?? Profile.Generic;

        ControllerSummary = padSlot is null
            ? Strings.PadNone
            : Strings.Format(Strings.PadSummary, padSlot.DeviceName ?? "", profile.Name);

        ButtonId[] sources =
        [
            ButtonId.South, ButtonId.East, ButtonId.West, ButtonId.North, ButtonId.Back, ButtonId.Start, ButtonId.Guide,
            ButtonId.LeftStick, ButtonId.RightStick, ButtonId.LeftShoulder, ButtonId.RightShoulder,
            ButtonId.DpadUp, ButtonId.DpadDown, ButtonId.DpadLeft, ButtonId.DpadRight, ButtonId.Touchpad,
        ];

        MappingSettings mapping;
        try
        {
            mapping = ProfileMapper.ToMappingSettings(profile);
        }
        catch (ProfileFormatException)
        {
            mapping = MappingSettings.Default;
        }

        ControllerRows.Clear();
        foreach (var control in Controls.Where(c => KeyboardControls.ToButton(c) != ButtonId.None))
        {
            var target = XboxButtonOf(control);
            var buttons = sources
                .Where(source => (mapping.Buttons[source] & target) != 0)
                .Select(source => ControlLabels.Physical(type, source))
                .ToList();
            ControllerRows.Add(new ControllerRow(ControlLabels.Xbox(control), buttons.Count == 0 ? Strings.PadNotMapped : string.Join(", ", buttons)));
        }

        var (leftTrigger, rightTrigger) = type switch
        {
            ControllerType.PS4 or ControllerType.PS5 => ("L2", "R2"),
            ControllerType.SwitchPro => ("ZL", "ZR"),
            _ => ("LT", "RT"),
        };
        ControllerRows.Add(new ControllerRow("Left stick", "Left stick"));
        ControllerRows.Add(new ControllerRow("Right stick", "Right stick"));
        ControllerRows.Add(new ControllerRow(ControlLabels.Xbox(KeyboardControl.LeftTrigger), leftTrigger));
        ControllerRows.Add(new ControllerRow(ControlLabels.Xbox(KeyboardControl.RightTrigger), rightTrigger));
    }

    internal static XboxButton XboxButtonOf(KeyboardControl control) => control switch
    {
        KeyboardControl.A => XboxButton.A,
        KeyboardControl.B => XboxButton.B,
        KeyboardControl.X => XboxButton.X,
        KeyboardControl.Y => XboxButton.Y,
        KeyboardControl.Back => XboxButton.Back,
        KeyboardControl.Start => XboxButton.Start,
        KeyboardControl.Guide => XboxButton.Guide,
        KeyboardControl.LeftThumb => XboxButton.LeftThumb,
        KeyboardControl.RightThumb => XboxButton.RightThumb,
        KeyboardControl.LeftShoulder => XboxButton.LeftShoulder,
        KeyboardControl.RightShoulder => XboxButton.RightShoulder,
        KeyboardControl.DpadUp => XboxButton.DpadUp,
        KeyboardControl.DpadDown => XboxButton.DpadDown,
        KeyboardControl.DpadLeft => XboxButton.DpadLeft,
        KeyboardControl.DpadRight => XboxButton.DpadRight,
        _ => XboxButton.None,
    };
}
