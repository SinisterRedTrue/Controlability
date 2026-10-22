using Controlability.Core.Models;

namespace Controlability.Core.Abstractions;

public interface IOutputSink : IDisposable
{
    /// <summary>False when the driver (ViGEmBus) is not installed.</summary>
    bool IsAvailable { get; }

    SlotHandle Connect(int slotIndex);

    void Submit(SlotHandle slot, in XboxState state);

    void Disconnect(SlotHandle slot);

    /// <summary>
    /// The XInput user index (0..3) Windows gave the pad, or null if unknown yet. Windows hands the indexes out in
    /// connect order, so it equals the slot index only if the pads were connected in slot order without gaps.
    /// </summary>
    int? GetXInputUserIndex(SlotHandle slot);

    /// <summary>Raised when the game sends rumble: large motor, small motor.</summary>
    event Action<SlotHandle, byte, byte>? RumbleRequested;
}
