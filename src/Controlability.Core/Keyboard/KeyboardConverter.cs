using Controlability.Core.Models;

namespace Controlability.Core.Keyboard;

/// <summary>
/// Turns the pressed keys and the mouse of the keyboard pad into an <see cref="InputState"/>. WASD-style keys form a
/// digital stick (diagonals normalized to length 1, opposite keys cancel); the mouse drives the right stick on top of
/// any right-stick keys. Called from the engine thread only.
/// </summary>
public sealed class KeyboardConverter
{
    private static readonly float Diagonal = 1f / MathF.Sqrt(2f);

    private static readonly MouseButtons[] AllMouseButtons =
        [MouseButtons.Left, MouseButtons.Right, MouseButtons.Middle, MouseButtons.X1, MouseButtons.X2];

    private readonly MouseStick _mouse = new();

    public KeyboardConverter(KeyBindings bindings)
    {
        Bindings = bindings;
    }

    public KeyBindings Bindings { get; set; }

    /// <param name="pressedKeys">Currently held keys, as virtual-key codes (any left/right variant).</param>
    /// <param name="mouseDx">Mouse pixels moved since the last call (right = positive).</param>
    /// <param name="mouseDy">Mouse pixels moved since the last call (down = positive).</param>
    /// <param name="dtMs">Time since the last call.</param>
    public InputState Convert(HashSet<int> pressedKeys, MouseButtons mouseButtons, float mouseDx, float mouseDy, double dtMs)
    {
        var bindings = Bindings;
        var state = new InputState();
        var stick = new StickKeys();

        foreach (var key in pressedKeys)
        {
            if (bindings.ControlOf(key) is { } control)
            {
                Apply(control, ref state, ref stick);
            }
        }

        foreach (var button in AllMouseButtons)
        {
            if ((mouseButtons & button) != MouseButtons.None && bindings.ControlOf(button) is { } control)
            {
                Apply(control, ref state, ref stick);
            }
        }

        (state.LeftX, state.LeftY) = stick.Left();
        var (keysX, keysY) = stick.Right();

        _mouse.Update(mouseDx, mouseDy, dtMs, bindings.MouseSensitivity, bindings.DecayMs);
        var rx = keysX + _mouse.X;
        var ry = keysY + _mouse.Y;
        var length = MathF.Sqrt((rx * rx) + (ry * ry));
        if (length > 1f)
        {
            rx /= length;
            ry /= length;
        }

        state.RightX = rx;
        state.RightY = ry;
        return state;
    }

    /// <summary>Forgets the mouse stick (mode switch, panic): nothing stays deflected.</summary>
    public void Reset() => _mouse.Reset();

    private static void Apply(KeyboardControl control, ref InputState state, ref StickKeys stick)
    {
        switch (control)
        {
            case KeyboardControl.LeftStickUp: stick.LeftUp = true; break;
            case KeyboardControl.LeftStickDown: stick.LeftDown = true; break;
            case KeyboardControl.LeftStickLeft: stick.LeftLeft = true; break;
            case KeyboardControl.LeftStickRight: stick.LeftRight = true; break;
            case KeyboardControl.RightStickUp: stick.RightUp = true; break;
            case KeyboardControl.RightStickDown: stick.RightDown = true; break;
            case KeyboardControl.RightStickLeft: stick.RightLeft = true; break;
            case KeyboardControl.RightStickRight: stick.RightRight = true; break;
            case KeyboardControl.LeftTrigger: state.LeftTrigger = 1f; break;
            case KeyboardControl.RightTrigger: state.RightTrigger = 1f; break;
            default: state.Buttons |= KeyboardControls.ToButton(control); break;
        }
    }

    private struct StickKeys
    {
        public bool LeftUp, LeftDown, LeftLeft, LeftRight;
        public bool RightUp, RightDown, RightLeft, RightRight;

        public readonly (float X, float Y) Left() => Axes(LeftUp, LeftDown, LeftLeft, LeftRight);

        public readonly (float X, float Y) Right() => Axes(RightUp, RightDown, RightLeft, RightRight);

        private static (float X, float Y) Axes(bool up, bool down, bool left, bool right)
        {
            var x = (right ? 1f : 0f) - (left ? 1f : 0f);
            var y = (up ? 1f : 0f) - (down ? 1f : 0f);
            return x != 0f && y != 0f ? (x * Diagonal, y * Diagonal) : (x, y);
        }
    }
}
