using Controlability.Core.Models;

namespace Controlability.Core.Mapping;

public static class DefaultMappings
{
    /// <summary>PlayStation layout: Cross = A, Circle = B, Square = X, Triangle = Y, Share/Create = Back, Touchpad = Back.</summary>
    public static readonly ButtonRemapper PlayStation = ButtonRemapper.Create(
        Common(XboxButton.A, XboxButton.B, XboxButton.X, XboxButton.Y)
            .Append(new KeyValuePair<ButtonId, XboxButton>(ButtonId.Touchpad, XboxButton.Back)));

    /// <summary>Nintendo layout by label: A/B and X/Y are swapped against the positions.</summary>
    public static readonly ButtonRemapper Nintendo = ButtonRemapper.Create(
        Common(XboxButton.B, XboxButton.A, XboxButton.Y, XboxButton.X));

    /// <summary>Position-based layout used by Xbox-style and unknown controllers.</summary>
    public static readonly ButtonRemapper Generic = ButtonRemapper.Create(
        Common(XboxButton.A, XboxButton.B, XboxButton.X, XboxButton.Y));

    private static IEnumerable<KeyValuePair<ButtonId, XboxButton>> Common(
        XboxButton south, XboxButton east, XboxButton west, XboxButton north) =>
    [
        new(ButtonId.South, south),
        new(ButtonId.East, east),
        new(ButtonId.West, west),
        new(ButtonId.North, north),
        new(ButtonId.Back, XboxButton.Back),
        new(ButtonId.Guide, XboxButton.Guide),
        new(ButtonId.Start, XboxButton.Start),
        new(ButtonId.LeftStick, XboxButton.LeftThumb),
        new(ButtonId.RightStick, XboxButton.RightThumb),
        new(ButtonId.LeftShoulder, XboxButton.LeftShoulder),
        new(ButtonId.RightShoulder, XboxButton.RightShoulder),
        new(ButtonId.DpadUp, XboxButton.DpadUp),
        new(ButtonId.DpadDown, XboxButton.DpadDown),
        new(ButtonId.DpadLeft, XboxButton.DpadLeft),
        new(ButtonId.DpadRight, XboxButton.DpadRight),
    ];
}
