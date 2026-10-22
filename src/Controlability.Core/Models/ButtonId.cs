namespace Controlability.Core.Models;

/// <summary>
/// Normalized, position-based button names (SDL style): South is Cross on PlayStation and A on Xbox.
/// </summary>
[Flags]
public enum ButtonId : uint
{
    None = 0,
    South = 1u << 0,
    East = 1u << 1,
    West = 1u << 2,
    North = 1u << 3,
    Back = 1u << 4,
    Guide = 1u << 5,
    Start = 1u << 6,
    LeftStick = 1u << 7,
    RightStick = 1u << 8,
    LeftShoulder = 1u << 9,
    RightShoulder = 1u << 10,
    DpadUp = 1u << 11,
    DpadDown = 1u << 12,
    DpadLeft = 1u << 13,
    DpadRight = 1u << 14,
    Touchpad = 1u << 15,
    Misc1 = 1u << 16,
    Paddle1 = 1u << 17,
    Paddle2 = 1u << 18,
    Paddle3 = 1u << 19,
    Paddle4 = 1u << 20,
}
