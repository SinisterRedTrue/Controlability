using Controlability.Core.Models;

namespace Controlability.Engine;

public enum SlotState
{
    Empty,
    Active,

    /// <summary>The device vanished; the virtual pad stays connected until the grace period ends.</summary>
    Waiting,

    /// <summary>The engine is paused: the pad is connected and held at neutral.</summary>
    Paused,
}

/// <param name="XInputIndex">
/// XInput user index (0..3) of the slot's virtual pad. Windows assigns it in connect order, so it differs from
/// <paramref name="Index"/> when a lower slot was empty while this one was connected. Null until known.
/// </param>
public sealed record SlotStatus(
    int Index,
    DeviceId? Device,
    string? DeviceName,
    string? ProfileId,
    SlotState State,
    int? XInputIndex);

public readonly record struct DriverStatus(bool ViGEmAvailable, bool HidHideAvailable);

/// <summary>The only thing the UI and the CLI talk to.</summary>
public interface IEngine : IDisposable
{
    bool IsRunning { get; }

    bool IsPaused { get; }

    /// <summary>Snapshot of the connected devices.</summary>
    IReadOnlyList<DeviceInfo> Devices { get; }

    /// <summary>Snapshot of the 4 slots.</summary>
    IReadOnlyList<SlotStatus> Slots { get; }

    DriverStatus DriverStatus { get; }

    /// <summary>Number of input-loop ticks that threw (and were survived).</summary>
    long TickErrorCount { get; }

    /// <summary>Raised on the input thread when devices or slots changed. The UI must marshal to its own thread.</summary>
    event Action? Changed;

    void Start();

    void Stop();

    /// <summary>Paused: all assigned pads are held at neutral; devices keep their slots.</summary>
    void Pause(bool paused);

    void Assign(DeviceId device, int slot);

    void Unassign(int slot);

    void SetProfile(int slot, string profileId);

    /// <summary>
    /// Latest raw and mapped state of a device, for the live test panel. Safe from any thread, takes no lock on the
    /// input thread's path. Returns false if the device is unknown or has not produced a state yet.
    /// </summary>
    bool TryGetLiveState(DeviceId device, out InputState raw, out XboxState mapped);
}
