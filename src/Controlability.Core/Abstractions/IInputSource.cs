using Controlability.Core.Models;

namespace Controlability.Core.Abstractions;

public interface IInputSource : IDisposable
{
    event Action<DeviceInfo>? DeviceAdded;

    event Action<DeviceId>? DeviceRemoved;

    /// <summary>Called by the engine thread.</summary>
    void Poll();

    bool TryGetState(DeviceId id, out InputState state);

    void SetRumble(DeviceId id, byte low, byte high, int durationMs);

    /// <summary>No-op if the device has no LED.</summary>
    void SetLed(DeviceId id, byte r, byte g, byte b);
}
