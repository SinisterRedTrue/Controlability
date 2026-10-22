using System.Collections.ObjectModel;
using System.Globalization;
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

/// <summary>One device on the Devices page, with its details and the buttons that act on it.</summary>
public sealed partial class DeviceDetailViewModel : ObservableObject
{
    public static readonly IReadOnlyList<string> LedSwatches =
        ["#FF0000", "#FF8800", "#FFFF00", "#00FF00", "#00FFFF", "#0000FF", "#8800FF", "#FF69B4", "#FFFFFF"];

    private readonly IEngine _engine;
    private bool _updating;

    [ObservableProperty]
    private string _assignmentText = "";

    [ObservableProperty]
    private bool _isHidden;

    [ObservableProperty]
    private bool _canHide;

    [ObservableProperty]
    private string _ledText = "#0000FF";

    [ObservableProperty]
    private string _ledError = "";

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

    /// <summary>Type and connection, for the list row.</summary>
    public string SummaryText => string.Join(" - ", new[] { TypeText, ConnectionText }.Where(part => part.Length > 0));

    public string AutomationName => $"{Name}, {SummaryText}, {AssignmentText}";

    public void Update(int? slot, bool hidden, bool hidHideAvailable)
    {
        AssignmentText = slot is { } s ? Strings.Format(Strings.DeviceAssigned, s + 1) : Strings.DeviceUnassigned;
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
    private readonly IEngine _engine;
    private readonly IProfileEditor _profiles;
    private readonly ThrottledAction _throttle;
    private bool _disposed;

    [ObservableProperty]
    private DeviceDetailViewModel? _selected;

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
    }

    partial void OnSelectedChanged(DeviceDetailViewModel? value)
    {
        Live.Device = value?.Id;
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
            device.Update(slot?.Index, hidden.Contains(device.Id), drivers.HidHideAvailable);
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
