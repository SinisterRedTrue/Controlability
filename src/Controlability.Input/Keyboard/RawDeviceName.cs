using System.Text.RegularExpressions;
using Controlability.Core.Models;

namespace Controlability.Input.Keyboard;

/// <summary>Reads the Raw Input device name (<c>\\?\HID#VID_046D&amp;PID_C31C#...</c>) into a <see cref="DeviceInfo"/>.</summary>
public static partial class RawDeviceName
{
    public static DeviceInfo ToDeviceInfo(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        ushort vid = 0, pid = 0;
        var match = VidPid().Match(name);
        if (match.Success)
        {
            vid = ushort.Parse(match.Groups["vid"].Value, System.Globalization.NumberStyles.HexNumber);
            pid = ushort.Parse(match.Groups["pid"].Value, System.Globalization.NumberStyles.HexNumber);
        }

        var label = match.Success ? $"Keyboard {vid:X4}:{pid:X4}" : "Keyboard";
        return new DeviceInfo(
            new DeviceId(name), label, vid, pid, null, name, ConnectionType.Unknown, ControllerType.Keyboard, DeviceCapabilities.None);
    }

    /// <summary>
    /// True for the pointing device of a game controller (the DS4/DualSense touchpad shows up in Windows as a mouse).
    /// Its touches and clicks must not drive the keyboard pad. Sony (054C) and Nintendo (057E) only; no mouse maker uses them.
    /// </summary>
    public static bool IsControllerPointer(string name)
        => name.Contains("VID_054C", StringComparison.OrdinalIgnoreCase)
        || name.Contains("VID_057E", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"VID_(?<vid>[0-9A-Fa-f]{4})&PID_(?<pid>[0-9A-Fa-f]{4})")]
    private static partial Regex VidPid();
}
