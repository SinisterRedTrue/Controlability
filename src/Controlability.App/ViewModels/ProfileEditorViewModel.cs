using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Abstractions;
using Controlability.Core.Keyboard;
using Controlability.Core.Mapping;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

/// <summary>One line of the Buttons tab: an Xbox button and the physical button(s) that send it.</summary>
public sealed partial class ButtonBindingRow(ProfileEditorViewModel owner, KeyboardControl control) : ObservableObject
{
    [ObservableProperty]
    private string _sourceText = "";

    [ObservableProperty]
    private bool _isBinding;

    public KeyboardControl Control { get; } = control;

    public string Label { get; } = ControlLabels.Xbox(control);

    public IRelayCommand BindCommand { get; } = new RelayCommand(() => owner.BeginBind(control));

    public IRelayCommand ClearCommand { get; } = new RelayCommand(() => owner.ClearBinding(control));
}

/// <summary>One line of the Keyboard tab: an Xbox control and the key(s) that drive it.</summary>
public sealed partial class EditorKeyRow(ProfileEditorViewModel owner, KeyboardControl control) : ObservableObject
{
    [ObservableProperty]
    private string _keyText = "";

    [ObservableProperty]
    private bool _isCapturing;

    public KeyboardControl Control { get; } = control;

    public string Label { get; } = ControlLabels.Xbox(control);

    public IRelayCommand ChangeCommand { get; } = new RelayCommand(() => owner.BeginKeyCapture(control));

    public IRelayCommand ClearCommand { get; } = new RelayCommand(() => owner.ClearKey(control));
}

/// <summary>A controller type check box in the "Applies to" section.</summary>
public sealed partial class ControllerTypeOption(ControllerType type, Action<ControllerType, bool> changed) : ObservableObject
{
    [ObservableProperty]
    private bool _isChecked;

    public ControllerType Type { get; } = type;

    public string Label { get; } = PlayerCardViewModel.TypeName(type);

    partial void OnIsCheckedChanged(bool value) => changed(Type, value);
}

/// <summary>
/// Edits one profile. All changes go to a private draft; nothing is stored or applied until <see cref="Save"/>, except
/// that the stick preview and the Xbox output panel already use the draft. Saving applies the profile to the slots that
/// use it (hot reload).
/// </summary>
public sealed partial class ProfileEditorViewModel : ObservableObject, IDisposable
{
    public const int CurveSampleCount = 65;

    public static readonly TimeSpan BindTimeout = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan BindPollInterval = TimeSpan.FromMilliseconds(16);
    private const string ShippedPrefix = "default-";

    private static readonly ButtonId[] BindableSources =
    [
        ButtonId.South, ButtonId.East, ButtonId.West, ButtonId.North, ButtonId.Back, ButtonId.Start, ButtonId.Guide,
        ButtonId.LeftStick, ButtonId.RightStick, ButtonId.LeftShoulder, ButtonId.RightShoulder,
        ButtonId.DpadUp, ButtonId.DpadDown, ButtonId.DpadLeft, ButtonId.DpadRight, ButtonId.Touchpad,
    ];

    private static readonly KeyboardControl[] AllControls = [.. Enum.GetValues<KeyboardControl>().Where(c => c != KeyboardControl.None)];

    private readonly IProfileEditor _profiles;
    private readonly IEngine _engine;
    private readonly IRunningApps _apps;
    private readonly IDialogs _dialogs;
    private readonly IUiThread _ui;
    private readonly IClock _clock;

    private Profile _saved;
    private string _savedJson;
    private Profile _draft;
    private MappingSettings? _mapping;
    private ControllerType _labelType = ControllerType.PS4;
    private bool _loading;

    private IDisposable? _bindTimer;
    private KeyboardControl? _bindTarget;
    private DeviceId _bindDevice;
    private ButtonId _bindHeld;
    private long _bindStart;
    private KeyboardControl? _capturing;

    [ObservableProperty]
    private string _validationText = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _bindPrompt = "";

    [ObservableProperty]
    private string _capturePrompt = "";

    [ObservableProperty]
    private string _newProcessName = "";

    [ObservableProperty]
    private string? _selectedRunningApp;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevertCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _isValid = true;

    public ProfileEditorViewModel(
        Profile profile,
        IProfileEditor profiles,
        IEngine engine,
        IRunningApps apps,
        IDialogs dialogs,
        IUiThread ui,
        IClock clock)
    {
        _profiles = profiles;
        _engine = engine;
        _apps = apps;
        _dialogs = dialogs;
        _ui = ui;
        _clock = clock;

        _saved = ProfileJson.Clone(profile);
        _savedJson = ProfileJson.Serialize(_saved);
        _draft = ProfileJson.Clone(_saved);

        Live = new LivePadViewModel(engine) { PreviewMapping = () => _mapping };
        ButtonRows = [.. AllControls.Where(c => KeyboardControls.ToButton(c) != ButtonId.None).Select(c => new ButtonBindingRow(this, c))];
        KeyRows = [.. AllControls.Select(c => new EditorKeyRow(this, c))];
        ControllerTypes = [.. Enum.GetValues<ControllerType>().Select(t => new ControllerTypeOption(t, OnControllerTypeToggled))];

        LoadDraft();
        RefreshDevice();
        RefreshRunningApps();
    }

    /// <summary>Raised after the profile was written, so the list can show the new name.</summary>
    public event Action<ProfileEditorViewModel>? Saved;

    public string Id => _draft.Id;

    public bool IsShipped => Id.StartsWith(ShippedPrefix, StringComparison.Ordinal);

    public LivePadViewModel Live { get; }

    public IReadOnlyList<ButtonBindingRow> ButtonRows { get; }

    public IReadOnlyList<EditorKeyRow> KeyRows { get; }

    public IReadOnlyList<ControllerTypeOption> ControllerTypes { get; }

    public ObservableCollection<string> ProcessNames { get; } = [];

    public ObservableCollection<string> RunningApps { get; } = [];

    public IReadOnlyList<CurveType> CurveOptions { get; private set; } = [CurveType.Linear, CurveType.Quadratic, CurveType.Cubic];

    public float[] LeftCurveSamples { get; private set; } = [];

    public float[] RightCurveSamples { get; private set; } = [];

    public bool IsKeyboardProfile => _draft.AppliesTo.ControllerTypes.Contains(ControllerType.Keyboard);

    public bool IsBinding => _bindTarget is not null;

    public bool IsCapturing => _capturing is not null;

    public bool HasValidationText => ValidationText.Length > 0;

    public bool CanSave => IsValid && IsDirty;

    public bool CanRevert => IsDirty;

    /// <summary>The profile as last saved (what Export writes).</summary>
    public Profile SavedProfile => ProfileJson.Clone(_saved);

    public string Name
    {
        get => _draft.Name;
        set
        {
            if (_draft.Name != value)
            {
                _draft.Name = value ?? "";
                Edited();
            }
        }
    }

    public double LeftInner
    {
        get => _draft.Sticks.Left.DeadZone;
        set => SetStick(_draft.Sticks.Left, value, inner: true);
    }

    public double LeftOuter
    {
        get => _draft.Sticks.Left.Outer;
        set => SetStick(_draft.Sticks.Left, value, inner: false);
    }

    public double RightInner
    {
        get => _draft.Sticks.Right.DeadZone;
        set => SetStick(_draft.Sticks.Right, value, inner: true);
    }

    public double RightOuter
    {
        get => _draft.Sticks.Right.Outer;
        set => SetStick(_draft.Sticks.Right, value, inner: false);
    }

    public CurveType LeftCurve
    {
        get => _draft.Sticks.Left.Curve;
        set
        {
            if (_draft.Sticks.Left.Curve != value)
            {
                _draft.Sticks.Left.Curve = value;
                Edited();
            }
        }
    }

    public CurveType RightCurve
    {
        get => _draft.Sticks.Right.Curve;
        set
        {
            if (_draft.Sticks.Right.Curve != value)
            {
                _draft.Sticks.Right.Curve = value;
                Edited();
            }
        }
    }

    public double TriggerDeadZone
    {
        get => _draft.Triggers.DeadZone;
        set
        {
            var rounded = Round(value);
            if (_draft.Triggers.DeadZone != rounded)
            {
                _draft.Triggers.DeadZone = rounded;
                Edited();
            }
        }
    }

    public bool TriggerDigital
    {
        get => _draft.Triggers.Digital;
        set
        {
            if (_draft.Triggers.Digital != value)
            {
                _draft.Triggers.Digital = value;
                Edited();
            }
        }
    }

    public double TriggerThreshold
    {
        get => _draft.Triggers.DigitalThreshold;
        set
        {
            var rounded = Round(value);
            if (_draft.Triggers.DigitalThreshold != rounded)
            {
                _draft.Triggers.DigitalThreshold = rounded;
                Edited();
            }
        }
    }

    public double MouseSensitivity
    {
        get => _draft.Keyboard.MouseSensitivity;
        set
        {
            var rounded = Round(value);
            if (_draft.Keyboard.MouseSensitivity != rounded)
            {
                _draft.Keyboard.MouseSensitivity = rounded;
                Edited();
            }
        }
    }

    public double DecayMs
    {
        get => _draft.Keyboard.DecayMs;
        set
        {
            var rounded = (int)Math.Round(value);
            if (_draft.Keyboard.DecayMs != rounded)
            {
                _draft.Keyboard.DecayMs = rounded;
                Edited();
            }
        }
    }

    public void Dispose()
    {
        StopBindTimer();
    }

    /// <summary>Finds the controller to listen to and show: one a slot runs this profile on, else the first controller.</summary>
    public DeviceInfo? FindInputDevice()
    {
        var devices = _engine.Devices;
        foreach (var slot in _engine.Slots)
        {
            if (slot.ProfileId == Id && slot.Device is { } id && id != KeyboardPad.Id
                && devices.FirstOrDefault(d => d.Id == id) is { } info)
            {
                return info;
            }
        }

        return devices.FirstOrDefault(d => d.Type != ControllerType.Keyboard && d.Id != KeyboardPad.Id);
    }

    /// <summary>Re-reads which controller is available (for the live panels and for binding). UI thread only.</summary>
    public void RefreshDevice()
    {
        var device = FindInputDevice();
        Live.Device = device?.Id;
        var type = device?.Type
            ?? _draft.AppliesTo.ControllerTypes.FirstOrDefault(t => t != ControllerType.Keyboard && t != ControllerType.Generic);
        var labelType = type == ControllerType.Generic ? ControllerType.PS4 : type;
        if (labelType != _labelType)
        {
            _labelType = labelType;
            RefreshButtonRows();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    public void Save()
    {
        if (!CanSave)
        {
            return;
        }

        CancelBind();
        CancelKeyCapture();
        var others = _profiles.GetAll().Where(p => p.Id != Id).Select(p => p.Name);
        var name = ProfileNaming.UniqueName(_draft.Name, others);
        if (name != _draft.Name)
        {
            _draft.Name = name;
            OnPropertyChanged(nameof(Name));
        }

        try
        {
            _profiles.Save(_draft);
        }
        catch (ProfileFormatException ex)
        {
            StatusText = ex.Message;
            return;
        }

        _engine.ReloadProfile(Id);
        _saved = ProfileJson.Clone(_draft);
        _savedJson = ProfileJson.Serialize(_saved);
        RefreshDirty();
        StatusText = Strings.Format(Strings.StatusSaved, _saved.Name);
        Saved?.Invoke(this);
    }

    [RelayCommand(CanExecute = nameof(CanRevert))]
    public void Revert()
    {
        _draft = ProfileJson.Clone(_saved);
        LoadDraft();
        StatusText = Strings.StatusReverted;
    }

    /// <summary>
    /// Puts back the profile as it ships (or, for a profile of your own, the generic or keyboard defaults). Id, name and
    /// "applies to" stay. Nothing is saved until Save.
    /// </summary>
    [RelayCommand]
    public void ResetToDefault()
    {
        var shipped = _profiles.GetShipped(Id);
        var source = shipped
            ?? _profiles.GetShipped(IsKeyboardProfile ? "default-keyboard" : "default-generic")
            ?? new Profile();
        var reset = ProfileJson.Clone(source);
        reset.Id = _draft.Id;
        if (shipped is null)
        {
            reset.Name = _draft.Name;
            reset.AppliesTo = _draft.AppliesTo;
        }

        _draft = reset;
        LoadDraft();
        StatusText = Strings.StatusReset;
    }

    // ---- Applies to ---------------------------------------------------------------------------------------------

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
    private void BrowseProcess()
    {
        if (_dialogs.PickOpenFile(Strings.BrowseGameTitle, Strings.FileFilterPrograms) is { } path)
        {
            AddTarget(Path.GetFileName(path));
        }
    }

    [RelayCommand]
    private void RemoveProcess(string? name)
    {
        if (name is not null && _draft.AppliesTo.ProcessNames.RemoveAll(existing => existing == name) > 0)
        {
            ProcessNames.Remove(name);
            Edited(nameof(ProcessNames));
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

    // ---- Buttons: bind by pressing ------------------------------------------------------------------------------

    /// <summary>Waits up to <see cref="BindTimeout"/> for a button on the controller and binds it to <paramref name="target"/>.</summary>
    public void BeginBind(KeyboardControl target)
    {
        CancelKeyCapture();
        CancelBind();
        if (FindInputDevice() is not { } device)
        {
            BindPrompt = Strings.BindNoController;
            return;
        }

        Live.Device = device.Id;
        _bindDevice = device.Id;
        _bindTarget = target;
        _bindHeld = _engine.TryGetLiveState(device.Id, out var raw, out _) ? raw.Buttons : ButtonId.None;
        _bindStart = _clock.Ticks;
        BindPrompt = Strings.Format(Strings.BindPrompt, ControlLabels.Xbox(target));
        foreach (var row in ButtonRows)
        {
            row.IsBinding = row.Control == target;
        }

        OnPropertyChanged(nameof(IsBinding));
        _bindTimer = _ui.Every(BindPollInterval, PollBind);
    }

    /// <summary>Esc or the Cancel button: stops waiting for a button.</summary>
    [RelayCommand]
    public void CancelBind()
    {
        StopBindTimer();
        if (_bindTarget is null)
        {
            return;
        }

        _bindTarget = null;
        BindPrompt = "";
        foreach (var row in ButtonRows)
        {
            row.IsBinding = false;
        }

        OnPropertyChanged(nameof(IsBinding));
    }

    /// <summary>One step of the wait: times out, or binds the first button that goes down. Called by the UI timer.</summary>
    public void PollBind()
    {
        if (_bindTarget is not { } target)
        {
            return;
        }

        if (_clock.ToMs(_clock.Ticks - _bindStart) >= BindTimeout.TotalMilliseconds)
        {
            CancelBind();
            BindPrompt = Strings.BindTimedOut;
            return;
        }

        if (!_engine.TryGetLiveState(_bindDevice, out var raw, out _))
        {
            return;
        }

        // Buttons that were already down when the wait began count only after they were let go.
        _bindHeld &= raw.Buttons;
        var pressed = raw.Buttons & ~_bindHeld;
        foreach (var source in BindableSources)
        {
            if ((pressed & source) != 0)
            {
                CancelBind();
                Bind(source, ControlsToXbox(target));
                return;
            }
        }
    }

    /// <summary>
    /// Makes <paramref name="source"/> send <paramref name="target"/>. A button that sent <paramref name="target"/> before
    /// takes over what <paramref name="source"/> sent, so nothing is lost by accident.
    /// </summary>
    public void Bind(ButtonId source, XboxButton target)
    {
        var current = _mapping?.Buttons ?? DefaultMappings.Generic;
        var previousTarget = current[source];
        var holder = BindableSources.FirstOrDefault(other => other != source && current[other] == target);

        SetOverride(source, target);
        if (holder != ButtonId.None)
        {
            SetOverride(holder, previousTarget);
        }

        Edited(nameof(ButtonRows));
        BindPrompt = "";
    }

    /// <summary>Unbinds every button that sends <paramref name="control"/>.</summary>
    public void ClearBinding(KeyboardControl control)
    {
        CancelBind();
        var target = ControlsToXbox(control);
        var current = _mapping?.Buttons ?? DefaultMappings.Generic;
        foreach (var source in BindableSources.Where(source => current[source] == target))
        {
            SetOverride(source, XboxButton.None);
        }

        Edited(nameof(ButtonRows));
    }

    // ---- Keyboard: bind by pressing a key -----------------------------------------------------------------------

    public void BeginKeyCapture(KeyboardControl control)
    {
        CancelBind();
        _capturing = control;
        CapturePrompt = Strings.Format(Strings.KeyPressPrompt, ControlLabels.Xbox(control));
        foreach (var row in KeyRows)
        {
            row.IsCapturing = row.Control == control;
        }

        OnPropertyChanged(nameof(IsCapturing));
    }

    [RelayCommand]
    public void CancelKeyCapture()
    {
        if (_capturing is null)
        {
            return;
        }

        _capturing = null;
        CapturePrompt = "";
        foreach (var row in KeyRows)
        {
            row.IsCapturing = false;
        }

        OnPropertyChanged(nameof(IsCapturing));
    }

    /// <summary>
    /// Binds a key or mouse button (profile name such as "F" or "MouseLeft") to the control being captured. The input is
    /// taken from any other control and the control's old keys are dropped.
    /// </summary>
    public bool ApplyKeyCapture(string inputName)
    {
        if (_capturing is not { } control)
        {
            return false;
        }

        var keys = _draft.Keyboard.Keys;
        foreach (var existing in keys.Keys.ToList())
        {
            if (SettingsViewModel.SameInput(existing, inputName)
                || (KeyboardControls.TryParse(keys[existing], out var bound) && bound == control))
            {
                keys.Remove(existing);
            }
        }

        keys[inputName] = control.ToString();
        CancelKeyCapture();
        Edited(nameof(KeyRows));
        return true;
    }

    public void ClearKey(KeyboardControl control)
    {
        var keys = _draft.Keyboard.Keys;
        foreach (var existing in keys.Keys.ToList())
        {
            if (KeyboardControls.TryParse(keys[existing], out var bound) && bound == control)
            {
                keys.Remove(existing);
            }
        }

        Edited(nameof(KeyRows));
    }

    // ---- Internals ----------------------------------------------------------------------------------------------

    private static float Round(double value) => (float)Math.Round(value, 3);

    private static XboxButton ControlsToXbox(KeyboardControl control) => SettingsViewModel.XboxButtonOf(control);

    private void SetStick(ProfileStick stick, double value, bool inner, [CallerMemberName] string? name = null)
    {
        var rounded = Round(value);
        if (inner ? stick.DeadZone == rounded : stick.Outer == rounded)
        {
            return;
        }

        if (inner)
        {
            stick.DeadZone = rounded;
        }
        else
        {
            stick.Outer = rounded;
        }

        Edited(name);
    }

    private void SetOverride(ButtonId source, XboxButton target)
    {
        var key = source.ToString();
        if (DefaultMappings.Generic[source] == target)
        {
            _draft.Buttons.Remove(key);
        }
        else
        {
            _draft.Buttons[key] = target.ToString();
        }
    }

    private void AddTarget(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0 || ProcessNames.Any(existing => SameProcess(existing, trimmed)))
        {
            return;
        }

        ProcessNames.Add(trimmed);
        _draft.AppliesTo.ProcessNames.Add(trimmed);
        Edited(nameof(ProcessNames));
    }

    private static bool SameProcess(string a, string b) => string.Equals(StripExe(a), StripExe(b), StringComparison.OrdinalIgnoreCase);

    private static string StripExe(string name) => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;

    private void OnControllerTypeToggled(ControllerType type, bool isChecked)
    {
        if (_loading)
        {
            return;
        }

        var types = _draft.AppliesTo.ControllerTypes;
        if (isChecked && !types.Contains(type))
        {
            types.Add(type);
        }
        else if (!isChecked)
        {
            types.Remove(type);
        }

        OnPropertyChanged(nameof(IsKeyboardProfile));
        Edited(nameof(ControllerTypes));
    }

    private void Edited([CallerMemberName] string? name = null)
    {
        if (_loading)
        {
            return;
        }

        OnPropertyChanged(name);
        StatusText = "";
        Refresh();
    }

    /// <summary>The draft was replaced: tell the UI everything changed.</summary>
    private void LoadDraft()
    {
        CancelBind();
        CancelKeyCapture();
        _loading = true;
        try
        {
            foreach (var option in ControllerTypes)
            {
                option.IsChecked = _draft.AppliesTo.ControllerTypes.Contains(option.Type);
            }

            ProcessNames.Clear();
            foreach (var name in _draft.AppliesTo.ProcessNames)
            {
                ProcessNames.Add(name);
            }
        }
        finally
        {
            _loading = false;
        }

        var curves = new List<CurveType> { CurveType.Linear, CurveType.Quadratic, CurveType.Cubic };
        if (_saved.Sticks.Left.Curve == CurveType.Custom || _saved.Sticks.Right.Curve == CurveType.Custom)
        {
            curves.Add(CurveType.Custom);
        }

        CurveOptions = curves;
        OnPropertyChanged(string.Empty);
        Refresh();
    }

    private void Refresh()
    {
        try
        {
            _mapping = ProfileMapper.ToMappingSettings(_draft);
        }
        catch (ProfileFormatException)
        {
            // Keep showing the last good mapping while the draft is invalid.
        }

        LeftCurveSamples = Sample(_mapping?.LeftStick.Curve);
        RightCurveSamples = Sample(_mapping?.RightStick.Curve);
        OnPropertyChanged(nameof(LeftCurveSamples));
        OnPropertyChanged(nameof(RightCurveSamples));

        Validate();
        RefreshDirty();
        RefreshButtonRows();
        RefreshKeyRows();
    }

    private static float[] Sample(ResponseCurve? curve)
    {
        var samples = new float[CurveSampleCount];
        for (var i = 0; i < samples.Length; i++)
        {
            var t = i / (float)(samples.Length - 1);
            samples[i] = curve?.Apply(t) ?? t;
        }

        return samples;
    }

    private void Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(_draft.Name))
        {
            errors.Add(Strings.ErrNameEmpty);
        }

        var sticksOk = true;
        if (!(_draft.Sticks.Left.DeadZone < _draft.Sticks.Left.Outer))
        {
            errors.Add(Strings.ErrLeftDeadZone);
            sticksOk = false;
        }

        if (!(_draft.Sticks.Right.DeadZone < _draft.Sticks.Right.Outer))
        {
            errors.Add(Strings.ErrRightDeadZone);
            sticksOk = false;
        }

        if (sticksOk)
        {
            try
            {
                _ = ProfileMapper.ToMappingSettings(_draft);
            }
            catch (ProfileFormatException ex)
            {
                errors.Add(ex.Message);
            }
        }

        IsValid = errors.Count == 0;
        ValidationText = string.Join(Environment.NewLine, errors);
        OnPropertyChanged(nameof(HasValidationText));
    }

    private void RefreshDirty()
    {
        IsDirty = ProfileJson.Serialize(_draft) != _savedJson;
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(CanRevert));
    }

    private void RefreshButtonRows()
    {
        var buttons = _mapping?.Buttons ?? DefaultMappings.Generic;
        foreach (var row in ButtonRows)
        {
            var target = ControlsToXbox(row.Control);
            var sources = BindableSources
                .Where(source => (buttons[source] & target) != 0)
                .Select(source => ControlLabels.Physical(_labelType, source))
                .ToList();
            row.SourceText = sources.Count == 0 ? Strings.PadNotMapped : string.Join(", ", sources);
        }
    }

    private void RefreshKeyRows()
    {
        foreach (var row in KeyRows)
        {
            var keys = _draft.Keyboard.Keys
                .Where(pair => KeyboardControls.TryParse(pair.Value, out var control) && control == row.Control)
                .Select(pair => pair.Key)
                .ToList();
            row.KeyText = keys.Count == 0 ? Strings.KeyNotBound : string.Join(", ", keys);
        }
    }

    private void StopBindTimer()
    {
        _bindTimer?.Dispose();
        _bindTimer = null;
    }
}
