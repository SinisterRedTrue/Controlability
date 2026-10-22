using Controlability.Core.Abstractions;
using Controlability.Core.Keyboard;
using Controlability.Core.Models;

namespace Controlability.Input.Keyboard;

/// <summary>
/// Mode B input: all keyboards and mice become one keyboard pad (<see cref="KeyboardPad.Info"/>). Raw Input
/// (<see cref="RawInputWindow"/>) supplies the pressed keys per physical keyboard and the mouse movement; the
/// <see cref="KeyboardBlocker"/> keeps the bound keys from the game; a watchdog repairs the hook if Windows dropped it.
/// <para>
/// <b>Threading:</b> Raw Input events arrive on the window thread and only touch state under <c>_gate</c>.
/// <see cref="Poll"/> runs on the engine thread: it turns that state into an <see cref="InputState"/> with the
/// profile's <see cref="KeyBindings"/>. <see cref="TryGetState"/> returns the last snapshot from any thread.
/// Nothing exists before <see cref="Start"/>, so in Mode A no keyboard code runs at all.
/// </para>
/// </summary>
public sealed class RawInputKeyboardSource : IKeyboardSource
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(100);
    private const int WatchdogEveryTicks = 5;

    private readonly IClock _clock;
    private readonly object _gate = new();
    private readonly Dictionary<DeviceId, HashSet<int>> _pressedByKeyboard = [];
    private readonly Dictionary<DeviceId, DeviceInfo> _keyboards = [];
    private readonly HashSet<int> _merged = [];
    private readonly KeyboardConverter _converter = new(KeyBindings.Empty);

    private RawInputWindow? _window;
    private KeyboardBlocker? _blocker;
    private readonly ForegroundWatcher _foreground = new();
    private Timer? _watchdog;
    private IReadOnlyList<string> _targets = [];
    private volatile bool _wantBlocking;
    private volatile bool _listening = true;
    private int _ticks;
    private DeviceId? _onlyKeyboard;
    private MouseButtons _mouseButtons;
    private int _mouseDx;
    private int _mouseDy;
    private InputState _state;
    private bool _hasState;
    private long _lastPollTicks;
    private long _lastRawKeyMs;
    private bool _active;
    private bool _disposed;

    public RawInputKeyboardSource(IClock clock)
    {
        _clock = clock;
    }

    public event Action<DeviceInfo>? DeviceAdded;

    public event Action<DeviceId>? DeviceRemoved;

    public event Action? PanicRequested;

    public event Action<string>? Diagnostic;

    public bool IsActive => _active;

    public IReadOnlyList<DeviceInfo> Keyboards
    {
        get
        {
            lock (_gate)
            {
                return [.. _keyboards.Values];
            }
        }
    }

    /// <summary>What the engine wants. The hook is only really on while a target game has the focus, see <see cref="ForegroundPolicy"/>.</summary>
    public bool Blocking
    {
        get => _wantBlocking;
        set
        {
            _wantBlocking = value;
            ApplyFocus();
        }
    }

    public void Start(KeyBindings bindings, string? keyboardId, IReadOnlyList<string> targetProcesses)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_active)
        {
            return;
        }

        _targets = targetProcesses;
        _wantBlocking = false;
        _listening = ForegroundPolicy.ShouldListen(targetProcesses, null);
        _converter.Bindings = bindings;
        _converter.Reset();
        _onlyKeyboard = string.IsNullOrWhiteSpace(keyboardId) || keyboardId.Equals("any", StringComparison.OrdinalIgnoreCase)
            ? null
            : new DeviceId(keyboardId);
        lock (_gate)
        {
            ClearInput();
            _keyboards.Clear();
            foreach (var keyboard in RawInputWindow.EnumerateKeyboards())
            {
                _keyboards[keyboard.Id] = keyboard;
            }
        }

        var window = new RawInputWindow();
        window.Key += OnKey;
        window.Mouse += OnMouse;
        window.KeyboardArrived += OnKeyboardArrived;
        window.KeyboardRemoved += OnKeyboardRemoved;
        window.Start();
        _window = window;

        var blocker = new KeyboardBlocker();
        blocker.PanicTriggered += OnPanic;
        try
        {
            blocker.Start(bindings);
            _blocker = blocker;
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            // Fail safe: the pad still works, the game just also sees the keys. Nothing is swallowed.
            blocker.Dispose();
            Diagnostic?.Invoke($"Keys will not be hidden from the game: {ex.Message}");
        }

        Interlocked.Exchange(ref _lastRawKeyMs, 0);
        _lastPollTicks = _clock.Ticks;
        _ticks = 0;
        _watchdog = new Timer(_ => OnTick(), null, TickInterval, TickInterval);
        _active = true;
        DeviceAdded?.Invoke(KeyboardPad.Info);
    }

    public void SetBindings(KeyBindings bindings)
    {
        _converter.Bindings = bindings;
        _blocker?.SetBindings(bindings);
    }

    public void Stop()
    {
        if (!_active)
        {
            return;
        }

        _active = false;
        _watchdog?.Dispose();
        _watchdog = null;

        _blocker?.Dispose();
        _blocker = null;
        if (_window is { } window)
        {
            window.Key -= OnKey;
            window.Mouse -= OnMouse;
            window.KeyboardArrived -= OnKeyboardArrived;
            window.KeyboardRemoved -= OnKeyboardRemoved;
            window.Dispose();
            _window = null;
        }

        lock (_gate)
        {
            ClearInput();
            _keyboards.Clear();
            _hasState = false;
            _state = InputState.Neutral;
        }

        _converter.Reset();
        DeviceRemoved?.Invoke(KeyboardPad.Id);
    }

    public void Poll()
    {
        if (!_active)
        {
            return;
        }

        var now = _clock.Ticks;
        var dtMs = _clock.ToMs(now - _lastPollTicks);
        _lastPollTicks = now;

        InputState state;
        lock (_gate)
        {
            if (!_listening)
            {
                // Not in a target game: the keyboard is just a keyboard. Drop anything pressed or moved meanwhile.
                _mouseDx = 0;
                _mouseDy = 0;
                _converter.Reset();
                _state = new InputState { TimestampTicks = now };
                _hasState = true;
                return;
            }

            _merged.Clear();
            foreach (var (id, keys) in _pressedByKeyboard)
            {
                if (_onlyKeyboard is null || _onlyKeyboard == id)
                {
                    _merged.UnionWith(keys);
                }
            }

            state = _converter.Convert(_merged, _mouseButtons, _mouseDx, _mouseDy, dtMs);
            _mouseDx = 0;
            _mouseDy = 0;
            state.TimestampTicks = now;
            _state = state;
            _hasState = true;
        }
    }

    public bool TryGetState(DeviceId id, out InputState state)
    {
        if (id == KeyboardPad.Id)
        {
            lock (_gate)
            {
                if (_hasState)
                {
                    state = _state;
                    return true;
                }
            }
        }

        state = default;
        return false;
    }

    public void SetRumble(DeviceId id, byte low, byte high, int durationMs)
    {
    }

    public void SetLed(DeviceId id, byte r, byte g, byte b)
    {
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    private void ClearInput()
    {
        _pressedByKeyboard.Clear();
        _mouseButtons = MouseButtons.None;
        _mouseDx = 0;
        _mouseDy = 0;
    }

    private void OnKey(RawKeyEvent e)
    {
        lock (_gate)
        {
            if (!_pressedByKeyboard.TryGetValue(e.Keyboard, out var keys))
            {
                keys = [];
                _pressedByKeyboard[e.Keyboard] = keys;
            }

            if (e.Down)
            {
                keys.Add(e.VirtualKey);
            }
            else
            {
                keys.Remove(e.VirtualKey);
            }

            if (!_keyboards.ContainsKey(e.Keyboard))
            {
                _keyboards[e.Keyboard] = RawDeviceName.ToDeviceInfo(e.Keyboard.Value);
            }
        }

        if (e.Down)
        {
            Interlocked.Exchange(ref _lastRawKeyMs, Environment.TickCount64);
        }
    }

    private void OnMouse(RawMouseEvent e)
    {
        lock (_gate)
        {
            _mouseDx += e.DeltaX;
            _mouseDy += e.DeltaY;
            _mouseButtons = e.Buttons;
        }
    }

    private void OnKeyboardArrived(DeviceInfo info)
    {
        lock (_gate)
        {
            _keyboards[info.Id] = info;
        }
    }

    private void OnKeyboardRemoved(DeviceId id)
    {
        lock (_gate)
        {
            _keyboards.Remove(id);
            _pressedByKeyboard.Remove(id); // a keyboard unplugged with a key held must not leave it pressed
        }
    }

    private void OnPanic()
    {
        Diagnostic?.Invoke("Panic hotkey Ctrl+Alt+Pause: keyboard capture is off.");
        PanicRequested?.Invoke();
    }

    private void OnTick()
    {
        if (!_active)
        {
            return;
        }

        if (_targets.Count > 0)
        {
            _foreground.Refresh();
        }

        ApplyFocus();
        if (++_ticks % WatchdogEveryTicks == 0)
        {
            CheckHook();
        }
    }

    /// <summary>Blocking and listening follow the foreground window, so Mode B never takes over the rest of the PC.</summary>
    private void ApplyFocus()
    {
        var process = _foreground.Current;
        _listening = ForegroundPolicy.ShouldListen(_targets, process);
        if (_blocker is { } blocker)
        {
            blocker.Blocking = _wantBlocking && ForegroundPolicy.ShouldBlock(_targets, process);
        }
    }

    private void CheckHook()
    {
        if (_blocker is not { } blocker)
        {
            return;
        }

        var rawMs = Interlocked.Read(ref _lastRawKeyMs);
        if (HookWatchdog.ShouldReinstall(blocker.Blocking, rawMs, blocker.LastKeyEventMs))
        {
            Diagnostic?.Invoke("Raw Input sees key presses but the keyboard hook saw none for 2 s: reinstalling the hook.");
            blocker.Reinstall();
        }
    }
}
