using Controlability.Core.Abstractions;
using Controlability.Core.Models;

namespace Controlability.Output.ViGEm;

/// <summary>
/// Virtual Xbox 360 pads on top of ViGEmBus. When the driver is missing <see cref="IsAvailable"/> is false and
/// every method is a no-op. Connect pads in slot order: Windows hands out XInput user indexes in connect order.
/// </summary>
public sealed class ViGEmSink : IOutputSink
{
    private readonly IXboxTargetFactory _factory;
    private readonly object _gate = new();
    private readonly Dictionary<int, Slot> _slots = [];
    private bool _disposed;

    public ViGEmSink()
        : this(new ViGEmTargetFactory())
    {
    }

    internal ViGEmSink(IXboxTargetFactory factory)
    {
        _factory = factory;
    }

    public event Action<SlotHandle, byte, byte>? RumbleRequested;

    public bool IsAvailable => !_disposed && _factory.IsAvailable;

    public SlotHandle Connect(int slotIndex)
    {
        if (!IsAvailable)
        {
            return SlotHandle.Invalid;
        }

        var handle = new SlotHandle(slotIndex);
        lock (_gate)
        {
            if (_slots.ContainsKey(slotIndex))
            {
                return handle;
            }

            var target = _factory.Create();
            try
            {
                target.Feedback += (large, small) => RumbleRequested?.Invoke(handle, large, small);
                target.Connect();
            }
            catch
            {
                target.Dispose();
                throw;
            }

            _slots[slotIndex] = new Slot(target);
        }

        return handle;
    }

    public void Submit(SlotHandle slot, in XboxState state) => SubmitIfChanged(slot, in state);

    /// <summary>Sends a report only when <paramref name="state"/> differs from the last one sent. Returns true if it did.</summary>
    public bool SubmitIfChanged(SlotHandle slot, in XboxState state)
    {
        var entry = Find(slot);
        if (entry is null || (entry.HasLast && Equal(in entry.Last, in state)))
        {
            return false;
        }

        entry.Target.Submit(in state);
        entry.Last = state;
        entry.HasLast = true;
        return true;
    }

    public void Disconnect(SlotHandle slot)
    {
        Slot? entry;
        lock (_gate)
        {
            if (!_slots.Remove(slot.SlotIndex, out entry))
            {
                return;
            }
        }

        entry.Target.Dispose();
    }

    /// <summary>The XInput user index (0..3) of the pad, or null if the driver has not reported it yet or the slot is not connected.</summary>
    public int? GetXInputUserIndex(SlotHandle slot)
    {
        var entry = Find(slot);
        if (entry is null)
        {
            return null;
        }

        entry.UserIndex ??= entry.Target.UserIndex;
        return entry.UserIndex;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Slot[] slots;
        lock (_gate)
        {
            slots = [.. _slots.Values];
            _slots.Clear();
        }

        foreach (var slot in slots)
        {
            try
            {
                slot.Target.Dispose();
            }
            catch (Exception)
            {
                // Best effort: keep disconnecting the remaining pads.
            }
        }

        _factory.Dispose();
    }

    private Slot? Find(SlotHandle slot)
    {
        lock (_gate)
        {
            return _slots.GetValueOrDefault(slot.SlotIndex);
        }
    }

    private static bool Equal(in XboxState a, in XboxState b) =>
        a.Buttons == b.Buttons
        && a.LeftTrigger == b.LeftTrigger
        && a.RightTrigger == b.RightTrigger
        && a.LeftX == b.LeftX
        && a.LeftY == b.LeftY
        && a.RightX == b.RightX
        && a.RightY == b.RightY;

    private sealed class Slot(IXboxTarget target)
    {
        public IXboxTarget Target { get; } = target;

        public XboxState Last;

        public bool HasLast;

        public int? UserIndex;
    }
}
