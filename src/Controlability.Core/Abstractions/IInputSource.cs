using Controlability.Core.Models;

namespace Controlability.Core.Abstractions;

/// <summary>Something that produces normalized <see cref="InputState"/> for one or more devices (controllers, the keyboard pad).</summary>
public interface IInputSource : IDisposable
{
    /// <summary>A device appeared. May be raised from any thread; the engine queues it for its own thread.</summary>
    event Action<DeviceInfo>? DeviceAdded;

    /// <summary>A device went away. May be raised from any thread; the engine queues it for its own thread.</summary>
    event Action<DeviceId>? DeviceRemoved;

    /// <summary>Called by the engine thread.</summary>
    void Poll();

    /// <summary>Latest state of a device. False if the device is unknown or gone.</summary>
    bool TryGetState(DeviceId id, out InputState state);

    /// <summary>Rumbles a device: low-frequency (large) and high-frequency (small) motor, 0..255, for a time. No-op without rumble.</summary>
    void SetRumble(DeviceId id, byte low, byte high, int durationMs);

    /// <summary>No-op if the device has no LED.</summary>
    void SetLed(DeviceId id, byte r, byte g, byte b);
}
