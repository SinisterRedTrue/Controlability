using Controlability.Core.Models;

namespace Controlability.Core.Abstractions;

public interface IDeviceHider
{
    /// <summary>False when HidHide is not installed.</summary>
    bool IsAvailable { get; }

    void Hide(DeviceInfo device);

    void Unhide(DeviceInfo device);

    void WhitelistSelf();
}
