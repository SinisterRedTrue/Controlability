using System.Runtime.InteropServices;
using Controlability.Core.Abstractions;
using Controlability.Core.Keyboard;

namespace Controlability.Input.Mouse;

/// <summary>Moves the cursor and clicks with <c>SendInput</c>. Thread-safe; each call is one Win32 call.</summary>
public sealed class SendInputMouse : IMouseInjector
{
    private const uint InputMouse = 0;
    private const uint MouseEventMove = 0x0001;
    private const uint MouseEventLeftDown = 0x0002;
    private const uint MouseEventLeftUp = 0x0004;
    private const uint MouseEventRightDown = 0x0008;
    private const uint MouseEventRightUp = 0x0010;
    private const uint MouseEventMiddleDown = 0x0020;
    private const uint MouseEventMiddleUp = 0x0040;

    public void Move(int dx, int dy)
    {
        if (dx != 0 || dy != 0)
        {
            Send(MouseEventMove, dx, dy);
        }
    }

    public void SetButton(MouseButtons button, bool down)
    {
        var flags = button switch
        {
            MouseButtons.Left => down ? MouseEventLeftDown : MouseEventLeftUp,
            MouseButtons.Right => down ? MouseEventRightDown : MouseEventRightUp,
            MouseButtons.Middle => down ? MouseEventMiddleDown : MouseEventMiddleUp,
            _ => 0u,
        };

        if (flags != 0)
        {
            Send(flags, 0, 0);
        }
    }

    private static void Send(uint flags, int dx, int dy)
    {
        var input = new Input
        {
            Type = InputMouse,
            Mouse = new MouseInput { Dx = dx, Dy = dy, Flags = flags },
        };

        // A failed call (for example while the secure desktop is up) just drops this movement.
        _ = SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nuint ExtraInfo;
    }

    /// <summary>INPUT with its union reduced to the mouse member, which is the largest; the layout is the x64 one (40 bytes).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public MouseInput Mouse;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
