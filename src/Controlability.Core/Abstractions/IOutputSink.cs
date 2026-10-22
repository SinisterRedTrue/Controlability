using Controlability.Core.Models;

namespace Controlability.Core.Abstractions;

public interface IOutputSink : IDisposable
{
    /// <summary>False when the driver (ViGEmBus) is not installed.</summary>
    bool IsAvailable { get; }

    SlotHandle Connect(int slotIndex);

    void Submit(SlotHandle slot, in XboxState state);

    void Disconnect(SlotHandle slot);

    /// <summary>Raised when the game sends rumble: large motor, small motor.</summary>
    event Action<SlotHandle, byte, byte>? RumbleRequested;
}
