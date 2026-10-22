using System.Numerics;

namespace Controlability.Core.Models;

/// <summary>A touchpad finger: position 0..1 on both axes.</summary>
public readonly record struct TouchPoint(float X, float Y, bool Down);

/// <summary>
/// Normalized state of any input device. Sticks are -1..+1 with up = +1, triggers are 0..1.
/// The all-zero default is the neutral state.
/// </summary>
public struct InputState
{
    public float LeftX;
    public float LeftY;
    public float RightX;
    public float RightY;
    public float LeftTrigger;
    public float RightTrigger;
    public ButtonId Buttons;

    /// <summary>Angular velocity in rad/s; only valid when <see cref="HasGyro"/> is true.</summary>
    public Vector3 Gyro;
    public bool HasGyro;

    public TouchPoint Touch0;
    public TouchPoint Touch1;
    public BatteryInfo Battery;

    /// <summary>Timestamp from <c>IClock.Ticks</c>.</summary>
    public long TimestampTicks;

    public static readonly InputState Neutral = default;
}
