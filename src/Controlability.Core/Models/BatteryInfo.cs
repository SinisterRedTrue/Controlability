namespace Controlability.Core.Models;

public enum BatteryState
{
    Unknown,
    Wired,
    Charging,
    Discharging,
    Full,
}

public readonly record struct BatteryInfo(BatteryState State, byte Percent);
