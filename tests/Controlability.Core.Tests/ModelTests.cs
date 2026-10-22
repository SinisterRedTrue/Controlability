using Controlability.Core.Abstractions;
using Controlability.Core.Models;

namespace Controlability.Core.Tests;

public class ModelTests
{
    [Theory]
    [InlineData(XboxButton.DpadUp, 0x0001)]
    [InlineData(XboxButton.DpadDown, 0x0002)]
    [InlineData(XboxButton.DpadLeft, 0x0004)]
    [InlineData(XboxButton.DpadRight, 0x0008)]
    [InlineData(XboxButton.Start, 0x0010)]
    [InlineData(XboxButton.Back, 0x0020)]
    [InlineData(XboxButton.LeftThumb, 0x0040)]
    [InlineData(XboxButton.RightThumb, 0x0080)]
    [InlineData(XboxButton.LeftShoulder, 0x0100)]
    [InlineData(XboxButton.RightShoulder, 0x0200)]
    [InlineData(XboxButton.Guide, 0x0400)]
    [InlineData(XboxButton.A, 0x1000)]
    [InlineData(XboxButton.B, 0x2000)]
    [InlineData(XboxButton.X, 0x4000)]
    [InlineData(XboxButton.Y, 0x8000)]
    public void XboxButton_HasRealXInputValues(XboxButton button, int value)
    {
        ((int)button).Should().Be(value);
    }

    [Fact]
    public void ButtonId_EveryValueIsADistinctSingleBit()
    {
        var values = Enum.GetValues<ButtonId>().Where(b => b != ButtonId.None).Select(b => (uint)b).ToList();

        values.Should().OnlyHaveUniqueItems();
        values.Should().OnlyContain(v => System.Numerics.BitOperations.IsPow2(v));
        values.Count.Should().Be(21);
    }

    [Fact]
    public void InputState_Neutral_IsAllZero()
    {
        var neutral = InputState.Neutral;

        neutral.LeftX.Should().Be(0f);
        neutral.LeftTrigger.Should().Be(0f);
        neutral.Buttons.Should().Be(ButtonId.None);
        neutral.HasGyro.Should().BeFalse();
    }

    [Fact]
    public void XboxState_Neutral_IsAllZero()
    {
        XboxState.Neutral.Should().Be(default(XboxState));
        XboxState.Neutral.Buttons.Should().Be(0);
    }

    [Fact]
    public void DeviceId_ComparesByValue()
    {
        new DeviceId("054C:09CC:abc").Should().Be(new DeviceId("054C:09CC:abc"));
        new DeviceId("a").Should().NotBe(new DeviceId("b"));
        new DeviceId("abc").ToString().Should().Be("abc");
    }

    [Fact]
    public void DeviceInfo_HoldsItsFields()
    {
        var info = new DeviceInfo(
            new DeviceId("x"),
            "Wireless Controller",
            0x054C,
            0x09CC,
            null,
            "\\\\?\\HID#VID_054C&PID_09CC",
            ConnectionType.Bluetooth,
            ControllerType.PS4,
            DeviceCapabilities.Rumble | DeviceCapabilities.Led);

        info.Capabilities.HasFlag(DeviceCapabilities.Led).Should().BeTrue();
        info.Capabilities.HasFlag(DeviceCapabilities.Gyro).Should().BeFalse();
        info.Type.Should().Be(ControllerType.PS4);
    }

    [Fact]
    public void SlotHandle_InvalidIsNotValid()
    {
        SlotHandle.Invalid.IsValid.Should().BeFalse();
        new SlotHandle(0).IsValid.Should().BeTrue();
    }
}
