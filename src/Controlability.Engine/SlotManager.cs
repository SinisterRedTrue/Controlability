using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Core.Settings;

namespace Controlability.Engine;

public enum SlotEventKind
{
    /// <summary>A device took the slot. The slot needs a virtual pad if it has none yet.</summary>
    Assigned,

    /// <summary>The device vanished; the slot keeps its pad until the grace period ends.</summary>
    Waiting,

    /// <summary>The device came back in time and has its slot again.</summary>
    Resumed,

    /// <summary>The slot is empty now; its virtual pad must be disconnected.</summary>
    Emptied,
}

/// <param name="Device">The device the event is about (for <see cref="SlotEventKind.Emptied"/>: the one that left).</param>
public readonly record struct SlotEvent(SlotEventKind Kind, int Slot, DeviceId? Device);

/// <summary>
/// Decides which device sits in which of the 4 slots. Pure logic: no threads, no I/O, time comes from the
/// <see cref="IClock"/>. Not thread-safe; the engine calls it from its input thread only.
/// </summary>
public sealed class SlotManager
{
    public const int SlotCount = 4;

    private readonly IClock _clock;
    private readonly Slot[] _slots = new Slot[SlotCount];
    private readonly List<DeviceId> _present = [];
    private readonly Dictionary<string, int> _remembered;

    public SlotManager(
        IClock clock,
        SlotPolicy policy = SlotPolicy.Auto,
        TimeSpan? gracePeriod = null,
        IReadOnlyDictionary<string, int>? remembered = null)
    {
        _clock = clock;
        Policy = policy;
        GracePeriod = gracePeriod ?? TimeSpan.FromSeconds(10);
        _remembered = remembered is null ? [] : new Dictionary<string, int>(remembered);
        for (var i = 0; i < _slots.Length; i++)
        {
            _slots[i] = new Slot();
        }
    }

    public SlotPolicy Policy { get; set; }

    public TimeSpan GracePeriod { get; set; }

    /// <summary>Keeps slot 0 free for the keyboard: Auto and Remember skip it. An explicit <see cref="Assign"/> may still use it.</summary>
    public bool ReserveFirstSlot { get; set; }

    public bool Paused { get; set; }

    /// <summary>Stable device id to last slot. Raised <see cref="RememberedChanged"/> when it changes.</summary>
    public IReadOnlyDictionary<string, int> Remembered => _remembered;

    public event Action? RememberedChanged;

    public SlotState GetState(int slot)
    {
        var entry = _slots[slot];
        if (entry.Device is null)
        {
            return SlotState.Empty;
        }

        if (!entry.Present)
        {
            return SlotState.Waiting;
        }

        return Paused ? SlotState.Paused : SlotState.Active;
    }

    public DeviceId? GetDevice(int slot) => _slots[slot].Device;

    public string? GetProfileId(int slot) => _slots[slot].ProfileId;

    public void SetProfileId(int slot, string? profileId) => _slots[slot].ProfileId = profileId;

    public int? FindSlot(DeviceId device)
    {
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Device == device)
            {
                return i;
            }
        }

        return null;
    }

    public IReadOnlyList<SlotEvent> OnDeviceAdded(DeviceId device)
    {
        if (_present.Contains(device))
        {
            return [];
        }

        _present.Add(device);

        if (FindSlot(device) is { } own)
        {
            // Back before the grace period ended: same slot, whatever the policy.
            _slots[own].Present = true;
            return [new SlotEvent(SlotEventKind.Resumed, own, device)];
        }

        return TryAutoAssign(device);
    }

    /// <summary>Applies the slot policy to connected devices that have no slot (after the engine restarted).</summary>
    public IReadOnlyList<SlotEvent> AutoAssignPresent()
    {
        var events = new List<SlotEvent>();
        foreach (var device in _present.ToArray())
        {
            if (FindSlot(device) is null)
            {
                events.AddRange(TryAutoAssign(device));
            }
        }

        return events;
    }

    private IReadOnlyList<SlotEvent> TryAutoAssign(DeviceId device)
    {
        if (Policy == SlotPolicy.Manual)
        {
            return [];
        }

        var target = Policy == SlotPolicy.Remember && _remembered.TryGetValue(device.Value, out var last) && IsFreeForAuto(last)
            ? last
            : LowestFreeSlot();
        if (target is null)
        {
            return [];
        }

        return Occupy(device, target.Value, []);
    }

    public IReadOnlyList<SlotEvent> OnDeviceRemoved(DeviceId device)
    {
        if (!_present.Remove(device) || FindSlot(device) is not { } slot)
        {
            return [];
        }

        if (GracePeriod <= TimeSpan.Zero)
        {
            return Free(slot, []);
        }

        var entry = _slots[slot];
        entry.Present = false;
        entry.GraceEndsMs = NowMs() + GracePeriod.TotalMilliseconds;
        return [new SlotEvent(SlotEventKind.Waiting, slot, device)];
    }

    /// <summary>Puts a connected device into a slot. Moves it if it already has another slot; replaces the occupant.</summary>
    public IReadOnlyList<SlotEvent> Assign(DeviceId device, int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotCount);

        if (!_present.Contains(device) || _slots[slot].Device == device)
        {
            return [];
        }

        var events = new List<SlotEvent>();
        if (FindSlot(device) is { } previous)
        {
            Free(previous, events);
        }

        return Occupy(device, slot, events);
    }

    public IReadOnlyList<SlotEvent> Unassign(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotCount);
        return _slots[slot].Device is null ? [] : Free(slot, []);
    }

    /// <summary>Ends the grace period of slots whose device did not come back.</summary>
    public IReadOnlyList<SlotEvent> Tick()
    {
        List<SlotEvent>? events = null;
        var now = NowMs();
        for (var i = 0; i < _slots.Length; i++)
        {
            var entry = _slots[i];
            if (entry.Device is not null && !entry.Present && now >= entry.GraceEndsMs)
            {
                Free(i, events ??= []);
            }
        }

        return events ?? [];
    }

    /// <summary>Empties every slot (engine stopped). Connected devices stay known.</summary>
    public IReadOnlyList<SlotEvent> Reset()
    {
        var events = new List<SlotEvent>();
        for (var i = 0; i < _slots.Length; i++)
        {
            if (_slots[i].Device is not null)
            {
                Free(i, events);
            }
        }

        return events;
    }

    private List<SlotEvent> Occupy(DeviceId device, int slot, List<SlotEvent> events)
    {
        var entry = _slots[slot];
        entry.Device = device;
        entry.Present = true;
        entry.ProfileId = null;
        if (!_remembered.TryGetValue(device.Value, out var old) || old != slot)
        {
            _remembered[device.Value] = slot;
            RememberedChanged?.Invoke();
        }

        events.Add(new SlotEvent(SlotEventKind.Assigned, slot, device));
        return events;
    }

    private List<SlotEvent> Free(int slot, List<SlotEvent> events)
    {
        var entry = _slots[slot];
        events.Add(new SlotEvent(SlotEventKind.Emptied, slot, entry.Device));
        entry.Device = null;
        entry.Present = false;
        entry.ProfileId = null;
        return events;
    }

    private int? LowestFreeSlot()
    {
        for (var i = ReserveFirstSlot ? 1 : 0; i < _slots.Length; i++)
        {
            if (_slots[i].Device is null)
            {
                return i;
            }
        }

        return null;
    }

    private bool IsFreeForAuto(int slot) => slot >= (ReserveFirstSlot ? 1 : 0) && slot < SlotCount && _slots[slot].Device is null;

    private double NowMs() => _clock.ToMs(_clock.Ticks);

    private sealed class Slot
    {
        public DeviceId? Device;
        public bool Present;
        public double GraceEndsMs;
        public string? ProfileId;
    }
}
