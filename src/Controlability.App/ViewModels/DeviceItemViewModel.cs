using CommunityToolkit.Mvvm.ComponentModel;
using Controlability.App.Resources;
using Controlability.Core.Models;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

/// <summary>One row of the "Detected devices" list.</summary>
public sealed partial class DeviceItemViewModel : ObservableObject
{
    private readonly IEngine _engine;

    [ObservableProperty]
    private string _assignmentText = "";

    [ObservableProperty]
    private string _detailText = "";

    /// <summary>Third line of the row: how the device is connected, or "Not assigned".</summary>
    [ObservableProperty]
    private string _statusLine = "";

    [ObservableProperty]
    private bool _isAssigned;

    /// <summary>"Assigned to P2", shown as a badge while the device is on a player.</summary>
    [ObservableProperty]
    private string _assignedBadge = "";

    [ObservableProperty]
    private bool _hasBatteryLevel;

    [ObservableProperty]
    private string _batteryLevelText = "";

    [ObservableProperty]
    private double _batteryFillWidth;

    public DeviceItemViewModel(DeviceInfo info, IEngine engine)
    {
        _engine = engine;
        Id = info.Id;
        Name = info.Name;
        Type = info.Type;
        Maker = MakerName(info);
        AssignOptions =
        [
            .. Enumerable.Range(0, 4).Select(slot => new MenuOption(
                Strings.Format(Strings.AssignToPlayer, slot + 1),
                () => _engine.Assign(Id, slot))),
        ];
        Update(info, null);
    }

    public DeviceId Id { get; }

    public string Name { get; }

    public ControllerType Type { get; }

    /// <summary>The second line of the row: the maker, or the USB ids when the vendor is not known.</summary>
    public string Maker { get; }

    public List<MenuOption> AssignOptions { get; }

    /// <summary>Text for screen readers.</summary>
    public string AutomationName => $"{Name}, {DetailText}, {AssignmentText}";

    public void Update(DeviceInfo info, int? slot, BatteryInfo? battery = null)
    {
        var parts = new[] { PlayerCardViewModel.TypeName(info.Type), PlayerCardViewModel.ConnectionName(info) };
        DetailText = string.Join(" - ", parts.Where(part => part.Length > 0));
        AssignmentText = slot is { } s ? Strings.Format(Strings.DeviceAssigned, s + 1) : Strings.DeviceUnassigned;
        IsAssigned = slot is not null;
        AssignedBadge = slot is { } p ? $"Assigned to P{p + 1}" : "";

        var level = PlayerCardViewModel.BatteryLevel(battery, info.Type);
        HasBatteryLevel = level is not null;
        BatteryLevelText = level is { } percent ? Strings.Format(Strings.BatteryPercent, percent) : "";
        BatteryFillWidth = (level ?? 0) / 100.0 * 16;

        StatusLine = !IsAssigned ? Strings.DeviceUnassigned
            : info.Type == ControllerType.Keyboard ? "Keyboard and mouse input"
            : PlayerCardViewModel.ConnectionName(info);
        OnPropertyChanged(nameof(AutomationName));
    }

    private static string MakerName(DeviceInfo info) => info.Vid switch
    {
        0x054C => "Sony Interactive Entertainment",
        0x045E => "Microsoft",
        0x057E => "Nintendo",
        0x28DE => "Valve",
        0x2DC8 => "8BitDo",
        _ when info.Type == ControllerType.Keyboard => "Keyboard",
        _ => $"Vendor: {info.Vid:x4} Product: {info.Pid:x4}",
    };
}
