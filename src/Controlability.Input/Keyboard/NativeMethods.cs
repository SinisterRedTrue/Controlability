using System.Runtime.InteropServices;

namespace Controlability.Input.Keyboard;

/// <summary>Win32 declarations for Raw Input, the message-only window and the low-level hooks (user32 / kernel32).</summary>
internal static class NativeMethods
{
    public const uint WM_QUIT = 0x0012;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_DESTROY = 0x0002;
    public const uint WM_INPUT_DEVICE_CHANGE = 0x00FE;
    public const uint WM_INPUT = 0x00FF;
    public const uint WM_APP = 0x8000;

    public const uint WM_KEYDOWN = 0x0100;
    public const uint WM_KEYUP = 0x0101;
    public const uint WM_SYSKEYDOWN = 0x0104;
    public const uint WM_SYSKEYUP = 0x0105;
    public const uint WM_LBUTTONDOWN = 0x0201;
    public const uint WM_LBUTTONUP = 0x0202;
    public const uint WM_RBUTTONDOWN = 0x0204;
    public const uint WM_RBUTTONUP = 0x0205;
    public const uint WM_MBUTTONDOWN = 0x0207;
    public const uint WM_MBUTTONUP = 0x0208;
    public const uint WM_XBUTTONDOWN = 0x020B;
    public const uint WM_XBUTTONUP = 0x020C;

    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;
    public const uint LLKHF_INJECTED = 0x10;
    public const uint LLMHF_INJECTED = 0x01;

    public static readonly nint HWND_MESSAGE = -3;

    public const uint RID_INPUT = 0x10000003;
    public const uint RIDI_DEVICENAME = 0x20000007;
    public const uint RIDEV_REMOVE = 0x00000001;
    public const uint RIDEV_INPUTSINK = 0x00000100;
    public const uint RIDEV_DEVNOTIFY = 0x00002000;
    public const uint RIM_TYPEMOUSE = 0;
    public const uint RIM_TYPEKEYBOARD = 1;
    public const ushort RI_KEY_BREAK = 1;
    public const ushort MOUSE_MOVE_ABSOLUTE = 1;

    public const ushort RI_MOUSE_LEFT_BUTTON_DOWN = 0x0001;
    public const ushort RI_MOUSE_LEFT_BUTTON_UP = 0x0002;
    public const ushort RI_MOUSE_RIGHT_BUTTON_DOWN = 0x0004;
    public const ushort RI_MOUSE_RIGHT_BUTTON_UP = 0x0008;
    public const ushort RI_MOUSE_MIDDLE_BUTTON_DOWN = 0x0010;
    public const ushort RI_MOUSE_MIDDLE_BUTTON_UP = 0x0020;
    public const ushort RI_MOUSE_BUTTON_4_DOWN = 0x0040;
    public const ushort RI_MOUSE_BUTTON_4_UP = 0x0080;
    public const ushort RI_MOUSE_BUTTON_5_DOWN = 0x0100;
    public const ushort RI_MOUSE_BUTTON_5_UP = 0x0200;

    public delegate nint WndProc(nint hwnd, uint message, nuint wParam, nint lParam);

    public delegate nint HookProc(int code, nuint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct WNDCLASSEXW
    {
        public uint cbSize;
        public uint style;
        public nint lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public nint hInstance;
        public nint hIcon;
        public nint hCursor;
        public nint hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public nint hIconSm;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSG
    {
        public nint hwnd;
        public uint message;
        public nuint wParam;
        public nint lParam;
        public uint time;
        public POINT pt;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTDEVICE
    {
        public ushort usUsagePage;
        public ushort usUsage;
        public uint dwFlags;
        public nint hwndTarget;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTDEVICELIST
    {
        public nint hDevice;
        public uint dwType;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWINPUTHEADER
    {
        public uint dwType;
        public uint dwSize;
        public nint hDevice;
        public nuint wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RAWKEYBOARD
    {
        public ushort MakeCode;
        public ushort Flags;
        public ushort Reserved;
        public ushort VKey;
        public uint Message;
        public uint ExtraInformation;
    }

    /// <summary>The ULONG button union is flattened into its two USHORT halves (flags, data); the union is 4-byte aligned, hence the padding.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RAWMOUSE
    {
        public ushort usFlags;
        public ushort padding;
        public ushort usButtonFlags;
        public ushort usButtonData;
        public uint ulRawButtons;
        public int lLastX;
        public int lLastY;
        public uint ulExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [DllImport("user32.dll", EntryPoint = "RegisterClassExW", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern ushort RegisterClassEx(ref WNDCLASSEXW windowClass);

    [DllImport("user32.dll", EntryPoint = "UnregisterClassW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnregisterClass(string className, nint instance);

    [DllImport("user32.dll", EntryPoint = "CreateWindowExW", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern nint CreateWindowEx(
        uint exStyle, string className, string? windowName, uint style, int x, int y, int width, int height,
        nint parent, nint menu, nint instance, nint param);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyWindow(nint hwnd);

    [DllImport("user32.dll", EntryPoint = "DefWindowProcW")]
    public static extern nint DefWindowProc(nint hwnd, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "GetMessageW", SetLastError = true)]
    public static extern int GetMessage(out MSG message, nint hwnd, uint filterMin, uint filterMax);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool TranslateMessage(ref MSG message);

    [DllImport("user32.dll", EntryPoint = "DispatchMessageW")]
    public static extern nint DispatchMessage(ref MSG message);

    [DllImport("user32.dll", EntryPoint = "PostMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostMessage(nint hwnd, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "PostThreadMessageW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool PostThreadMessage(uint threadId, uint message, nuint wParam, nint lParam);

    [DllImport("user32.dll")]
    public static extern void PostQuitMessage(int exitCode);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern unsafe uint GetRawInputData(nint rawInput, uint command, void* data, ref uint size, uint headerSize);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern unsafe uint GetRawInputDeviceList(RAWINPUTDEVICELIST* list, ref uint count, uint size);

    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
    public static extern unsafe uint GetRawInputDeviceInfo(nint device, uint command, char* data, ref uint size);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    public static extern nint SetWindowsHookEx(int hookId, HookProc callback, nint module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hook, int code, nuint wParam, nint lParam);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    public static extern nint GetModuleHandle(string? moduleName);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();
}
