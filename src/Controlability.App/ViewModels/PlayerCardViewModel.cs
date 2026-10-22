using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Models;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

/// <summary>One of the four player cards on the dashboard.</summary>
public sealed partial class PlayerCardViewModel : ObservableObject
{
    private static readonly string[] SlotColors = ["#3B82F6", "#EF4444", "#22C55E", "#EC4899"];

    private const string ActiveColor = "#22C55E";
    private const string WaitingColor = "#EAB308";
    private const string EmptyColor = "#6B7280";

    private readonly IEngine _engine;

    [ObservableProperty]
    private string _deviceName = "";

    [ObservableProperty]
    private ControllerType _deviceType = ControllerType.Generic;

    [ObservableProperty]
    private string _typeText = "";

    [ObservableProperty]
    private string _connectionText = "";

    /// <summary>Type and connection, joined; a keyboard has no connection to show, so no dangling separator.</summary>
    [ObservableProperty]
    private string _detailText = "";

    [ObservableProperty]
    private string _batteryText = "";

    [ObservableProperty]
    private string _profileText = "";

    [ObservableProperty]
    private string _xInputText = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _statusColor = EmptyColor;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOccupied))]
    private bool _isEmpty = true;

    [ObservableProperty]
    private string _automationName = "";

    public PlayerCardViewModel(int index, IEngine engine)
    {
        _engine = engine;
        Index = index;
        Title = Strings.Format(Strings.PlayerTitle, index + 1);
        SlotColor = SlotColors[index];
        UnassignCommand = new RelayCommand(() => _engine.Unassign(Index), () => !IsEmpty);
        IdentifyCommand = new RelayCommand(() => _engine.Identify(Index), () => !IsEmpty);
        Update(new SlotStatus(index, null, null, null, SlotState.Empty, null), null, null, []);
    }

    public bool IsOccupied => !IsEmpty;

    public int Index { get; }

    public string Title { get; }

    /// <summary>The slot color as <c>#RRGGBB</c>.</summary>
    public string SlotColor { get; }

    public DeviceId? Device { get; private set; }

    public IRelayCommand UnassignCommand { get; }

    public IRelayCommand IdentifyCommand { get; }

    /// <summary>The "Change profile" sub-menu; the current profile is checked.</summary>
    public List<MenuOption> ProfileOptions { get; private set; } = [];

    public bool HasProfileOptions => ProfileOptions.Count > 0;

    public void Update(SlotStatus slot, DeviceInfo? info, BatteryInfo? battery, IReadOnlyList<ProfileChoice> profiles)
    {
        Device = slot.Device;
        IsEmpty = slot.Device is null;
        DeviceName = slot.DeviceName ?? "";
        DeviceType = info?.Type ?? ControllerType.Generic;
        TypeText = IsEmpty ? "" : TypeName(DeviceType);
        ConnectionText = IsEmpty ? "" : ConnectionName(info);
        DetailText = string.Join("  |  ", new[] { TypeText, ConnectionText }.Where(part => part.Length > 0));
        BatteryText = IsEmpty ? "" : BatteryName(battery, DeviceType);
        XInputText = slot.XInputIndex is { } xi ? Strings.Format(Strings.XInputLabel, xi + 1) : Strings.XInputUnknown;

        var profile = profiles.FirstOrDefault(choice => choice.Id == slot.ProfileId);
        ProfileText = Strings.Format(Strings.ProfileLabel, profile?.Name ?? slot.ProfileId ?? Strings.ProfileNone);
        ProfileOptions = [.. profiles.Select(choice => new MenuOption(choice.Name, () => _engine.SetProfile(Index, choice.Id), choice.Id == slot.ProfileId))];
        OnPropertyChanged(nameof(ProfileOptions));
        OnPropertyChanged(nameof(HasProfileOptions));

        (StatusText, StatusColor) = slot.State switch
        {
            SlotState.Active => (Strings.SlotActiveStatus, ActiveColor),
            SlotState.Waiting => (Strings.SlotWaitingStatus, WaitingColor),
            SlotState.Paused => (Strings.SlotPausedStatus, WaitingColor),
            _ => (Strings.SlotEmptyStatus, EmptyColor),
        };

        AutomationName = IsEmpty ? $"{Title}, {Strings.SlotEmptyStatus}" : $"{Title}, {DeviceName}, {StatusText}";
        UnassignCommand.NotifyCanExecuteChanged();
        IdentifyCommand.NotifyCanExecuteChanged();
    }

    internal static string TypeName(ControllerType type) => type switch
    {
        ControllerType.PS4 => Strings.TypePS4,
        ControllerType.PS5 => Strings.TypePS5,
        ControllerType.SwitchPro => Strings.TypeSwitchPro,
        ControllerType.Xbox => Strings.TypeXbox,
        ControllerType.Keyboard => Strings.TypeKeyboard,
        _ => Strings.TypeGeneric,
    };

    internal static string ConnectionName(DeviceInfo? info) => info?.Connection switch
    {
        ConnectionType.Usb => Strings.ConnectionUsb,
        ConnectionType.Bluetooth => Strings.ConnectionBluetooth,
        _ => Strings.ConnectionUnknown,
    };

    private static string BatteryName(BatteryInfo? battery, ControllerType type)
    {
        if (type == ControllerType.Keyboard || battery is not { } info)
        {
            return "";
        }

        return info.State switch
        {
            BatteryState.Wired => Strings.BatteryWired,
            BatteryState.Charging => Strings.Format(Strings.BatteryCharging, info.Percent),
            BatteryState.Discharging or BatteryState.Full => Strings.Format(Strings.BatteryPercent, info.Percent),
            _ => "",
        };
    }
}
