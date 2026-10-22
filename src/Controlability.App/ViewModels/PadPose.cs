using Controlability.Core.Models;

namespace Controlability.App.ViewModels;

/// <summary>
/// What the on-screen pad shows: sticks -1..+1 (up = +1), triggers 0..1 and the pressed buttons as Xbox bits. A raw
/// controller is shown by button position (South lights the A button), whatever is printed on the real pad.
/// </summary>
public readonly record struct PadPose(
    float LeftX,
    float LeftY,
    float RightX,
    float RightY,
    float LeftTrigger,
    float RightTrigger,
    XboxButton Buttons)
{
    public static PadPose FromRaw(in InputState raw) => new(
        Clamp(raw.LeftX),
        Clamp(raw.LeftY),
        Clamp(raw.RightX),
        Clamp(raw.RightY),
        Math.Clamp(raw.LeftTrigger, 0f, 1f),
        Math.Clamp(raw.RightTrigger, 0f, 1f),
        ButtonsByPosition(raw.Buttons));

    public static PadPose FromXbox(in XboxState state) => new(
        Clamp(state.LeftX / 32767f),
        Clamp(state.LeftY / 32767f),
        Clamp(state.RightX / 32767f),
        Clamp(state.RightY / 32767f),
        state.LeftTrigger / 255f,
        state.RightTrigger / 255f,
        (XboxButton)state.Buttons);

    private static float Clamp(float value) => Math.Clamp(value, -1f, 1f);

    private static XboxButton ButtonsByPosition(ButtonId buttons)
    {
        (ButtonId Source, XboxButton Target)[] table =
        [
            (ButtonId.South, XboxButton.A), (ButtonId.East, XboxButton.B), (ButtonId.West, XboxButton.X), (ButtonId.North, XboxButton.Y),
            (ButtonId.Back, XboxButton.Back), (ButtonId.Start, XboxButton.Start), (ButtonId.Guide, XboxButton.Guide),
            (ButtonId.LeftStick, XboxButton.LeftThumb), (ButtonId.RightStick, XboxButton.RightThumb),
            (ButtonId.LeftShoulder, XboxButton.LeftShoulder), (ButtonId.RightShoulder, XboxButton.RightShoulder),
            (ButtonId.DpadUp, XboxButton.DpadUp), (ButtonId.DpadDown, XboxButton.DpadDown),
            (ButtonId.DpadLeft, XboxButton.DpadLeft), (ButtonId.DpadRight, XboxButton.DpadRight),
        ];

        var result = XboxButton.None;
        foreach (var (source, target) in table)
        {
            if ((buttons & source) != 0)
            {
                result |= target;
            }
        }

        return result;
    }
}
