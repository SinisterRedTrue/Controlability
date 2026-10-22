using Controlability.Core.Models;
using Controlability.Input.Sdl;
using SDL;

namespace Controlability.Engine.Tests.Input;

public class SdlMappingTests
{
    [Theory]
    [InlineData((short)0, 0f)]
    [InlineData(short.MaxValue, 1f)]
    [InlineData(short.MinValue, -1f)]
    [InlineData((short)16384, 0.5000153f)]
    [InlineData((short)-16384, -0.5f)]
    public void NormalizeAxis_maps_to_unit_range(short raw, float expected)
    {
        SdlMapping.NormalizeAxis(raw).Should().BeApproximately(expected, 1e-5f);
    }

    [Theory]
    [InlineData(short.MaxValue, -1f)]
    [InlineData(short.MinValue, 1f)]
    [InlineData((short)0, 0f)]
    public void NormalizeStickY_inverts_so_up_is_positive(short raw, float expected)
    {
        SdlMapping.NormalizeStickY(raw).Should().BeApproximately(expected, 1e-5f);
    }

    [Theory]
    [InlineData((short)0, 0f)]
    [InlineData(short.MaxValue, 1f)]
    [InlineData((short)-100, 0f)]
    [InlineData(short.MinValue, 0f)]
    public void NormalizeTrigger_maps_to_0_1(short raw, float expected)
    {
        SdlMapping.NormalizeTrigger(raw).Should().BeApproximately(expected, 1e-5f);
    }

    [Fact]
    public void Axis_helpers_stay_in_range_for_every_value()
    {
        for (var raw = short.MinValue; raw < short.MaxValue; raw++)
        {
            SdlMapping.NormalizeAxis(raw).Should().BeInRange(-1f, 1f);
            SdlMapping.NormalizeStickY(raw).Should().BeInRange(-1f, 1f);
            SdlMapping.NormalizeTrigger(raw).Should().BeInRange(0f, 1f);
        }
    }

    [Theory]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH, ButtonId.South)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST, ButtonId.East)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST, ButtonId.West)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH, ButtonId.North)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK, ButtonId.Back)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE, ButtonId.Guide)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START, ButtonId.Start)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK, ButtonId.LeftStick)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK, ButtonId.RightStick)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER, ButtonId.LeftShoulder)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER, ButtonId.RightShoulder)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP, ButtonId.DpadUp)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN, ButtonId.DpadDown)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT, ButtonId.DpadLeft)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT, ButtonId.DpadRight)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_TOUCHPAD, ButtonId.Touchpad)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC1, ButtonId.Misc1)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE1, ButtonId.Paddle1)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE1, ButtonId.Paddle2)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE2, ButtonId.Paddle3)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE2, ButtonId.Paddle4)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC2, ButtonId.None)]
    [InlineData(SDL_GamepadButton.SDL_GAMEPAD_BUTTON_INVALID, ButtonId.None)]
    public void TranslateButton_uses_position_based_names(SDL_GamepadButton button, ButtonId expected)
    {
        SdlMapping.TranslateButton(button).Should().Be(expected);
    }

    [Fact]
    public void ReadableButtons_cover_every_ButtonId_and_each_maps_to_a_distinct_flag()
    {
        var combined = SdlMapping.ReadableButtons.Aggregate(ButtonId.None, (acc, b) => acc | SdlMapping.TranslateButton(b));
        var all = Enum.GetValues<ButtonId>().Aggregate(ButtonId.None, (acc, b) => acc | b);
        combined.Should().Be(all);

        SdlMapping.ReadableButtons.Select(SdlMapping.TranslateButton).Should().OnlyHaveUniqueItems()
            .And.NotContain(ButtonId.None);
    }

    [Theory]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_PS4, ControllerType.PS4)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_PS5, ControllerType.PS5)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO, ControllerType.SwitchPro)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOX360, ControllerType.Xbox)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOXONE, ControllerType.Xbox)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_STANDARD, ControllerType.Generic)]
    [InlineData(SDL_GamepadType.SDL_GAMEPAD_TYPE_UNKNOWN, ControllerType.Generic)]
    public void TranslateType_maps_known_types(SDL_GamepadType type, ControllerType expected)
    {
        SdlMapping.TranslateType(type).Should().Be(expected);
    }

    [Theory]
    [InlineData(SDL_JoystickConnectionState.SDL_JOYSTICK_CONNECTION_WIRED, ConnectionType.Usb)]
    [InlineData(SDL_JoystickConnectionState.SDL_JOYSTICK_CONNECTION_WIRELESS, ConnectionType.Bluetooth)]
    [InlineData(SDL_JoystickConnectionState.SDL_JOYSTICK_CONNECTION_UNKNOWN, ConnectionType.Unknown)]
    [InlineData(SDL_JoystickConnectionState.SDL_JOYSTICK_CONNECTION_INVALID, ConnectionType.Unknown)]
    public void TranslateConnection_maps_wired_and_wireless(SDL_JoystickConnectionState state, ConnectionType expected)
    {
        SdlMapping.TranslateConnection(state).Should().Be(expected);
    }

    [Theory]
    [InlineData(SDL_PowerState.SDL_POWERSTATE_ON_BATTERY, 60, BatteryState.Discharging, 60)]
    [InlineData(SDL_PowerState.SDL_POWERSTATE_CHARGING, 30, BatteryState.Charging, 30)]
    [InlineData(SDL_PowerState.SDL_POWERSTATE_CHARGED, 97, BatteryState.Full, 100)]
    [InlineData(SDL_PowerState.SDL_POWERSTATE_NO_BATTERY, -1, BatteryState.Wired, 0)]
    [InlineData(SDL_PowerState.SDL_POWERSTATE_UNKNOWN, -1, BatteryState.Unknown, 0)]
    [InlineData(SDL_PowerState.SDL_POWERSTATE_ERROR, 50, BatteryState.Unknown, 0)]
    [InlineData(SDL_PowerState.SDL_POWERSTATE_ON_BATTERY, 250, BatteryState.Discharging, 100)]
    [InlineData(SDL_PowerState.SDL_POWERSTATE_ON_BATTERY, -1, BatteryState.Discharging, 0)]
    public void TranslateBattery_maps_state_and_clamps_percent(
        SDL_PowerState state, int percent, BatteryState expectedState, byte expectedPercent)
    {
        SdlMapping.TranslateBattery(state, percent).Should().Be(new BatteryInfo(expectedState, expectedPercent));
    }

    [Fact]
    public void MakeDeviceId_prefers_serial()
    {
        SdlMapping.MakeDeviceId(0x054C, 0x09CC, "dc-0c-2d-2f-97-e0", @"\\?\HID#x").Value
            .Should().Be("dc-0c-2d-2f-97-e0");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void MakeDeviceId_falls_back_to_vid_pid_path(string? serial)
    {
        SdlMapping.MakeDeviceId(0x054C, 0x09CC, serial, @"\\?\HID#x").Value.Should().Be(@"054C:09CC:\\?\HID#x");
    }

    [Theory]
    [InlineData((ushort)0x045E, (ushort)0x028E, true)]
    [InlineData((ushort)0x054C, (ushort)0x09CC, false)]
    [InlineData((ushort)0x045E, (ushort)0x0B12, false)]
    public void IsOwnVirtualPad_matches_only_the_xbox360_ids(ushort vid, ushort pid, bool expected)
    {
        SdlMapping.IsOwnVirtualPad(vid, pid).Should().Be(expected);
    }

    [Theory]
    [InlineData((byte)0, (ushort)0)]
    [InlineData((byte)1, (ushort)257)]
    [InlineData((byte)255, (ushort)65535)]
    public void ScaleRumble_spans_the_full_16_bit_range(byte value, ushort expected)
    {
        SdlMapping.ScaleRumble(value).Should().Be(expected);
    }

    [Fact]
    public void Ignored_devices_hint_names_the_virtual_pad()
    {
        SdlRuntime.IgnoredDevices.Should().Be("0x045E/0x028E");
    }
}
