using Controlability.Core.Models;

namespace Controlability.Core.Keyboard;

/// <summary>The Xbox control a key or mouse button drives. Names are the values used in profile <c>keyboard.keys</c>.</summary>
public enum KeyboardControl
{
    None,
    LeftStickUp,
    LeftStickDown,
    LeftStickLeft,
    LeftStickRight,
    RightStickUp,
    RightStickDown,
    RightStickLeft,
    RightStickRight,
    LeftTrigger,
    RightTrigger,
    A,
    B,
    X,
    Y,
    Back,
    Start,
    Guide,
    LeftThumb,
    RightThumb,
    LeftShoulder,
    RightShoulder,
    DpadUp,
    DpadDown,
    DpadLeft,
    DpadRight,
}

public static class KeyboardControls
{
    public static bool TryParse(string name, out KeyboardControl control)
    {
        control = KeyboardControl.None;
        var text = (name ?? "").Trim();
        return text.Length > 0
            && char.IsLetter(text[0])
            && Enum.TryParse(text, ignoreCase: true, out control)
            && control != KeyboardControl.None;
    }

    /// <summary>The normalized button for a button-type control; <see cref="ButtonId.None"/> for sticks and triggers.</summary>
    public static ButtonId ToButton(KeyboardControl control) => control switch
    {
        KeyboardControl.A => ButtonId.South,
        KeyboardControl.B => ButtonId.East,
        KeyboardControl.X => ButtonId.West,
        KeyboardControl.Y => ButtonId.North,
        KeyboardControl.Back => ButtonId.Back,
        KeyboardControl.Start => ButtonId.Start,
        KeyboardControl.Guide => ButtonId.Guide,
        KeyboardControl.LeftThumb => ButtonId.LeftStick,
        KeyboardControl.RightThumb => ButtonId.RightStick,
        KeyboardControl.LeftShoulder => ButtonId.LeftShoulder,
        KeyboardControl.RightShoulder => ButtonId.RightShoulder,
        KeyboardControl.DpadUp => ButtonId.DpadUp,
        KeyboardControl.DpadDown => ButtonId.DpadDown,
        KeyboardControl.DpadLeft => ButtonId.DpadLeft,
        KeyboardControl.DpadRight => ButtonId.DpadRight,
        _ => ButtonId.None,
    };
}
