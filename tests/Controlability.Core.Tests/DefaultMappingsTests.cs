using Controlability.Core.Mapping;
using Controlability.Core.Models;

namespace Controlability.Core.Tests;

public class DefaultMappingsTests
{
    [Theory]
    [InlineData(ButtonId.South, XboxButton.A)]
    [InlineData(ButtonId.East, XboxButton.B)]
    [InlineData(ButtonId.West, XboxButton.X)]
    [InlineData(ButtonId.North, XboxButton.Y)]
    [InlineData(ButtonId.Back, XboxButton.Back)]
    [InlineData(ButtonId.Guide, XboxButton.Guide)]
    [InlineData(ButtonId.Start, XboxButton.Start)]
    [InlineData(ButtonId.LeftStick, XboxButton.LeftThumb)]
    [InlineData(ButtonId.RightStick, XboxButton.RightThumb)]
    [InlineData(ButtonId.LeftShoulder, XboxButton.LeftShoulder)]
    [InlineData(ButtonId.RightShoulder, XboxButton.RightShoulder)]
    [InlineData(ButtonId.DpadUp, XboxButton.DpadUp)]
    [InlineData(ButtonId.DpadDown, XboxButton.DpadDown)]
    [InlineData(ButtonId.DpadLeft, XboxButton.DpadLeft)]
    [InlineData(ButtonId.DpadRight, XboxButton.DpadRight)]
    [InlineData(ButtonId.Touchpad, XboxButton.Back)]
    [InlineData(ButtonId.Misc1, XboxButton.None)]
    [InlineData(ButtonId.Paddle1, XboxButton.None)]
    [InlineData(ButtonId.Paddle2, XboxButton.None)]
    [InlineData(ButtonId.Paddle3, XboxButton.None)]
    [InlineData(ButtonId.Paddle4, XboxButton.None)]
    public void PlayStation_MapsEveryButton(ButtonId source, XboxButton expected)
    {
        DefaultMappings.PlayStation[source].Should().Be(expected);
    }

    [Theory]
    [InlineData(ButtonId.South, XboxButton.A)]
    [InlineData(ButtonId.East, XboxButton.B)]
    [InlineData(ButtonId.West, XboxButton.X)]
    [InlineData(ButtonId.North, XboxButton.Y)]
    [InlineData(ButtonId.Touchpad, XboxButton.None)]
    public void Generic_IsPositionBased(ButtonId source, XboxButton expected)
    {
        DefaultMappings.Generic[source].Should().Be(expected);
    }

    [Theory]
    [InlineData(ButtonId.South, XboxButton.B)]
    [InlineData(ButtonId.East, XboxButton.A)]
    [InlineData(ButtonId.West, XboxButton.Y)]
    [InlineData(ButtonId.North, XboxButton.X)]
    [InlineData(ButtonId.Start, XboxButton.Start)]
    [InlineData(ButtonId.LeftShoulder, XboxButton.LeftShoulder)]
    public void Nintendo_SwapsFaceButtonsByLabel(ButtonId source, XboxButton expected)
    {
        DefaultMappings.Nintendo[source].Should().Be(expected);
    }

    [Fact]
    public void PlayStation_AllButtonsAtOnce_PressesAllMappedXboxButtons()
    {
        var all = ButtonId.South | ButtonId.East | ButtonId.West | ButtonId.North | ButtonId.Back | ButtonId.Guide
            | ButtonId.Start | ButtonId.LeftStick | ButtonId.RightStick | ButtonId.LeftShoulder
            | ButtonId.RightShoulder | ButtonId.DpadUp | ButtonId.DpadDown | ButtonId.DpadLeft | ButtonId.DpadRight
            | ButtonId.Touchpad | ButtonId.Misc1 | ButtonId.Paddle1 | ButtonId.Paddle2 | ButtonId.Paddle3 | ButtonId.Paddle4;

        var result = DefaultMappings.PlayStation.Remap(all);

        result.Should().Be(
            XboxButton.A | XboxButton.B | XboxButton.X | XboxButton.Y | XboxButton.Back | XboxButton.Guide
            | XboxButton.Start | XboxButton.LeftThumb | XboxButton.RightThumb | XboxButton.LeftShoulder
            | XboxButton.RightShoulder | XboxButton.DpadUp | XboxButton.DpadDown | XboxButton.DpadLeft
            | XboxButton.DpadRight);
    }
}
