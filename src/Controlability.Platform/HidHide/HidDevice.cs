using System.Text.Json;
using System.Text.RegularExpressions;

namespace Controlability.Platform.HidHide;

/// <summary>One entry of <c>HidHideCLI --dev-gaming</c> / <c>--dev-all</c>.</summary>
/// <param name="SymbolicLink">The device interface path, the same string SDL reports as the HID path (case may differ).</param>
/// <param name="InstancePath">What <c>--dev-hide</c> expects, e.g. <c>HID\VID_054C&amp;PID_09CC&amp;MI_03\9&amp;343547ec&amp;0&amp;0000</c>.</param>
public sealed record HidDevice(
    string SymbolicLink,
    string InstancePath,
    string? ContainerInstancePath,
    string? Serial,
    bool Present,
    bool Gaming)
{
    private static readonly Regex UsbIds = new(@"VID_(?<vid>[0-9A-F]{4})&PID_(?<pid>[0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Bluetooth HID: VID&0002054C_PID&09CC, where 0002 is the vendor id source (USB-IF).
    private static readonly Regex BluetoothIds = new(@"VID&[0-9A-F]{4}(?<vid>[0-9A-F]{4})_PID&(?<pid>[0-9A-F]{4})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public bool TryGetVidPid(out ushort vid, out ushort pid)
    {
        var match = UsbIds.Match(InstancePath);
        if (!match.Success)
        {
            match = BluetoothIds.Match(InstancePath);
        }

        if (match.Success)
        {
            vid = Convert.ToUInt16(match.Groups["vid"].Value, 16);
            pid = Convert.ToUInt16(match.Groups["pid"].Value, 16);
            return true;
        }

        vid = 0;
        pid = 0;
        return false;
    }

    /// <summary>Parses the JSON that <c>--dev-gaming</c> prints: a list of groups, each with a <c>devices</c> list.</summary>
    /// <exception cref="JsonException">The text is not JSON of that shape.</exception>
    public static IReadOnlyList<HidDevice> ParseList(string json)
    {
        var devices = new List<HidDevice>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return devices;
        }

        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("Expected a JSON array of device groups.");
        }

        foreach (var group in document.RootElement.EnumerateArray())
        {
            if (!group.TryGetProperty("devices", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var item in list.EnumerateArray())
            {
                var instancePath = GetString(item, "deviceInstancePath");
                if (string.IsNullOrEmpty(instancePath))
                {
                    continue;
                }

                var serial = GetString(item, "serialNumber");
                devices.Add(new HidDevice(
                    GetString(item, "symbolicLink") ?? string.Empty,
                    instancePath,
                    GetString(item, "baseContainerDeviceInstancePath"),
                    string.IsNullOrWhiteSpace(serial) ? null : serial,
                    GetBool(item, "present"),
                    GetBool(item, "gamingDevice")));
            }
        }

        return devices;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
