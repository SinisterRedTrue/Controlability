using System.Collections.Concurrent;
using Controlability.Core.Abstractions;
using Controlability.Core.Mapping;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;

namespace Controlability.Engine;

/// <summary>
/// Runs the input loop: every tick polls the sources, applies device/slot changes, maps each active slot and submits
/// it to its virtual pad. All mutable state lives on the input thread; other threads only enqueue commands and read
/// the published snapshots.
/// </summary>
public sealed class Engine : IEngine
{
    /// <summary>Game rumble is refreshed by the game; this only bounds a motor that is never switched off.</summary>
    private const int RumbleDurationMs = 60_000;

    private static readonly (byte R, byte G, byte B)[] SlotColors =
    [
        (0, 0, 255), // P1 blue
        (255, 0, 0), // P2 red
        (0, 255, 0), // P3 green
        (255, 105, 180), // P4 pink
    ];

    private readonly IReadOnlyList<IInputSource> _sources;
    private readonly IOutputSink _sink;
    private readonly ProfileStore _profiles;
    private readonly IClock _clock;
    private readonly Settings _settings;
    private readonly IDeviceHider? _hider;
    private readonly Action<Settings>? _saveSettings;
    private readonly Action<string>? _log;
    private readonly SlotManager _slots;

    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly ConcurrentQueue<(IInputSource Source, DeviceInfo? Added, DeviceId? Removed)> _deviceEvents = new();
    private readonly Dictionary<DeviceId, DeviceRuntime> _devices = [];
    private readonly SlotRuntime[] _slotRuntimes = new SlotRuntime[SlotManager.SlotCount];
    private readonly List<int> _padsToConnect = [];
    private readonly object _lifecycle = new();

    private DeviceRuntime[] _deviceArray = [];
    private InputLoop? _loop;
    private bool _dirty;
    private bool _disposed;
    private long _tickErrors;

    private volatile IReadOnlyList<DeviceInfo> _devicesSnapshot = [];
    private volatile IReadOnlyList<SlotStatus> _slotsSnapshot;
    private volatile IReadOnlyDictionary<DeviceId, DeviceRuntime> _liveLookup = new Dictionary<DeviceId, DeviceRuntime>();
    private volatile bool _paused;

    public Engine(
        IReadOnlyList<IInputSource> sources,
        IOutputSink sink,
        ProfileStore profiles,
        IClock clock,
        Settings settings,
        IDeviceHider? hider = null,
        Action<Settings>? saveSettings = null,
        Action<string>? log = null)
    {
        _sources = sources;
        _sink = sink;
        _profiles = profiles;
        _clock = clock;
        _settings = settings;
        _hider = hider;
        _saveSettings = saveSettings;
        _log = log;

        _slots = new SlotManager(clock, settings.SlotPolicy, TimeSpan.FromSeconds(settings.GracePeriodSeconds), settings.RememberedSlots)
        {
            ReserveFirstSlot = settings.Mode == KeyboardMode.KeyboardAsController,
        };
        _slots.RememberedChanged += OnRememberedChanged;

        for (var i = 0; i < _slotRuntimes.Length; i++)
        {
            _slotRuntimes[i] = new SlotRuntime();
        }

        _slotsSnapshot = BuildSlotsSnapshot();

        foreach (var source in sources)
        {
            var captured = source;
            source.DeviceAdded += info => _deviceEvents.Enqueue((captured, info, null));
            source.DeviceRemoved += id => _deviceEvents.Enqueue((captured, null, id));
        }

        _sink.RumbleRequested += OnRumbleRequested;
    }

    public event Action? Changed;

    /// <summary>Keeps slot 1 free for the keyboard (Mode B). Takes effect for devices connecting from now on.</summary>
    public bool ReserveFirstSlot
    {
        get => _slots.ReserveFirstSlot;
        set => _commands.Enqueue(() => _slots.ReserveFirstSlot = value);
    }

    public bool IsRunning => _loop?.IsRunning ?? false;

    public bool IsPaused => _paused;

    public IReadOnlyList<DeviceInfo> Devices => _devicesSnapshot;

    public IReadOnlyList<SlotStatus> Slots => _slotsSnapshot;

    public DriverStatus DriverStatus => new(_sink.IsAvailable, _hider?.IsAvailable ?? false);

    public long TickErrorCount => Interlocked.Read(ref _tickErrors);

    public void Start()
    {
        lock (_lifecycle)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_loop is { IsRunning: true })
            {
                return;
            }

            var rate = _settings.PollingRateHz is 1000 or 500 or 250 ? _settings.PollingRateHz : 1000;
            _commands.Enqueue(() => ApplyEvents(_slots.AutoAssignPresent()));
            _loop = new InputLoop(Tick, rate, Shutdown);
            _loop.Start();
        }
    }

    public void Stop()
    {
        InputLoop? loop;
        lock (_lifecycle)
        {
            loop = _loop;
            _loop = null;
        }

        loop?.Stop();
    }

    public void Pause(bool paused) => _commands.Enqueue(() =>
    {
        _paused = paused;
        _slots.Paused = paused;
        if (paused)
        {
            foreach (var slot in _slotRuntimes)
            {
                if (Volatile.Read(ref slot.RumbleTarget) is { } target)
                {
                    target.Source.SetRumble(target.Info.Id, 0, 0, 0);
                }
            }
        }

        MarkDirty();
    });

    public void Assign(DeviceId device, int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotManager.SlotCount);
        _commands.Enqueue(() => ApplyEvents(_slots.Assign(device, slot)));
    }

    public void Unassign(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotManager.SlotCount);
        _commands.Enqueue(() => ApplyEvents(_slots.Unassign(slot)));
    }

    public void SetProfile(int slot, string profileId)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotManager.SlotCount);
        ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
        _commands.Enqueue(() =>
        {
            if (_slots.GetDevice(slot) is not { } device || !_devices.TryGetValue(device, out var runtime))
            {
                return;
            }

            var profile = _profiles.Get(profileId);
            if (profile is null)
            {
                _log?.Invoke($"Profile \"{profileId}\" not found; slot {slot + 1} keeps its profile.");
                return;
            }

            ApplyProfile(slot, runtime, profile);
            MarkDirty();
        });
    }

    public bool TryGetLiveState(DeviceId device, out InputState raw, out XboxState mapped)
    {
        if (_liveLookup.TryGetValue(device, out var runtime))
        {
            return runtime.Live.TryRead(out raw, out mapped);
        }

        raw = default;
        mapped = default;
        return false;
    }

    public void Dispose()
    {
        lock (_lifecycle)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        Stop();
        _sink.RumbleRequested -= OnRumbleRequested;
        foreach (var source in _sources)
        {
            source.Dispose();
        }

        _sink.Dispose();
    }

    /// <summary>One loop iteration. Never throws; exceptions are logged and counted.</summary>
    internal void Tick()
    {
        try
        {
            TickCore();
        }
        catch (Exception ex)
        {
            var count = Interlocked.Increment(ref _tickErrors);
            if (count <= 10 || count % 1000 == 0)
            {
                _log?.Invoke($"Input tick failed ({count} so far): {ex}");
            }
        }
    }

    /// <summary>Runs on the input thread when the loop ends: stops rumble, removes the virtual pads, empties the slots.</summary>
    internal void Shutdown()
    {
        try
        {
            // Commands queued before Stop must not leak into the next Start.
            _commands.Clear();

            ApplyEvents(_slots.Reset());
            foreach (var source in _sources)
            {
                source.Poll(); // flushes the rumble-off requests
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Engine shutdown failed: {ex}");
        }

        PublishIfDirty();
    }

    private void TickCore()
    {
        while (_commands.TryDequeue(out var command))
        {
            command();
        }

        foreach (var source in _sources)
        {
            source.Poll();
        }

        ProcessDeviceEvents();
        ReadDevices();
        ApplyEvents(_slots.Tick());
        ConnectPendingPads();
        SubmitSlots();
        PublishIfDirty();
    }

    private void ProcessDeviceEvents()
    {
        while (_deviceEvents.TryDequeue(out var e))
        {
            if (e.Added is { } info)
            {
                if (_devices.ContainsKey(info.Id))
                {
                    continue;
                }

                _devices[info.Id] = new DeviceRuntime(info, e.Source);
                RebuildDeviceViews();
                ApplyEvents(_slots.OnDeviceAdded(info.Id));
            }
            else if (e.Removed is { } id && _devices.TryGetValue(id, out var runtime) && runtime.Source == e.Source)
            {
                // The runtime stays reachable (via the slot) until the grace period ends.
                _devices.Remove(id);
                RebuildDeviceViews();
                ApplyEvents(_slots.OnDeviceRemoved(id));
            }
        }
    }

    private void ReadDevices()
    {
        foreach (var device in _deviceArray)
        {
            if (device.Source.TryGetState(device.Info.Id, out var raw))
            {
                device.Mapped = Mapper.Map(in raw, device.Mapping);
                device.HasState = true;
                device.Live.Write(in raw, in device.Mapped);
            }
            else
            {
                device.HasState = false;
            }
        }
    }

    private void ApplyEvents(IReadOnlyList<SlotEvent> events)
    {
        foreach (var e in events)
        {
            var slot = _slotRuntimes[e.Slot];
            switch (e.Kind)
            {
                case SlotEventKind.Assigned:
                    if (e.Device is { } assigned && _devices.TryGetValue(assigned, out var runtime))
                    {
                        slot.Device = runtime;
                        Volatile.Write(ref slot.RumbleTarget, runtime);
                        ApplyProfile(e.Slot, runtime, _profiles.FindFor(runtime.Info, null));
                        if (!slot.Handle.IsValid && !_padsToConnect.Contains(e.Slot))
                        {
                            _padsToConnect.Add(e.Slot);
                        }
                    }

                    break;
                case SlotEventKind.Resumed:
                    if (e.Device is { } resumed && _devices.TryGetValue(resumed, out runtime))
                    {
                        slot.Device = runtime;
                        Volatile.Write(ref slot.RumbleTarget, runtime);
                        ApplyProfile(e.Slot, runtime, slot.Profile ?? _profiles.FindFor(runtime.Info, null));
                    }

                    break;
                case SlotEventKind.Waiting:
                    Volatile.Write(ref slot.RumbleTarget, null);
                    break;
                case SlotEventKind.Emptied:
                    if (slot.Handle.IsValid)
                    {
                        _sink.Disconnect(slot.Handle);
                    }

                    if (slot.Device is { } leaving)
                    {
                        // The pad may still be connected (user unassigned it): stop any running rumble.
                        leaving.Source.SetRumble(leaving.Info.Id, 0, 0, 0);
                    }

                    Volatile.Write(ref slot.RumbleTarget, null);
                    _padsToConnect.Remove(e.Slot);
                    slot.Reset();
                    break;
            }

            MarkDirty();
        }
    }

    /// <summary>Connects the virtual pads that are due, lowest slot first: Windows numbers XInput users in connect order.</summary>
    private void ConnectPendingPads()
    {
        if (_padsToConnect.Count == 0)
        {
            return;
        }

        _padsToConnect.Sort();
        foreach (var index in _padsToConnect)
        {
            var slot = _slotRuntimes[index];
            if (slot.Device is null)
            {
                continue;
            }

            try
            {
                slot.Handle = _sink.Connect(index);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Could not connect the virtual pad for slot {index + 1}: {ex.Message}");
            }

            slot.HasLast = false;
        }

        _padsToConnect.Clear();
        MarkDirty();
    }

    private void SubmitSlots()
    {
        var paused = _paused;
        for (var i = 0; i < _slotRuntimes.Length; i++)
        {
            var slot = _slotRuntimes[i];
            if (!slot.Handle.IsValid)
            {
                continue;
            }

            var xbox = XboxState.Neutral;
            if (!paused && _slots.GetState(i) == SlotState.Active && slot.Device is { } device && device.HasState)
            {
                xbox = device.Mapped;
            }

            if (!slot.HasLast || !Equal(in slot.Last, in xbox))
            {
                _sink.Submit(slot.Handle, in xbox);
                slot.Last = xbox;
                slot.HasLast = true;
            }

            if (slot.XInputIndex is null && _sink.GetXInputUserIndex(slot.Handle) is { } index)
            {
                slot.XInputIndex = index;
                MarkDirty();
            }
        }
    }

    private void ApplyProfile(int slotIndex, DeviceRuntime runtime, Profile profile)
    {
        var slot = _slotRuntimes[slotIndex];
        slot.Profile = profile;
        _slots.SetProfileId(slotIndex, profile.Id);
        try
        {
            runtime.Mapping = ProfileMapper.ToMappingSettings(profile);
        }
        catch (ProfileFormatException ex)
        {
            _log?.Invoke($"{ex.Message} Using the default mapping for slot {slotIndex + 1}.");
            runtime.Mapping = MappingSettings.Default;
        }

        ApplyLed(slotIndex, runtime, profile);
    }

    private void ApplyLed(int slotIndex, DeviceRuntime runtime, Profile? profile)
    {
        if (!runtime.Info.Capabilities.HasFlag(DeviceCapabilities.Led))
        {
            return;
        }

        var (r, g, b) = SlotColors[slotIndex];
        if (profile?.Led is { } led && TryParseColor(led.Color, out var custom))
        {
            (r, g, b) = custom;
        }

        runtime.Source.SetLed(runtime.Info.Id, r, g, b);
    }

    private void OnRumbleRequested(SlotHandle handle, byte large, byte small)
    {
        // Runs on a ViGEm thread; SetRumble only enqueues.
        if ((uint)handle.SlotIndex >= SlotManager.SlotCount || _paused)
        {
            return;
        }

        var target = Volatile.Read(ref _slotRuntimes[handle.SlotIndex].RumbleTarget);
        target?.Source.SetRumble(target.Info.Id, large, small, large == 0 && small == 0 ? 0 : RumbleDurationMs);
    }

    private void OnRememberedChanged()
    {
        _settings.RememberedSlots = new Dictionary<string, int>(_slots.Remembered);
        if (_saveSettings is null)
        {
            return;
        }

        try
        {
            _saveSettings(_settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Invoke($"Could not save the remembered slots: {ex.Message}");
        }
    }

    private void RebuildDeviceViews()
    {
        _deviceArray = [.. _devices.Values];
        _devicesSnapshot = _deviceArray.Select(d => d.Info).ToList();
        _liveLookup = _devices.ToDictionary(pair => pair.Key, pair => pair.Value);
        MarkDirty();
    }

    private void MarkDirty() => _dirty = true;

    private void PublishIfDirty()
    {
        if (!_dirty)
        {
            return;
        }

        _dirty = false;
        _slotsSnapshot = BuildSlotsSnapshot();
        Changed?.Invoke();
    }

    private IReadOnlyList<SlotStatus> BuildSlotsSnapshot()
    {
        var list = new SlotStatus[SlotManager.SlotCount];
        for (var i = 0; i < list.Length; i++)
        {
            var device = _slots.GetDevice(i);
            var runtime = _slotRuntimes[i];
            list[i] = new SlotStatus(
                i,
                device,
                runtime.Device?.Info.Name,
                _slots.GetProfileId(i),
                _slots.GetState(i),
                runtime.XInputIndex);
        }

        return list;
    }

    private static bool TryParseColor(string text, out (byte R, byte G, byte B) color)
    {
        color = default;
        var hex = text.TrimStart('#');
        if (hex.Length != 6)
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromHexString(hex);
            color = (bytes[0], bytes[1], bytes[2]);
            return true;
        }
        catch (FormatException)
        {
            return false;
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

    private sealed class SlotRuntime
    {
        public DeviceRuntime? Device;
        public DeviceRuntime? RumbleTarget;
        public Profile? Profile;
        public SlotHandle Handle = SlotHandle.Invalid;
        public XboxState Last;
        public bool HasLast;
        public int? XInputIndex;

        public void Reset()
        {
            Device = null;
            Profile = null;
            Handle = SlotHandle.Invalid;
            Last = default;
            HasLast = false;
            XInputIndex = null;
        }
    }

    private sealed class DeviceRuntime(DeviceInfo info, IInputSource source)
    {
        public DeviceInfo Info { get; } = info;

        public IInputSource Source { get; } = source;

        public LiveStateBuffer Live { get; } = new();

        public MappingSettings Mapping { get; set; } = MappingSettings.Default;

        public bool HasState;

        public XboxState Mapped;
    }
}
