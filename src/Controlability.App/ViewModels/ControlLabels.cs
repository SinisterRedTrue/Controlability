using Controlability.Core.Keyboard;
using Controlability.Core.Models;

namespace Controlability.App.ViewModels;

/// <summary>Human names for Xbox controls and for the physical buttons of each controller family.</summary>
public static class ControlLabels
{
    public static string Xbox(KeyboardControl control) => control switch
    {
        KeyboardControl.LeftStickUp => "Left stick up",
        KeyboardControl.LeftStickDown => "Left stick down",
        KeyboardControl.LeftStickLeft => "Left stick left",
        KeyboardControl.LeftStickRight => "Left stick right",
        KeyboardControl.RightStickUp => "Right stick up",
        KeyboardControl.RightStickDown => "Right stick down",
        KeyboardControl.RightStickLeft => "Right stick left",
        KeyboardControl.RightStickRight => "Right stick right",
        KeyboardControl.LeftTrigger => "Left trigger (LT)",
        KeyboardControl.RightTrigger => "Right trigger (RT)",
        KeyboardControl.A => "A",
        KeyboardControl.B => "B",
        KeyboardControl.X => "X",
        KeyboardControl.Y => "Y",
        KeyboardControl.Back => "Back / View",
        KeyboardControl.Start => "Start / Menu",
        KeyboardControl.Guide => "Guide (Xbox button)",
        KeyboardControl.LeftThumb => "Left stick click (LS)",
        KeyboardControl.RightThumb => "Right stick click (RS)",
        KeyboardControl.LeftShoulder => "Left bumper (LB)",
        KeyboardControl.RightShoulder => "Right bumper (RB)",
        KeyboardControl.DpadUp => "D-pad up",
        KeyboardControl.DpadDown => "D-pad down",
        KeyboardControl.DpadLeft => "D-pad left",
        KeyboardControl.DpadRight => "D-pad right",
        _ => control.ToString(),
    };

    /// <summary>The label printed on the controller for a position-based button.</summary>
    public static string Physical(ControllerType type, ButtonId button)
    {
        var playStation = type is ControllerType.PS4 or ControllerType.PS5;
        var nintendo = type == ControllerType.SwitchPro;
        return button switch
        {
            ButtonId.South => playStation ? "Cross" : nintendo ? "B" : "A",
            ButtonId.East => playStation ? "Circle" : nintendo ? "A" : "B",
            ButtonId.West => playStation ? "Square" : nintendo ? "Y" : "X",
            ButtonId.North => playStation ? "Triangle" : nintendo ? "X" : "Y",
            ButtonId.Back => playStation ? "Share / Create" : nintendo ? "Minus" : "View",
            ButtonId.Start => playStation ? "Options" : nintendo ? "Plus" : "Menu",
            ButtonId.Guide => playStation ? "PS button" : nintendo ? "Home" : "Xbox button",
            ButtonId.LeftStick => playStation ? "L3" : "Left stick click",
            ButtonId.RightStick => playStation ? "R3" : "Right stick click",
            ButtonId.LeftShoulder => playStation ? "L1" : nintendo ? "L" : "LB",
            ButtonId.RightShoulder => playStation ? "R1" : nintendo ? "R" : "RB",
            ButtonId.DpadUp => "D-pad up",
            ButtonId.DpadDown => "D-pad down",
            ButtonId.DpadLeft => "D-pad left",
            ButtonId.DpadRight => "D-pad right",
            ButtonId.Touchpad => "Touchpad click",
            _ => button.ToString(),
        };
    }
}
