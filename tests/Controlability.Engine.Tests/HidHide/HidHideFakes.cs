using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Platform.HidHide;

namespace Controlability.Engine.Tests.HidHide;

/// <summary>In-memory <see cref="IHidHideCli"/>; every mutating call is appended to <see cref="Calls"/>.</summary>
public sealed class FakeHidHideCli : IHidHideCli
{
    public string ExePath => @"C:\fake\HidHideCLI.exe";

    public bool Cloak { get; set; }

    public List<string> Apps { get; } = [];

    public List<string> Hidden { get; } = [];

    public List<HidDevice> Gaming { get; } = [];

    public List<string> Calls { get; } = [];

    public bool FailHide { get; set; }

    public bool FailUnhide { get; set; }

    public string Version() => "1.5.230.0";

    public bool IsCloakOn() => Cloak;

    public void SetCloak(bool on)
    {
        Calls.Add(on ? "cloak-on" : "cloak-off");
        Cloak = on;
    }

    public bool IsInverseOn() => false;

    public IReadOnlyList<string> GetApps() => [.. Apps];

    public void RegisterApp(string exePath)
    {
        Calls.Add($"app-reg {exePath}");
        Apps.Add(exePath);
    }

    public IReadOnlyList<string> GetHiddenDevices() => [.. Hidden];

    public void Hide(string instancePath)
    {
        Calls.Add($"hide {instancePath}");
        if (FailHide)
        {
            throw new HidHideCliException("hide failed");
        }

        Hidden.Add(instancePath);
    }

    public void Unhide(string instancePath)
    {
        Calls.Add($"unhide {instancePath}");
        if (FailUnhide)
        {
            throw new HidHideCliException("unhide failed");
        }

        Hidden.RemoveAll(p => string.Equals(p, instancePath, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<HidDevice> GetGamingDevices() => [.. Gaming];
}

/// <summary>Records hide/unhide into a shared list so tests can check the order against other fakes.</summary>
public sealed class FakeDeviceHider(List<string> events) : IDeviceHider
{
    public bool IsAvailable { get; set; } = true;

    public int WhitelistCalls { get; private set; }

    public void Hide(DeviceInfo device) => events.Add($"hide {device.Id}");

    public void Unhide(DeviceInfo device) => events.Add($"unhide {device.Id}");

    public void UnhideAll() => events.Add("unhide-all");

    public void WhitelistSelf() => WhitelistCalls++;
}
