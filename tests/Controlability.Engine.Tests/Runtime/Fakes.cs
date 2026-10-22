using Controlability.Core.Abstractions;
using Controlability.Core.Models;

namespace Controlability.Engine.Tests.Runtime;

public sealed class FakeClock : IClock
{
    public long Ticks { get; private set; }

    public double ToMs(long ticks) => ticks / 1000.0;

    public void Advance(TimeSpan time) => Ticks += (long)(time.TotalMilliseconds * 1000);
}

public sealed class FakeInputSource : IInputSource
{
    private readonly Dictionary<DeviceId, InputState> _states = [];
    private readonly object _gate = new();

    public event Action<DeviceInfo>? DeviceAdded;

    public event Action<DeviceId>? DeviceRemoved;

    public int PollCount { get; private set; }

    public Action? OnPoll { get; set; }

    public List<(DeviceId Id, byte Low, byte High, int DurationMs)> Rumbles { get; } = [];

    public List<(DeviceId Id, byte R, byte G, byte B)> Leds { get; } = [];

    public bool Disposed { get; private set; }

    public static DeviceInfo MakeDevice(string name, DeviceCapabilities capabilities = DeviceCapabilities.Rumble | DeviceCapabilities.Led)
        => new(new DeviceId(name), name, 0x054C, 0x09CC, name, null, ConnectionType.Usb, ControllerType.PS4, capabilities);

    public void Plug(DeviceInfo device)
    {
        lock (_gate)
        {
            _states[device.Id] = InputState.Neutral;
        }

        DeviceAdded?.Invoke(device);
    }

    public void Unplug(DeviceId id)
    {
        lock (_gate)
        {
            _states.Remove(id);
        }

        DeviceRemoved?.Invoke(id);
    }

    public void SetState(DeviceId id, InputState state)
    {
        lock (_gate)
        {
            _states[id] = state;
        }
    }

    public void Poll()
    {
        PollCount++;
        OnPoll?.Invoke();
    }

    public bool TryGetState(DeviceId id, out InputState state)
    {
        lock (_gate)
        {
            return _states.TryGetValue(id, out state);
        }
    }

    public void SetRumble(DeviceId id, byte low, byte high, int durationMs)
    {
        lock (_gate)
        {
            Rumbles.Add((id, low, high, durationMs));
        }
    }

    public void SetLed(DeviceId id, byte r, byte g, byte b)
    {
        lock (_gate)
        {
            Leds.Add((id, r, g, b));
        }
    }

    public void Dispose() => Disposed = true;
}

public sealed class FakeOutputSink : IOutputSink
{
    private readonly object _gate = new();
    private readonly Dictionary<int, int> _xinput = [];
    private int _connects;

    public bool IsAvailable { get; set; } = true;

    public event Action<SlotHandle, byte, byte>? RumbleRequested;

    /// <summary>Slot indexes in the order Connect was called.</summary>
    public List<int> Connected { get; } = [];

    public List<int> Disconnected { get; } = [];

    /// <summary>Called at the start of <see cref="Connect"/>, so tests can record the order against other fakes.</summary>
    public Action<int>? OnConnect { get; set; }

    public List<(int Slot, XboxState State)> Submitted { get; } = [];

    public int SubmitCount(int slot)
    {
        lock (_gate)
        {
            return Submitted.Count(s => s.Slot == slot);
        }
    }

    public XboxState LastSubmitted(int slot)
    {
        lock (_gate)
        {
            return Submitted.Last(s => s.Slot == slot).State;
        }
    }

    public SlotHandle Connect(int slotIndex)
    {
        OnConnect?.Invoke(slotIndex);
        lock (_gate)
        {
            Connected.Add(slotIndex);
            _xinput[slotIndex] = _connects++;
        }

        return new SlotHandle(slotIndex);
    }

    public void Submit(SlotHandle slot, in XboxState state)
    {
        lock (_gate)
        {
            Submitted.Add((slot.SlotIndex, state));
        }
    }

    public void Disconnect(SlotHandle slot)
    {
        lock (_gate)
        {
            Disconnected.Add(slot.SlotIndex);
            _xinput.Remove(slot.SlotIndex);
        }
    }

    public int? GetXInputUserIndex(SlotHandle slot)
    {
        lock (_gate)
        {
            return _xinput.TryGetValue(slot.SlotIndex, out var index) ? index : null;
        }
    }

    public void RaiseRumble(int slot, byte large, byte small) => RumbleRequested?.Invoke(new SlotHandle(slot), large, small);

    public void Dispose()
    {
    }
}
