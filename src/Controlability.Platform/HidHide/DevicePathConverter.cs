using Controlability.Core.Models;

namespace Controlability.Platform.HidHide;

/// <summary>
/// SDL reports a device interface path (<c>\\?\HID#VID_054C&amp;PID_09CC&amp;MI_03#9&amp;343547ec&amp;0&amp;0000#{4d1e55b2-...}</c>),
/// HidHide wants the device instance path (<c>HID\VID_054C&amp;PID_09CC&amp;MI_03\9&amp;343547ec&amp;0&amp;0000</c>).
/// </summary>
public static class DevicePathConverter
{
    /// <summary>Converts an interface path to an instance path. Returns null if the text is not an interface path.</summary>
    public static string? ToInstancePath(string? hidPath)
    {
        if (string.IsNullOrWhiteSpace(hidPath))
        {
            return null;
        }

        var path = hidPath.Trim();
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            path = path[4..];
        }

        // The device interface class GUID is the last #-separated part.
        var guidStart = path.LastIndexOf("#{", StringComparison.Ordinal);
        if (guidStart >= 0 && path.EndsWith('}'))
        {
            path = path[..guidStart];
        }

        var parts = path.Split('#');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
        {
            return null;
        }

        return string.Join('\\', parts);
    }

    /// <summary>
    /// Finds the entry of <c>--dev-gaming</c> that belongs to <paramref name="device"/>: first by interface path, then by the
    /// converted instance path, then by VID/PID (narrowed by serial). Returns null if nothing matches or the match is ambiguous.
    /// </summary>
    public static HidDevice? Resolve(DeviceInfo device, IReadOnlyList<HidDevice> candidates)
    {
        if (!string.IsNullOrEmpty(device.HidPath))
        {
            var bySymbolicLink = candidates.FirstOrDefault(c => string.Equals(c.SymbolicLink, device.HidPath, StringComparison.OrdinalIgnoreCase));
            if (bySymbolicLink is not null)
            {
                return bySymbolicLink;
            }

            if (ToInstancePath(device.HidPath) is { } instancePath)
            {
                var byInstancePath = candidates.FirstOrDefault(c => string.Equals(c.InstancePath, instancePath, StringComparison.OrdinalIgnoreCase));
                if (byInstancePath is not null)
                {
                    return byInstancePath;
                }
            }
        }

        var sameModel = candidates
            .Where(c => c.Present && c.TryGetVidPid(out var vid, out var pid) && vid == device.Vid && pid == device.Pid)
            .ToList();
        if (sameModel.Count > 1 && !string.IsNullOrEmpty(device.Serial))
        {
            var bySerial = sameModel
                .Where(c => string.Equals(c.Serial, device.Serial, StringComparison.OrdinalIgnoreCase)
                    || c.InstancePath.Contains(device.Serial, StringComparison.OrdinalIgnoreCase)
                    || (c.ContainerInstancePath?.Contains(device.Serial, StringComparison.OrdinalIgnoreCase) ?? false))
                .ToList();
            if (bySerial.Count > 0)
            {
                sameModel = bySerial;
            }
        }

        // Two identical pads and nothing to tell them apart: hiding the wrong one is worse than hiding none.
        return sameModel.Count == 1 ? sameModel[0] : null;
    }
}
