using Controlability.Core.Models;

namespace Controlability.Core.Abstractions;

/// <summary>
/// Implemented by input sources whose motion sensor can be switched on and off. The sensor draws battery, so the
/// engine enables it only while a profile uses it. Like rumble, it may be called from any thread.
/// </summary>
public interface ISensorControl
{
    /// <summary>No-op if the device has no gyro.</summary>
    void SetGyroEnabled(DeviceId id, bool enabled);
}
