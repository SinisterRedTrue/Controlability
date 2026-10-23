using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Abstractions;
using Controlability.Core.Mapping;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

/// <summary>One colour choice of the light bar, with whether it is the colour currently shown.</summary>
public sealed partial class LedSwatchViewModel(string hex) : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected;

    public string Hex { get; } = hex;

    public Brush Brush { get; } = DeviceDetailViewModel.ParseBrush(hex) ?? Brushes.Transparent;
}

/// <summary>One device on the Devices page, with its details and the buttons that act on it.</summary>
public sealed partial class DeviceDetailViewModel : ObservableObject
{
    public static readonly IReadOnlyList<string> LedSwatches =
        ["#39BDF8", "#9B5CF6", "#E044D8", "#EF4444", "#FF9F2E", "#FFD60A", "#4ADE50", "#FFFFFF"];

    private readonly IEngine _engine;
    private bool _updating;

    [ObservableProperty]
    private string _assignmentText = "";

    [ObservableProperty]
    private bool _isHidden;

    [ObservableProperty]
    private bool _canHide;

    [ObservableProperty]
    private string _ledText = "#39BDF8";

    [ObservableProperty]
    private string _ledError = "";

    /// <summary>The colour in <see cref="LedText"/>, for the preview square of the colour box.</summary>
    [ObservableProperty]
    private Brush _ledPreview = Brushes.Transparent;

    /// <summary>"P2", the badge of the list row while the device is on a player.</summary>
    [ObservableProperty]
    private string _playerBadge = "";

    /// <summary>"Player 2", the badge of the header while the device is on a player.</summary>
    [ObservableProperty]
    private string _playerLabel = "";

    [ObservableProperty]
    private string _assignmentValue = "";

    [ObservableProperty]
    private bool _hasBatteryLevel;

    [ObservableProperty]
    private string _batteryLevelText = "";

    /// <summary>Width in device-independent pixels of the filled part of the battery glyph (0 to 16).</summary>
    [ObservableProperty]
    private double _batteryFillWidth;

    public DeviceDetailViewModel(DeviceInfo info, IEngine engine)
    {
        _engine = engine;
        Info = info;
        Id = info.Id;
        Name = info.Name;
        TypeText = PlayerCardViewModel.TypeName(info.Type);
        ConnectionText = info.Type == ControllerType.Keyboard ? "" : PlayerCardViewModel.ConnectionName(info);
        VidPidText = $"{info.Vid:X4}:{info.Pid:X4}";
        SerialText = string.IsNullOrEmpty(info.Serial) ? "-" : info.Serial;
        HidPathText = string.IsNullOrEmpty(info.HidPath) ? "-" : info.HidPath;
        CapabilitiesText = DescribeCapabilities(info.Capabilities);
        HasRumble = info.Capabilities.HasFlag(DeviceCapabilities.Rumble);
        HasLed = info.Capabilities.HasFlag(DeviceCapabilities.Led);
        IsKeyboard = info.Type == ControllerType.Keyboard;
        IsPlayStation = info.Type is ControllerType.PS4 or ControllerType.PS5;
        TypeDescription = Describe(info.Type);
        CapabilitiesDisplay = CapabilitiesText.Replace(", ", " / ", StringComparison.Ordinal);
        HeaderSubtitle = IsKeyboard ? "Keyboard and mouse input" : $"Connected via {ConnectionText}";
        Swatches = [.. LedSwatches.Select(hex => new LedSwatchViewModel(hex))];
        OnLedTextChanged(LedText);
    }

    public DeviceInfo Info { get; }

    public DeviceId Id { get; }

    public string Name { get; }

    public string TypeText { get; }

    public string ConnectionText { get; }

    public string VidPidText { get; }

    public string SerialText { get; }

    public string HidPathText { get; }

    public string CapabilitiesText { get; }

    public bool HasRumble { get; }

    public bool HasLed { get; }

    public bool IsKeyboard { get; }

    /// <summary>A PlayStation pad: the live test draws it with its own layout.</summary>
    public bool IsPlayStation { get; }

    /// <summary>The long name of the device type, for the information table.</summary>
    public string TypeDescription { get; }

    /// <summary>The capabilities separated by slashes.</summary>
    public string CapabilitiesDisplay { get; }

    public string HeaderSubtitle { get; }

    public bool HasConnection => ConnectionText.Length > 0;

    public IReadOnlyList<LedSwatchViewModel> Swatches { get; }

    /// <summary>The second line of the list row: the assignment for a keyboard, else how the pad is connected.</summary>
    public string ListStatusText => IsKeyboard
        ? (PlayerBadge.Length > 0 ? $"Assigned to {PlayerLabel}" : AssignmentText)
        : string.Join(" • ", new[] { ConnectionText, "Connected" }.Where(part => part.Length > 0));

    public bool HasPlayer => PlayerBadge.Length > 0;

    public IReadOnlyList<MenuOption> AssignOptions =>
        Enumerable.Range(0, 4).Select(slot => new MenuOption(
            Strings.Format(Strings.AssignToPlayer, slot + 1), () => _engine.Assign(Id, slot))).ToArray();

    /// <summary>Type and connection, for the list row.</summary>
    public string SummaryText => string.Join(" - ", new[] { TypeText, ConnectionText }.Where(part => part.Length > 0));

    public string AutomationName => $"{Name}, {SummaryText}, {AssignmentText}";

    public void Update(int? slot, bool hidden, bool hidHideAvailable, BatteryInfo? battery = null)
    {
        AssignmentText = slot is { } s ? Strings.Format(Strings.DeviceAssigned, s + 1) : Strings.DeviceUnassigned;
        AssignmentValue = slot is { } p ? $"Player {p + 1}" : Strings.DeviceUnassigned;
        PlayerBadge = slot is { } n ? $"P{n + 1}" : "";
        PlayerLabel = slot is { } q ? $"Player {q + 1}" : "";
        var level = PlayerCardViewModel.BatteryLevel(battery, Info.Type);
        HasBatteryLevel = level is not null;
        BatteryLevelText = level is { } percent ? $"Battery {Strings.Format(Strings.BatteryPercent, percent)}" : "";
        BatteryFillWidth = (level ?? 0) / 100.0 * 16;
        OnPropertyChanged(nameof(ListStatusText));
        OnPropertyChanged(nameof(HasPlayer));
        CanHide = hidHideAvailable && Info.Type != ControllerType.Keyboard;
        _updating = true;
        IsHidden = hidden;
        _updating = false;
        OnPropertyChanged(nameof(AutomationName));
    }

    partial void OnIsHiddenChanged(bool value)
    {
        if (!_updating)
        {
            _engine.SetDeviceHidden(Id, value);
        }
    }

    partial void OnLedTextChanged(string value)
    {
        LedPreview = ParseBrush(value) ?? Brushes.Transparent;
        foreach (var swatch in Swatches ?? [])
        {
            swatch.IsSelected = string.Equals(swatch.Hex, value.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand(CanExecute = nameof(HasRumble))]
    private void RumbleTest() => _engine.TestRumble(Id);

    /// <summary>Sets the light bar to <paramref name="hex"/> ("#RRGGBB"), which also fills the text box.</summary>
    [RelayCommand(CanExecute = nameof(HasLed))]
    private void SetLed(string? hex)
    {
        hex ??= LedText;
        if (!TryParseColor(hex, out var r, out var g, out var b))
        {
            LedError = Strings.LedInvalid;
            return;
        }

        LedError = "";
        LedText = hex.Trim().ToUpperInvariant();
        _engine.SetLedColor(Id, r, g, b);
    }

    internal static Brush? ParseBrush(string text)
    {
        if (!TryParseColor(text, out var r, out var g, out var b))
        {
            return null;
        }

        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static string Describe(ControllerType type) => type switch
    {
        ControllerType.PS4 => "PlayStation 4 Controller (DUALSHOCK 4)",
        ControllerType.PS5 => "PlayStation 5 Controller (DualSense)",
        ControllerType.SwitchPro => "Nintendo Switch Pro Controller",
        ControllerType.Xbox => "Xbox Controller",
        ControllerType.Keyboard => "Keyboard and mouse",
        _ => "Generic game controller",
    };

    internal static bool TryParseColor(string text, out byte r, out byte g, out byte b)
    {
        r = g = b = 0;
        var value = (text ?? "").Trim();
        if (value.Length != 7 || value[0] != '#'
            || !int.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            return false;
        }

        r = (byte)(rgb >> 16);
        g = (byte)(rgb >> 8);
        b = (byte)rgb;
        return true;
    }

    private static string DescribeCapabilities(DeviceCapabilities capabilities)
    {
        var parts = new List<string>();

        void Add(DeviceCapabilities flag, string text)
        {
            if (capabilities.HasFlag(flag))
            {
                parts.Add(text);
            }
        }

        Add(DeviceCapabilities.Rumble, Strings.CapRumble);
        Add(DeviceCapabilities.Led, Strings.CapLed);
        Add(DeviceCapabilities.Gyro, Strings.CapGyro);
        Add(DeviceCapabilities.Touchpad, Strings.CapTouchpad);
        Add(DeviceCapabilities.Battery, Strings.CapBattery);
        return parts.Count == 0 ? Strings.CapNone : string.Join(", ", parts);
    }
}

/// <summary>The devices page: every device the engine sees, with details, actions and the live test panels.</summary>
public sealed partial class DevicesViewModel : ObservableObject, IDisposable
{
    /// <summary>Battery levels do not raise <see cref="IEngine.Changed"/>, so they are polled slowly.</summary>
    private static readonly TimeSpan BatteryPollInterval = TimeSpan.FromSeconds(5);

    private readonly IEngine _engine;
    private readonly IProfileEditor _profiles;
    private readonly ThrottledAction _throttle;
    private readonly IDisposable _batteryTimer;
    private bool _disposed;

    [ObservableProperty]
    private DeviceDetailViewModel? _selected;

    /// <summary>The device name in brackets, beside the title of the physical-input panel.</summary>
    [ObservableProperty]
    private string _rawTitle = "";

    /// <summary>Whether the physical-input panel draws a PlayStation pad rather than the generic one.</summary>
    [ObservableProperty]
    private bool _rawIsPlayStation;

    [ObservableProperty]
    private double _leftInner = DeadZone.Default.Inner;

    [ObservableProperty]
    private double _leftOuter = DeadZone.Default.Outer;

    [ObservableProperty]
    private double _rightInner = DeadZone.Default.Inner;

    [ObservableProperty]
    private double _rightOuter = DeadZone.Default.Outer;

    [ObservableProperty]
    private bool _hidHideMissing;

    public DevicesViewModel(IEngine engine, IProfileEditor profiles, IUiThread ui, IClock clock)
    {
        _engine = engine;
        _profiles = profiles;
        Live = new LivePadViewModel(engine);
        _throttle = new ThrottledAction(ui, clock, DashboardViewModel.RefreshInterval, Refresh);
        Refresh();
        _engine.Changed += _throttle.Request;
        _batteryTimer = ui.Every(BatteryPollInterval, Refresh);
    }

    public ObservableCollection<DeviceDetailViewModel> Devices { get; } = [];

    public LivePadViewModel Live { get; }

    public bool HasDevices => Devices.Count > 0;

    public bool HasNoDevices => Devices.Count == 0;

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

    partial void OnSelectedChanged(DeviceDetailViewModel? value)
    {
        Live.Device = value?.Id;
        RawTitle = value is null ? "" : $"({value.Name})";
        RawIsPlayStation = value?.IsPlayStation == true;
        RefreshDeadZones();
    }

    /// <summary>Re-reads the engine into the list. UI thread only.</summary>
    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var devices = _engine.Devices;
        var drivers = _engine.DriverStatus;
        HidHideMissing = !drivers.HidHideAvailable;

        if (!Devices.Select(d => d.Id).SequenceEqual(devices.Select(d => d.Id)))
        {
            var selectedId = Selected?.Id;
            var existing = Devices.ToDictionary(d => d.Id);
            Devices.Clear();
            foreach (var info in devices)
            {
                Devices.Add(existing.GetValueOrDefault(info.Id) ?? new DeviceDetailViewModel(info, _engine));
            }

            Selected = Devices.FirstOrDefault(d => d.Id == selectedId) ?? Devices.FirstOrDefault();
            OnPropertyChanged(nameof(HasDevices));
            OnPropertyChanged(nameof(HasNoDevices));
        }

        var hidden = _engine.HiddenDevices;
        var slots = _engine.Slots;
        foreach (var device in Devices)
        {
            var slot = slots.FirstOrDefault(s => s.Device == device.Id);
            BatteryInfo? battery = _engine.TryGetLiveState(device.Id, out var raw, out _) ? raw.Battery : null;
            device.Update(slot?.Index, hidden.Contains(device.Id), drivers.HidHideAvailable, battery);
        }

        RefreshDeadZones();
    }

    /// <summary>Shows the dead zones of the profile the selected device runs on, or the defaults.</summary>
    private void RefreshDeadZones()
    {
        var settings = MappingSettings.Default;
        if (Selected is { } device
            && _engine.Slots.FirstOrDefault(s => s.Device == device.Id)?.ProfileId is { } profileId
            && _profiles.Get(profileId) is { } profile)
        {
            try
            {
                settings = ProfileMapper.ToMappingSettings(profile);
            }
            catch (ProfileFormatException)
            {
                // Fall back to the default dead zones.
            }
        }

        LeftInner = settings.LeftStick.DeadZone.Inner;
        LeftOuter = settings.LeftStick.DeadZone.Outer;
        RightInner = settings.RightStick.DeadZone.Inner;
        RightOuter = settings.RightStick.DeadZone.Outer;
    }
}
