// ppy.SDL3-CS 2026.1002.1 binding conventions (checked against the package metadata, not guessed):
//  - Namespace `SDL`, one static class `SDL3` holding every function under its C name (SDL_Init, SDL_OpenGamepad, ...).
//    Use `using static SDL.SDL3;`. The XML docs in the package are empty, so signatures were read by reflection.
//  - Booleans are `SDLBool` (implicitly convertible to/from bool). Failure is reported by `false`; call SDL_GetError().
//  - Strings: managed overloads take `Utf8String` (implicit from `string` and `ReadOnlySpan<byte>`) and return `string?`.
//    The raw `Byte*` overloads and `Unsafe_*` getters are not needed.
//  - Hint names are `ReadOnlySpan<byte>` properties (SDL_HINT_XINPUT_ENABLED, ...). Values are plain strings.
//  - Handles are pointers: `SDL_Gamepad*`. Instance ids are the `SDL_JoystickID` struct (uint). Both need unsafe code.
//  - SDL_Init flags are `SDL_InitFlags` (an enum), but the SDL_INIT_* constants are `uint`, so they are cast.
//  - SDL_GetGamepads() returns a disposable `SDLArray<SDL_JoystickID>` (frees the native buffer on Dispose).
//  - SDL_Event is a union struct: read `type`, then the matching member (gdevice, gtouchpad, jbattery, ...).
//  - Capability flags come from properties: SDL_GetBooleanProperty(SDL_GetGamepadProperties(pad), SDL_PROP_GAMEPAD_CAP_*).
//  - SDL3 has no PS4/PS5 rumble hint; SDL_HINT_JOYSTICK_ENHANCED_REPORTS covers rumble/LED/gyro over Bluetooth.
using SDL;
using static SDL.SDL3;

namespace Controlability.Input.Sdl;

/// <summary>
/// Process-wide SDL3 lifetime. Hints are set before the first init; <see cref="Acquire"/> / <see cref="Release"/> are
/// reference counted so SDL is initialised and shut down exactly once. Call them from the thread that polls SDL.
/// </summary>
public static class SdlRuntime
{
    private static readonly object Gate = new();
    private static int _refCount;

    /// <summary>The Xbox 360 pad ViGEm creates (VID/PID 045E:028E), in SDL's ignore-list format.</summary>
    public const string IgnoredDevices = "0x045E/0x028E";

    public static void Acquire()
    {
        lock (Gate)
        {
            if (_refCount == 0)
            {
                ApplyHints();
                var flags = (SDL_InitFlags)(SDL_INIT_GAMEPAD | SDL_INIT_JOYSTICK | SDL_INIT_SENSOR);
                if (!SDL_Init(flags))
                {
                    throw new InvalidOperationException($"SDL_Init failed: {SDL_GetError()}");
                }
            }

            _refCount++;
        }
    }

    public static void Release()
    {
        lock (Gate)
        {
            if (_refCount == 0)
            {
                return;
            }

            _refCount--;
            if (_refCount == 0)
            {
                SDL_Quit();
            }
        }
    }

    private static void ApplyHints()
    {
        Set(SDL_HINT_JOYSTICK_HIDAPI, "1");
        Set(SDL_HINT_JOYSTICK_HIDAPI_PS4, "1");
        Set(SDL_HINT_JOYSTICK_HIDAPI_PS5, "1");
        Set(SDL_HINT_JOYSTICK_HIDAPI_SWITCH, "1");
        Set(SDL_HINT_JOYSTICK_ENHANCED_REPORTS, "1");
        Set(SDL_HINT_JOYSTICK_ALLOW_BACKGROUND_EVENTS, "1");

        // Our own virtual pads are XInput devices (045E:028E): never read them back.
        Set(SDL_HINT_XINPUT_ENABLED, "0");
        Set(SDL_HINT_GAMECONTROLLER_IGNORE_DEVICES, IgnoredDevices);
    }

    private static void Set(ReadOnlySpan<byte> name, string value)
    {
        if (!SDL_SetHint(name, value))
        {
            throw new InvalidOperationException($"SDL_SetHint failed: {SDL_GetError()}");
        }
    }
}
