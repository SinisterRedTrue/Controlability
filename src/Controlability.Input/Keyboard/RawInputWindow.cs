using System.Runtime.InteropServices;
using Controlability.Core.Keyboard;
using Controlability.Core.Models;
using static Controlability.Input.Keyboard.NativeMethods;

namespace Controlability.Input.Keyboard;

/// <param name="Keyboard">Stable id of the physical keyboard (its Raw Input device name).</param>
/// <param name="VirtualKey">Generic virtual-key code (Shift/Ctrl/Alt are not split into left and right).</param>
public readonly record struct RawKeyEvent(DeviceId Keyboard, int VirtualKey, bool Down);

/// <param name="Buttons">Mouse buttons held after this event.</param>
public readonly record struct RawMouseEvent(int DeltaX, int DeltaY, MouseButtons Buttons);

/// <summary>
/// A message-only window on its own STA thread that receives Raw Input for all keyboards and mice, also while another
/// window has the focus (<c>RIDEV_INPUTSINK</c>). It only reports events; it never blocks or changes anything.
/// <para>
/// <see cref="Key"/>, <see cref="Mouse"/> and <see cref="KeyboardArrived"/> / <see cref="KeyboardRemoved"/> are raised on
/// the window thread and must return quickly.
/// </para>
/// </summary>
public sealed class RawInputWindow : IDisposable
{
    private readonly object _lifecycle = new();
    private readonly Dictionary<nint, DeviceId> _names = [];
    private readonly Dictionary<nint, bool> _ignoredMice = [];
    private readonly WndProc _wndProc;
    private readonly string _className = "ControlabilityRawInput_" + Guid.NewGuid().ToString("N");
    private Thread? _thread;
    private nint _hwnd;
    private MouseButtons _buttons;
    private Exception? _startError;

    public RawInputWindow()
    {
        _wndProc = WindowProc;
    }

    public event Action<RawKeyEvent>? Key;

    public event Action<RawMouseEvent>? Mouse;

    public event Action<DeviceInfo>? KeyboardArrived;

    public event Action<DeviceId>? KeyboardRemoved;

    public bool IsRunning => _thread is { IsAlive: true };

    /// <summary>Starts the window thread and waits until Raw Input is registered. Throws if that failed.</summary>
    public void Start()
    {
        lock (_lifecycle)
        {
            if (_thread is not null)
            {
                return;
            }

            using var ready = new ManualResetEventSlim();
            _thread = new Thread(() => Run(ready))
            {
                IsBackground = true,
                Name = "Controlability.RawInput",
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
            if (!ready.Wait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("The Raw Input window did not start in time.");
            }

            if (_startError is not null)
            {
                _thread.Join(TimeSpan.FromSeconds(2));
                _thread = null;
                throw new InvalidOperationException("Raw Input could not be registered: " + _startError.Message, _startError);
            }
        }
    }

    public void Stop()
    {
        Thread? thread;
        nint hwnd;
        lock (_lifecycle)
        {
            thread = _thread;
            hwnd = _hwnd;
            _thread = null;
        }

        if (thread is null)
        {
            return;
        }

        if (hwnd != 0)
        {
            PostMessage(hwnd, WM_CLOSE, 0, 0);
        }

        thread.Join(TimeSpan.FromSeconds(2));
    }

    public void Dispose() => Stop();

    /// <summary>Keyboards that are plugged in right now (Raw Input device list).</summary>
    public static List<DeviceInfo> EnumerateKeyboards()
    {
        var result = new List<DeviceInfo>();
        foreach (var handle in ListDevices(RIM_TYPEKEYBOARD))
        {
            if (NameOf(handle) is { } name)
            {
                result.Add(RawDeviceName.ToDeviceInfo(name));
            }
        }

        return result;
    }

    private void Run(ManualResetEventSlim ready)
    {
        var instance = GetModuleHandle(null);
        var registered = false;
        try
        {
            var windowClass = new WNDCLASSEXW
            {
                cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = instance,
                lpszClassName = _className,
            };
            if (RegisterClassEx(ref windowClass) == 0)
            {
                throw new InvalidOperationException($"RegisterClassEx failed ({Marshal.GetLastWin32Error()}).");
            }

            registered = true;
            _hwnd = CreateWindowEx(0, _className, "Controlability Raw Input", 0, 0, 0, 0, 0, HWND_MESSAGE, 0, instance, 0);
            if (_hwnd == 0)
            {
                throw new InvalidOperationException($"CreateWindowEx failed ({Marshal.GetLastWin32Error()}).");
            }

            // Usage page 1 (generic desktop): usage 6 = keyboard, usage 2 = mouse.
            RAWINPUTDEVICE[] devices =
            [
                new() { usUsagePage = 1, usUsage = 6, dwFlags = RIDEV_INPUTSINK | RIDEV_DEVNOTIFY, hwndTarget = _hwnd },
                new() { usUsagePage = 1, usUsage = 2, dwFlags = RIDEV_INPUTSINK, hwndTarget = _hwnd },
            ];
            if (!RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>()))
            {
                throw new InvalidOperationException($"RegisterRawInputDevices failed ({Marshal.GetLastWin32Error()}).");
            }
        }
        catch (Exception ex)
        {
            _startError = ex;
            ready.Set();
            Cleanup(registered, instance);
            return;
        }

        ready.Set();

        while (GetMessage(out var message, 0, 0, 0) > 0)
        {
            TranslateMessage(ref message);
            DispatchMessage(ref message);
        }

        Cleanup(registered, instance);
    }

    private void Cleanup(bool classRegistered, nint instance)
    {
        // RIDEV_REMOVE needs a null target window.
        RAWINPUTDEVICE[] remove =
        [
            new() { usUsagePage = 1, usUsage = 6, dwFlags = RIDEV_REMOVE, hwndTarget = 0 },
            new() { usUsagePage = 1, usUsage = 2, dwFlags = RIDEV_REMOVE, hwndTarget = 0 },
        ];
        RegisterRawInputDevices(remove, (uint)remove.Length, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());

        if (_hwnd != 0)
        {
            DestroyWindow(_hwnd);
            _hwnd = 0;
        }

        if (classRegistered)
        {
            UnregisterClass(_className, instance);
        }
    }

    private nint WindowProc(nint hwnd, uint message, nuint wParam, nint lParam)
    {
        switch (message)
        {
            case WM_INPUT:
                try
                {
                    HandleInput(lParam);
                }
                catch (Exception)
                {
                    // A subscriber must not be able to kill the window thread.
                }

                // The docs require DefWindowProc for WM_INPUT so the system can clean up (matters for INPUTSINK).
                return DefWindowProc(hwnd, message, wParam, lParam);
            case WM_INPUT_DEVICE_CHANGE:
                HandleDeviceChange(wParam, lParam);
                return 0;
            case WM_CLOSE:
                DestroyWindow(hwnd);
                return 0;
            case WM_DESTROY:
                PostQuitMessage(0);
                return 0;
            default:
                return DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    private unsafe void HandleInput(nint rawInputHandle)
    {
        // Header 24 bytes + the larger of RAWKEYBOARD (16) / RAWMOUSE (24) on x64; 128 leaves room.
        const int BufferSize = 128;
        var buffer = stackalloc byte[BufferSize];
        var size = (uint)BufferSize;
        var headerSize = (uint)sizeof(RAWINPUTHEADER);
        if (GetRawInputData(rawInputHandle, RID_INPUT, buffer, ref size, headerSize) == uint.MaxValue)
        {
            return;
        }

        var header = *(RAWINPUTHEADER*)buffer;
        var data = buffer + headerSize;
        if (header.dwType == RIM_TYPEKEYBOARD)
        {
            var keyboard = *(RAWKEYBOARD*)data;
            if (keyboard.VKey is 0 or 0xFF)
            {
                return; // fake keys the driver sends around extended key sequences
            }

            var id = IdOf(header.hDevice);
            Key?.Invoke(new RawKeyEvent(id, keyboard.VKey, (keyboard.Flags & RI_KEY_BREAK) == 0));
        }
        else if (header.dwType == RIM_TYPEMOUSE)
        {
            if (IsIgnoredMouse(header.hDevice))
            {
                return;
            }

            var mouse = *(RAWMOUSE*)data;
            _buttons = ApplyButtons(_buttons, mouse.usButtonFlags);
            var relative = (mouse.usFlags & MOUSE_MOVE_ABSOLUTE) == 0;
            Mouse?.Invoke(new RawMouseEvent(relative ? mouse.lLastX : 0, relative ? mouse.lLastY : 0, _buttons));
        }
    }

    private void HandleDeviceChange(nuint wParam, nint handle)
    {
        const int Arrival = 1;
        const int Removal = 2;
        if (wParam == Arrival)
        {
            if (ListDevices(RIM_TYPEKEYBOARD).Contains(handle) && NameOf(handle) is { } name)
            {
                _names[handle] = new DeviceId(name);
                KeyboardArrived?.Invoke(RawDeviceName.ToDeviceInfo(name));
            }
        }
        else if (wParam == Removal && _names.Remove(handle, out var id))
        {
            KeyboardRemoved?.Invoke(id);
        }
    }

    /// <summary>Mice that belong to a game controller (its touchpad) are skipped; the answer is cached per device.</summary>
    private bool IsIgnoredMouse(nint handle)
    {
        if (!_ignoredMice.TryGetValue(handle, out var ignored))
        {
            ignored = NameOf(handle) is { } name && RawDeviceName.IsControllerPointer(name);
            _ignoredMice[handle] = ignored;
        }

        return ignored;
    }

    private DeviceId IdOf(nint handle)
    {
        if (!_names.TryGetValue(handle, out var id))
        {
            id = new DeviceId(NameOf(handle) ?? $"keyboard-{handle:X}");
            _names[handle] = id;
        }

        return id;
    }

    internal static MouseButtons ApplyButtons(MouseButtons current, ushort flags)
    {
        (ushort Down, ushort Up, MouseButtons Button)[] map =
        [
            (RI_MOUSE_LEFT_BUTTON_DOWN, RI_MOUSE_LEFT_BUTTON_UP, MouseButtons.Left),
            (RI_MOUSE_RIGHT_BUTTON_DOWN, RI_MOUSE_RIGHT_BUTTON_UP, MouseButtons.Right),
            (RI_MOUSE_MIDDLE_BUTTON_DOWN, RI_MOUSE_MIDDLE_BUTTON_UP, MouseButtons.Middle),
            (RI_MOUSE_BUTTON_4_DOWN, RI_MOUSE_BUTTON_4_UP, MouseButtons.X1),
            (RI_MOUSE_BUTTON_5_DOWN, RI_MOUSE_BUTTON_5_UP, MouseButtons.X2),
        ];
        foreach (var (down, up, button) in map)
        {
            if ((flags & down) != 0)
            {
                current |= button;
            }

            if ((flags & up) != 0)
            {
                current &= ~button;
            }
        }

        return current;
    }

    private static unsafe List<nint> ListDevices(uint type)
    {
        var result = new List<nint>();
        uint count = 0;
        var itemSize = (uint)sizeof(RAWINPUTDEVICELIST);
        if (GetRawInputDeviceList(null, ref count, itemSize) == uint.MaxValue || count == 0)
        {
            return result;
        }

        var list = new RAWINPUTDEVICELIST[count];
        fixed (RAWINPUTDEVICELIST* pointer = list)
        {
            var read = GetRawInputDeviceList(pointer, ref count, itemSize);
            if (read == uint.MaxValue)
            {
                return result;
            }

            for (var i = 0; i < read; i++)
            {
                if (list[i].dwType == type)
                {
                    result.Add(list[i].hDevice);
                }
            }
        }

        return result;
    }

    private static unsafe string? NameOf(nint handle)
    {
        uint chars = 0;
        if (GetRawInputDeviceInfo(handle, RIDI_DEVICENAME, null, ref chars) == uint.MaxValue || chars == 0)
        {
            return null;
        }

        var buffer = new char[chars];
        fixed (char* pointer = buffer)
        {
            var written = GetRawInputDeviceInfo(handle, RIDI_DEVICENAME, pointer, ref chars);
            return written == uint.MaxValue || written == 0 ? null : new string(pointer, 0, (int)written - 1);
        }
    }
}
