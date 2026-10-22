using Controlability.Core.Models;
using Controlability.Core.Settings;

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

    /// <summary>Keyboard-first co-op (Mode A) or keyboard-as-controller (Mode B).</summary>
    KeyboardMode Mode { get; }

    /// <summary>Mode B only: Xbox pad input or the normal keyboard (see <see cref="KeyboardInputStyle"/>).</summary>
    KeyboardInputStyle KeyboardStyle { get; }

    /// <summary>Snapshot of the connected devices.</summary>
    IReadOnlyList<DeviceInfo> Devices { get; }

    /// <summary>Snapshot of the 4 slots.</summary>
    IReadOnlyList<SlotStatus> Slots { get; }

    DriverStatus DriverStatus { get; }

    /// <summary>
    /// Process name of the game whose own profile is in use because it has the focus, or null. Follows the foreground
    /// window about once a second.
    /// </summary>
    string? ActiveGame { get; }

    /// <summary>Number of input-loop ticks that threw (and were survived).</summary>
    long TickErrorCount { get; }

    /// <summary>Devices HidHide currently hides from games (snapshot).</summary>
    IReadOnlyCollection<DeviceId> HiddenDevices { get; }

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
    /// Sets the games (process names) whose focus makes Mode B swallow keys and listen, and saves it. In Mode B the
    /// keyboard pad restarts so the new list applies at once; pads reconnect like after a mode switch.
    /// </summary>
    void SetKeyboardTargets(IReadOnlyList<string> processNames);

    /// <summary>Switches the Mode B keyboard style at runtime and saves it. No pad is reconnected.</summary>
    void SetKeyboardStyle(KeyboardInputStyle style);

    /// <summary>Rumbles the slot's device for 300 ms and blinks its LED (if it has one), so the user can find it.</summary>
    void Identify(int slot);

    /// <summary>
    /// Re-reads the profile from the store and applies it to every slot that uses it. The slot's mapping is swapped
    /// in one step on the input thread, so a report never mixes old and new settings. Does nothing if no slot uses it.
    /// </summary>
    void ReloadProfile(string profileId);

    /// <summary>Hides a device from games, or gives it back. Does nothing when HidHide is missing or off.</summary>
    void SetDeviceHidden(DeviceId device, bool hidden);

    /// <summary>Rumbles one device for 300 ms, assigned to a slot or not.</summary>
    void TestRumble(DeviceId device);

    /// <summary>Sets a device's light bar. A profile applied later (or a reconnect) sets the slot color again.</summary>
    void SetLedColor(DeviceId device, byte r, byte g, byte b);

    /// <summary>
    /// Switches the keyboard mode at runtime and saves it. All pads are released and reconnected in slot order, because
    /// games read the player order at connect time. Mode B puts the keyboard pad in slot 1.
    /// </summary>
    void SetMode(KeyboardMode mode);

    /// <summary>
    /// Re-reads the shared <see cref="Settings"/> object after the UI changed it and applies what can change at runtime:
    /// slot policy, grace period, polling rate and HidHide on/off.
    /// </summary>
    void ApplySettings();

    /// <summary>
    /// Gives every hidden controller back to Windows now, even while pads are active. Works when the engine is stopped,
    /// too (the crash handler uses it).
    /// </summary>
    void UnhideAll();

    /// <summary>
    /// Closes and re-opens the input sources (after the PC woke from sleep). Devices come back as new connections and
    /// return to their slots through the grace period.
    /// </summary>
    void RestartInput();

    /// <summary>
    /// Latest raw and mapped state of a device, for the live test panel. Safe from any thread, takes no lock on the
    /// input thread's path. Returns false if the device is unknown or has not produced a state yet.
    /// </summary>
    bool TryGetLiveState(DeviceId device, out InputState raw, out XboxState mapped);
}
