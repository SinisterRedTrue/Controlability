using System.Collections.Concurrent;
using Controlability.Core.Abstractions;
using Controlability.Core.Keyboard;
using Controlability.Core.Mapping;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;
using Controlability.Platform.HidHide;

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

    private const int IdentifyRumbleMs = 300;
    private const int IdentifyBlinkMs = 150;
    private const int IdentifyBlinks = 3;

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
    private readonly IKeyboardSource? _keyboard;
    private readonly Action<Settings>? _saveSettings;
    private readonly Action<string>? _log;
    private readonly SlotManager _slots;
    private readonly UnhideOnExit? _unhideOnExit;

    private readonly ConcurrentQueue<Action> _commands = new();
    private readonly ConcurrentQueue<(IInputSource Source, DeviceInfo? Added, DeviceId? Removed)> _deviceEvents = new();
    private readonly Dictionary<DeviceId, DeviceRuntime> _devices = [];
    private readonly SlotRuntime[] _slotRuntimes = new SlotRuntime[SlotManager.SlotCount];
    private readonly List<int> _padsToConnect = [];
    private readonly List<IdentifyRun> _identifies = [];
    private readonly HashSet<DeviceId> _hidden = [];
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
        Action<string>? log = null,
        IKeyboardSource? keyboard = null)
    {
        _sources = sources;
        _sink = sink;
        _profiles = profiles;
        _clock = clock;
        _settings = settings;
        _hider = hider;
        _saveSettings = saveSettings;
        _log = log;
        _keyboard = keyboard;

        _slots = new SlotManager(clock, settings.SlotPolicy, TimeSpan.FromSeconds(settings.GracePeriodSeconds), settings.RememberedSlots)
        {
            ReserveFirstSlot = settings.Mode == KeyboardMode.KeyboardAsController,
            KeyboardDevice = settings.Mode == KeyboardMode.KeyboardAsController ? KeyboardPad.Id : null,
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

        if (keyboard is not null)
        {
            // Raised only while Mode B is on. DeviceAdded/Removed come from the engine thread, the rest from workers.
            keyboard.DeviceAdded += info => _deviceEvents.Enqueue((keyboard, info, null));
            keyboard.DeviceRemoved += id => _deviceEvents.Enqueue((keyboard, null, id));
            keyboard.PanicRequested += OnPanic;
            keyboard.Diagnostic += message => _log?.Invoke($"[keyboard] {message}");
        }

        _sink.RumbleRequested += OnRumbleRequested;
        if (hider is not null)
        {
            _unhideOnExit = new UnhideOnExit(hider, log);
        }
    }

    public event Action? Changed;

    public bool IsRunning => _loop?.IsRunning ?? false;

    public bool IsPaused => _paused;

    public KeyboardMode Mode => _settings.Mode;

    public KeyboardInputStyle KeyboardStyle => _settings.KeyboardStyle;

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
            if (_settings.HidHideEnabled && _hider is { IsAvailable: true })
            {
                _hider.WhitelistSelf();
            }

            _commands.Enqueue(ApplyMode);
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
        SyncHiding();
        SyncKeyboardBlocking();
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

    public void SetKeyboardStyle(KeyboardInputStyle style)
    {
        if (!Enum.IsDefined(style))
        {
            throw new ArgumentOutOfRangeException(nameof(style));
        }

        if (_settings.KeyboardStyle == style)
        {
            return;
        }

        _settings.KeyboardStyle = style;
        SaveSettings();
        _commands.Enqueue(() =>
        {
            SyncKeyboardBlocking();
            MarkDirty();
        });
    }

    public void SetKeyboardTargets(IReadOnlyList<string> processNames)
    {
        ArgumentNullException.ThrowIfNull(processNames);
        var names = processNames.ToList();
        _commands.Enqueue(() =>
        {
            _settings.KeyboardTargetProcesses = names;
            SaveSettings();
            if (_settings.Mode == KeyboardMode.KeyboardAsController)
            {
                SetKeyboardActive(false);
                ApplyMode();
            }
        });
    }

    public void Identify(int slot)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(slot);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(slot, SlotManager.SlotCount);
        _commands.Enqueue(() =>
        {
            if (_slotRuntimes[slot].Device is not { } runtime)
            {
                return;
            }

            runtime.Source.SetRumble(runtime.Info.Id, 255, 255, IdentifyRumbleMs);
            _identifies.RemoveAll(run => run.Slot == slot);
            _identifies.Add(new IdentifyRun(slot, runtime, _clock.Ticks));
            SetIdentifyLed(runtime, on: true);
        });
    }

    public void SetMode(KeyboardMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (_settings.Mode == mode)
        {
            return;
        }

        _settings.Mode = mode;
        SaveSettings();
        _commands.Enqueue(ApplyMode);
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

        _keyboard?.Dispose();

        // Shutdown already unhid every slot's device; this also covers a loop that never ran.
        UnhideEverything();
        _unhideOnExit?.Dispose();

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
            _identifies.Clear();

            ApplyEvents(_slots.Reset());
            SetKeyboardActive(false);
            ProcessDeviceEvents();
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

        UpdateIdentifies();

        foreach (var source in _sources)
        {
            source.Poll();
        }

        if (_keyboard is { IsActive: true })
        {
            _keyboard.Poll();
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
                if (_settings.KeyboardStyle == KeyboardInputStyle.NormalKeyboard && device.Info.Id == KeyboardPad.Id)
                {
                    // The keyboard goes to the game as a keyboard; its pad only holds the player slot.
                    raw = InputState.Neutral;
                }

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

                        // The game must never see the real pad next to the virtual one: hide before the pad connects.
                        HideDevice(runtime.Info);
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
                        UnhideDevice(leaving.Info);
                    }

                    Volatile.Write(ref slot.RumbleTarget, null);
                    _padsToConnect.Remove(e.Slot);
                    slot.Reset();
                    break;
            }

            MarkDirty();
        }

        SyncKeyboardBlocking();
    }

    private bool HidingWanted => _settings.HidHideEnabled && _hider is { IsAvailable: true };

    private void HideDevice(DeviceInfo info)
    {
        if (!HidingWanted || (_paused && _settings.UnhideWhilePaused) || !_hidden.Add(info.Id))
        {
            return;
        }

        try
        {
            _hider!.Hide(info);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Could not hide \"{info.Name}\": {ex.Message}");
        }
    }

    private void UnhideDevice(DeviceInfo info)
    {
        if (_hider is null || !_hidden.Remove(info.Id))
        {
            return;
        }

        try
        {
            _hider.Unhide(info);
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Could not unhide \"{info.Name}\": {ex.Message}");
        }
    }

    /// <summary>Pause/resume: gives the physical pads back while paused (if the setting is on) and hides them again afterwards.</summary>
    private void SyncHiding()
    {
        if (_hider is null)
        {
            return;
        }

        foreach (var slot in _slotRuntimes)
        {
            if (slot.Device is not { } device)
            {
                continue;
            }

            if (_paused && _settings.UnhideWhilePaused)
            {
                UnhideDevice(device.Info);
            }
            else if (_devices.ContainsKey(device.Info.Id))
            {
                HideDevice(device.Info);
            }
        }
    }

    private void UnhideEverything()
    {
        if (_hider is null)
        {
            return;
        }

        try
        {
            _hidden.Clear();
            _hider.UnhideAll();
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Could not unhide the controllers: {ex.Message}");
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
        if (runtime.Info.Id == KeyboardPad.Id)
        {
            _keyboard?.SetBindings(CompileBindings(profile));
        }

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

    private void UpdateIdentifies()
    {
        for (var i = _identifies.Count - 1; i >= 0; i--)
        {
            var run = _identifies[i];
            var phase = (int)(_clock.ToMs(_clock.Ticks - run.StartTicks) / IdentifyBlinkMs);
            if (phase >= IdentifyBlinks * 2)
            {
                _identifies.RemoveAt(i);
                ApplyLed(run.Slot, run.Runtime, _slotRuntimes[run.Slot].Profile);
            }
            else if (phase != run.Phase)
            {
                run.Phase = phase;
                SetIdentifyLed(run.Runtime, on: phase % 2 == 0);
            }
        }
    }

    private static void SetIdentifyLed(DeviceRuntime runtime, bool on)
    {
        if (runtime.Info.Capabilities.HasFlag(DeviceCapabilities.Led))
        {
            var level = on ? (byte)255 : (byte)0;
            runtime.Source.SetLed(runtime.Info.Id, level, level, level);
        }
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
        SaveSettings();
    }

    private void SaveSettings()
    {
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
            _log?.Invoke($"Could not save the settings: {ex.Message}");
        }
    }

    /// <summary>
    /// Puts the engine into <see cref="Settings.Mode"/>: frees every slot (the pads disconnect and the keyboard keys are
    /// released), starts or stops the keyboard pad, then fills the slots again. Pads connect lowest slot first, so
    /// Windows hands out the XInput players in slot order. Runs on the input thread.
    /// </summary>
    private void ApplyMode()
    {
        var modeB = _settings.Mode == KeyboardMode.KeyboardAsController;
        ApplyEvents(_slots.Reset());
        _slots.ReserveFirstSlot = modeB;
        _slots.KeyboardDevice = modeB ? KeyboardPad.Id : null;

        SetKeyboardActive(modeB);

        // The keyboard announced (or withdrew) itself just now; handle that before the controllers pick their slots, so
        // the keyboard pad takes slot 1 and is connected first.
        ProcessDeviceEvents();
        ApplyEvents(_slots.AutoAssignPresent());
        MarkDirty();
    }

    private void SetKeyboardActive(bool active)
    {
        if (_keyboard is null || _keyboard.IsActive == active)
        {
            return;
        }

        try
        {
            if (active)
            {
                _keyboard.Start(CompileBindings(_profiles.FindFor(KeyboardPad.Info, null)), _settings.KeyboardDevice, [.. _settings.KeyboardTargetProcesses]);
            }
            else
            {
                _keyboard.Stop();
            }
        }
        catch (Exception ex)
        {
            _log?.Invoke($"Keyboard mode could not be {(active ? "started" : "stopped")}: {ex.Message}");
        }
    }

    private KeyBindings CompileBindings(Profile profile)
        => KeyBindings.From(profile.Keyboard, message => _log?.Invoke($"{message} (profile \"{profile.Id}\")"));

    /// <summary>
    /// Keys are swallowed only while the keyboard pad is in a slot, the engine is not paused and the keyboard acts as an
    /// Xbox pad (not as a normal keyboard).
    /// </summary>
    private void SyncKeyboardBlocking()
    {
        if (_keyboard is { IsActive: true })
        {
            _keyboard.Blocking = !_paused
                && _settings.KeyboardStyle == KeyboardInputStyle.XboxController
                && _slots.FindSlot(KeyboardPad.Id) is not null;
        }
    }

    /// <summary>
    /// Ctrl+Alt+Pause. The keyboard is already free (the hook switched blocking off itself); this only makes the engine
    /// catch up. Runs on a worker thread, so it touches nothing but the settings and the command queue.
    /// </summary>
    private void OnPanic()
    {
        _log?.Invoke("Panic hotkey pressed: switching to keyboard-first co-op.");
        _settings.Mode = KeyboardMode.KeyboardFirst;
        SaveSettings();
        _commands.Enqueue(ApplyMode);
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

    private sealed class IdentifyRun(int slot, DeviceRuntime runtime, long startTicks)
    {
        public int Slot { get; } = slot;

        public DeviceRuntime Runtime { get; } = runtime;

        public long StartTicks { get; } = startTicks;

        public int Phase { get; set; }
    }

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
