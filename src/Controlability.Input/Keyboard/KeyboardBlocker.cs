using System.Runtime.InteropServices;
using Controlability.Core.Keyboard;
using static Controlability.Input.Keyboard.NativeMethods;

namespace Controlability.Input.Keyboard;

/// <summary>
/// Keeps the keys and mouse buttons that are bound in the active profile away from the game: a <c>WH_KEYBOARD_LL</c> and
/// a <c>WH_MOUSE_LL</c> hook on one thread of their own, with a message loop (low-level hooks are called through it).
/// <para>
/// The callbacks do nothing slow: read two volatile fields, look the key up in a frozen dictionary, return. Windows
/// removes a hook whose callback takes too long. Mouse <i>movement</i> is never touched.
/// </para>
/// <para>
/// A <c>WH_MOUSE_LL</c> hook is called for every mouse move on the whole system, even when it swallows nothing, and a
/// 1000 Hz mouse in a game makes that a context switch per event (visible as FPS drops). So the mouse hook is only
/// installed while it can swallow something: blocking is on and a mouse button is bound.
/// </para>
/// <para>
/// <b>Panic hotkey Ctrl+Alt+Pause</b> is detected inside the keyboard callback itself: it turns blocking off at once,
/// before anyone else (the engine thread may be stuck), and only then tells the owner.
/// </para>
/// </summary>
/// <remarks>
/// Not done in v1: locking the cursor to the window center (ClipCursor/SetCursorPos) for games that read the absolute
/// cursor. Mouse movement is never swallowed, only turned into right-stick movement.
/// </remarks>
public sealed class KeyboardBlocker : IDisposable
{
    private const uint ReinstallMessage = WM_APP + 1;
    private const uint SyncMouseHookMessage = WM_APP + 2;

    private readonly object _lifecycle = new();
    private readonly KeySwallowTracker _tracker = new();
    private readonly PanicHotkeyDetector _panic = new();
    private readonly HookProc _keyboardProc;
    private readonly HookProc _mouseProc;

    private Thread? _thread;
    private uint _threadId;
    private Exception? _startError;
    private nint _keyboardHook;
    private nint _mouseHook;

    private volatile KeyBindings _bindings = KeyBindings.Empty;
    private volatile bool _blocking;
    private volatile bool _panicked;
    private long _lastKeyEventMs;

    public KeyboardBlocker()
    {
        _keyboardProc = KeyboardCallback;
        _mouseProc = MouseCallback;
    }

    /// <summary>Raised on a thread-pool thread, after blocking was switched off, when the panic hotkey was pressed.</summary>
    public event Action? PanicTriggered;

    /// <summary>When the keyboard hook last saw any key event (<see cref="Environment.TickCount64"/>), or when it was installed.</summary>
    public long LastKeyEventMs => Interlocked.Read(ref _lastKeyEventMs);

    public bool IsRunning => _thread is { IsAlive: true };

    /// <summary>True once the panic hotkey fired. Blocking cannot be switched on again until the blocker is restarted.</summary>
    public bool Panicked => _panicked;

    public bool Blocking
    {
        get => _blocking;
        set
        {
            var blocking = value && !_panicked;
            if (_blocking != blocking)
            {
                _blocking = blocking;
                PostSyncMouseHook();
            }
        }
    }

    public void SetBindings(KeyBindings bindings)
    {
        _bindings = bindings;
        PostSyncMouseHook();
    }

    /// <summary>Installs the hooks on a new thread and waits until that worked. Throws if a hook could not be installed.</summary>
    public void Start(KeyBindings bindings)
    {
        lock (_lifecycle)
        {
            if (_thread is not null)
            {
                return;
            }

            _bindings = bindings;
            _blocking = false;
            _panicked = false;
            _tracker.Reset();
            _panic.Reset();
            _startError = null;

            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() => Run(ready))
            {
                IsBackground = true,
                Name = "Controlability.KeyboardHook",
                Priority = ThreadPriority.AboveNormal,
            };
            _thread.Start();
            if (!ready.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("The keyboard hook thread did not start in time.");
            }

            if (_startError is not null)
            {
                var error = _startError;
                _thread.Join(TimeSpan.FromSeconds(2));
                _thread = null;
                throw new InvalidOperationException("The keyboard hook could not be installed: " + error.Message, error);
            }
        }
    }

    /// <summary>Asks the hook thread to remove and install the hooks again (the watchdog's repair).</summary>
    public void Reinstall()
    {
        var id = _threadId;
        if (id != 0)
        {
            PostThreadMessage(id, ReinstallMessage, 0, 0);
        }
    }

    public void Stop()
    {
        Thread? thread;
        uint id;
        lock (_lifecycle)
        {
            thread = _thread;
            id = _threadId;
            _thread = null;
        }

        _blocking = false;
        if (thread is null)
        {
            return;
        }

        if (id != 0)
        {
            PostThreadMessage(id, WM_QUIT, 0, 0);
        }

        thread.Join(TimeSpan.FromSeconds(2));
    }

    public void Dispose() => Stop();

    private void Run(ManualResetEventSlim ready)
    {
        _threadId = GetCurrentThreadId();
        try
        {
            Install();
        }
        catch (Exception ex)
        {
            _startError = ex;
            _threadId = 0;
            ready.Set();
            return;
        }

        ready.Set();

        try
        {
            while (GetMessage(out var message, 0, 0, 0) > 0)
            {
                if (message.message == ReinstallMessage)
                {
                    Uninstall();
                    Install();
                    continue;
                }

                if (message.message == SyncMouseHookMessage)
                {
                    SyncMouseHook();
                    continue;
                }

                TranslateMessage(ref message);
                DispatchMessage(ref message);
            }
        }
        catch (Exception)
        {
            // Falls through to the cleanup; a failed reinstall leaves the keyboard free rather than half-hooked.
        }
        finally
        {
            _blocking = false;
            Uninstall();
            _threadId = 0;
        }
    }

    private void Install()
    {
        var module = GetModuleHandle(null);
        Interlocked.Exchange(ref _lastKeyEventMs, Environment.TickCount64);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        if (_keyboardHook == 0)
        {
            throw new InvalidOperationException($"SetWindowsHookEx(keyboard) failed ({Marshal.GetLastWin32Error()}).");
        }

        _tracker.Reset();
        SyncMouseHook();
    }

    private void PostSyncMouseHook()
    {
        var id = _threadId;
        if (id != 0)
        {
            PostThreadMessage(id, SyncMouseHookMessage, 0, 0);
        }
    }

    /// <summary>Hook thread only: the mouse hook exists exactly while it could swallow a bound button.</summary>
    private void SyncMouseHook()
    {
        var wanted = _blocking && _bindings.BoundMouseButtons != MouseButtons.None;
        if (wanted && _mouseHook == 0)
        {
            // A failure leaves the buttons unswallowed (the pad still works); the next sync tries again.
            _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc, GetModuleHandle(null), 0);
            _tracker.Reset();
        }
        else if (!wanted && _mouseHook != 0)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
            _tracker.Reset();
        }
    }

    private void Uninstall()
    {
        if (_keyboardHook != 0)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = 0;
        }

        if (_mouseHook != 0)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }
    }

    private unsafe nint KeyboardCallback(int code, nuint wParam, nint lParam)
    {
        if (code >= 0)
        {
            var info = (KBDLLHOOKSTRUCT*)lParam;
            var message = (uint)wParam;
            var down = message is WM_KEYDOWN or WM_SYSKEYDOWN;
            var vk = (int)info->vkCode;
            var injected = (info->flags & LLKHF_INJECTED) != 0;

            Interlocked.Exchange(ref _lastKeyEventMs, Environment.TickCount64);

            if (!injected && _panic.OnKey(vk, down))
            {
                // Before anything else: the keyboard is free again right now.
                _blocking = false;
                _panicked = true;
                PostSyncMouseHook();
                ThreadPool.UnsafeQueueUserWorkItem(static state => state.PanicTriggered?.Invoke(), this, preferLocal: false);
            }
            else if (_blocking && _tracker.DecideKey(_bindings, vk, down, injected))
            {
                return 1;
            }
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    private unsafe nint MouseCallback(int code, nuint wParam, nint lParam)
    {
        if (code >= 0 && _blocking)
        {
            var info = (MSLLHOOKSTRUCT*)lParam;
            var message = (uint)wParam;
            var (button, down) = message switch
            {
                WM_LBUTTONDOWN => (MouseButtons.Left, true),
                WM_LBUTTONUP => (MouseButtons.Left, false),
                WM_RBUTTONDOWN => (MouseButtons.Right, true),
                WM_RBUTTONUP => (MouseButtons.Right, false),
                WM_MBUTTONDOWN => (MouseButtons.Middle, true),
                WM_MBUTTONUP => (MouseButtons.Middle, false),
                WM_XBUTTONDOWN => (XButton(info->mouseData), true),
                WM_XBUTTONUP => (XButton(info->mouseData), false),
                _ => (MouseButtons.None, false),
            };

            var injected = (info->flags & LLMHF_INJECTED) != 0;
            if (button != MouseButtons.None && _tracker.DecideMouse(_bindings, button, down, injected))
            {
                return 1;
            }
        }

        return CallNextHookEx(0, code, wParam, lParam);
    }

    /// <summary>The high word of <c>mouseData</c> says which X button: 1 = X1, 2 = X2.</summary>
    private static MouseButtons XButton(uint mouseData) => (mouseData >> 16) switch
    {
        1 => MouseButtons.X1,
        2 => MouseButtons.X2,
        _ => MouseButtons.None,
    };
}
