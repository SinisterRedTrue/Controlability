using Controlability.Core.Models;

namespace Controlability.Core.Abstractions;

/// <summary>Where mapped <see cref="XboxState"/> goes: virtual Xbox 360 pads (ViGEmBus).</summary>
public interface IOutputSink : IDisposable
{
    /// <summary>False when the driver (ViGEmBus) is not installed.</summary>
    bool IsAvailable { get; }

    /// <summary>Plugs in a virtual pad for a slot. Returns <see cref="SlotHandle.Invalid"/> when the driver is missing.</summary>
    SlotHandle Connect(int slotIndex);

    /// <summary>Sends a state to the pad. Cheap to call every tick: an unchanged state is not sent again.</summary>
    void Submit(SlotHandle slot, in XboxState state);

    /// <summary>Unplugs the virtual pad. Safe to call twice.</summary>
    void Disconnect(SlotHandle slot);

    /// <summary>
    /// The XInput user index (0..3) Windows gave the pad, or null if unknown yet. Windows hands the indexes out in
    /// connect order, so it equals the slot index only if the pads were connected in slot order without gaps.
    /// </summary>
    int? GetXInputUserIndex(SlotHandle slot);

    /// <summary>Raised when the game sends rumble: large motor, small motor.</summary>
    event Action<SlotHandle, byte, byte>? RumbleRequested;
}
