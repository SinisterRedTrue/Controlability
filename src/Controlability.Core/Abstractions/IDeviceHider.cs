using Controlability.Core.Models;

namespace Controlability.Core.Abstractions;

public interface IDeviceHider
{
    /// <summary>False when HidHide is not installed.</summary>
    bool IsAvailable { get; }

    /// <summary>Hides the device from every app except the whitelisted ones. Never throws; failures are logged.</summary>
    void Hide(DeviceInfo device);

    /// <summary>Unhides a device this hider hid. Devices it did not hide are left alone.</summary>
    void Unhide(DeviceInfo device);

    /// <summary>Unhides everything this hider hid (also what an earlier, crashed run left behind).</summary>
    void UnhideAll();

    /// <summary>Lets the current executable keep seeing hidden devices.</summary>
    void WhitelistSelf();
}
