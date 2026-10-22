using Controlability.Core.Models;
using SDL;

namespace Controlability.Input.Sdl;

/// <summary>Pure translation helpers between SDL3 values and the normalized Core model (no SDL calls).</summary>
public static class SdlMapping
{
    private const float AxisMax = 32767f;
    private const float AxisMin = 32768f;

    /// <summary>SDL axis (-32768..32767) to -1..+1 with the same direction.</summary>
    public static float NormalizeAxis(short raw)
    {
        var value = raw >= 0 ? raw / AxisMax : raw / AxisMin;
        return Math.Clamp(value, -1f, 1f);
    }

    /// <summary>SDL Y axis is down-positive; Core is up-positive, so the sign is inverted.</summary>
    public static float NormalizeStickY(short raw) => -NormalizeAxis(raw);

    /// <summary>SDL trigger (0..32767, negative values from bad drivers count as 0) to 0..1.</summary>
    public static float NormalizeTrigger(short raw) => raw <= 0 ? 0f : Math.Min(raw / AxisMax, 1f);

    /// <summary>SDL button to Core button; <see cref="ButtonId.None"/> for buttons Core does not model.</summary>
    public static ButtonId TranslateButton(SDL_GamepadButton button) => button switch
    {
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH => ButtonId.South,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST => ButtonId.East,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST => ButtonId.West,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH => ButtonId.North,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK => ButtonId.Back,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE => ButtonId.Guide,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START => ButtonId.Start,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK => ButtonId.LeftStick,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK => ButtonId.RightStick,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER => ButtonId.LeftShoulder,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER => ButtonId.RightShoulder,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP => ButtonId.DpadUp,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN => ButtonId.DpadDown,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT => ButtonId.DpadLeft,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT => ButtonId.DpadRight,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_TOUCHPAD => ButtonId.Touchpad,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC1 => ButtonId.Misc1,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE1 => ButtonId.Paddle1,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE1 => ButtonId.Paddle2,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE2 => ButtonId.Paddle3,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE2 => ButtonId.Paddle4,
        _ => ButtonId.None,
    };

    /// <summary>Every SDL button we read each poll, so the table lives in one place.</summary>
    public static readonly SDL_GamepadButton[] ReadableButtons =
    [
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_SOUTH,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_EAST,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_WEST,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_NORTH,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_BACK,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_GUIDE,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_START,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_STICK,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_STICK,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_SHOULDER,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_SHOULDER,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_UP,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_DOWN,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_LEFT,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_DPAD_RIGHT,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_TOUCHPAD,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_MISC1,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE1,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE1,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_RIGHT_PADDLE2,
        SDL_GamepadButton.SDL_GAMEPAD_BUTTON_LEFT_PADDLE2,
    ];

    public static ControllerType TranslateType(SDL_GamepadType type) => type switch
    {
        SDL_GamepadType.SDL_GAMEPAD_TYPE_PS4 => ControllerType.PS4,
        SDL_GamepadType.SDL_GAMEPAD_TYPE_PS5 => ControllerType.PS5,
        SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_PRO
            or SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_LEFT
            or SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_RIGHT
            or SDL_GamepadType.SDL_GAMEPAD_TYPE_NINTENDO_SWITCH_JOYCON_PAIR => ControllerType.SwitchPro,
        SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOX360
            or SDL_GamepadType.SDL_GAMEPAD_TYPE_XBOXONE => ControllerType.Xbox,
        _ => ControllerType.Generic,
    };

    public static ConnectionType TranslateConnection(SDL_JoystickConnectionState state) => state switch
    {
        SDL_JoystickConnectionState.SDL_JOYSTICK_CONNECTION_WIRED => ConnectionType.Usb,
        SDL_JoystickConnectionState.SDL_JOYSTICK_CONNECTION_WIRELESS => ConnectionType.Bluetooth,
        _ => ConnectionType.Unknown,
    };

    /// <summary>SDL reports percent as -1 when unknown; that becomes 0 with <see cref="BatteryState.Unknown"/>.</summary>
    public static BatteryInfo TranslateBattery(SDL_PowerState state, int percent)
    {
        var clamped = (byte)Math.Clamp(percent, 0, 100);
        return state switch
        {
            SDL_PowerState.SDL_POWERSTATE_ON_BATTERY => new BatteryInfo(BatteryState.Discharging, clamped),
            SDL_PowerState.SDL_POWERSTATE_CHARGING => new BatteryInfo(BatteryState.Charging, clamped),
            SDL_PowerState.SDL_POWERSTATE_CHARGED => new BatteryInfo(BatteryState.Full, 100),
            SDL_PowerState.SDL_POWERSTATE_NO_BATTERY => new BatteryInfo(BatteryState.Wired, 0),
            _ => new BatteryInfo(BatteryState.Unknown, 0),
        };
    }

    /// <summary>Stable device identity: serial if available, otherwise <c>VID:PID:path</c>.</summary>
    public static DeviceId MakeDeviceId(ushort vid, ushort pid, string? serial, string? path)
    {
        if (!string.IsNullOrWhiteSpace(serial))
        {
            return new DeviceId(serial);
        }

        return new DeviceId($"{vid:X4}:{pid:X4}:{path}");
    }

    /// <summary>True for the Xbox 360 pad ViGEm creates; we must never read our own virtual pads.</summary>
    public static bool IsOwnVirtualPad(ushort vid, ushort pid) => vid == 0x045E && pid == 0x028E;

    /// <summary>Core 0..255 motor byte to SDL's 0..65535 range (255 maps to 65535).</summary>
    public static ushort ScaleRumble(byte value) => (ushort)(value * 257);
}
