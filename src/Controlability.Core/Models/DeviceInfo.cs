namespace Controlability.Core.Models;

public enum ConnectionType
{
    Unknown,
    Usb,
    Bluetooth,
}

public enum ControllerType
{
    Generic,
    PS4,
    PS5,
    SwitchPro,
    Xbox,
    Keyboard,
}

[Flags]
public enum DeviceCapabilities
{
    None = 0,
    Rumble = 1 << 0,
    Led = 1 << 1,
    Gyro = 1 << 2,
    Touchpad = 1 << 3,
    Battery = 1 << 4,
}

/// <summary>Description of a connected input device.</summary>
public sealed record DeviceInfo(
    DeviceId Id,
    string Name,
    ushort Vid,
    ushort Pid,
    string? Serial,
    string? HidPath,
    ConnectionType Connection,
    ControllerType Type,
    DeviceCapabilities Capabilities);
