using Controlability.Core.Keyboard;
using Controlability.Core.Models;

namespace Controlability.Core.Extras;

public sealed record TouchpadMouseSettings
{
    public static readonly TouchpadMouseSettings Off = new();

    public bool Enabled { get; init; }

    /// <summary>1 = sliding a finger across the whole pad moves the cursor <see cref="TouchpadMouseProcessor.PixelsPerPad"/> pixels.</summary>
    public float Sensitivity { get; init; } = 1f;

    /// <summary>0 = none. Each unit adds +100 % speed per pad width per second of finger speed.</summary>
    public float Acceleration { get; init; } = 0.5f;
}

/// <summary>What the touchpad wants the mouse to do this tick: relative pixels, and the buttons that should be down.</summary>
public readonly record struct MouseFrame(int Dx, int Dy, MouseButtons Buttons);

/// <summary>
/// Touchpad to mouse: one finger moves the cursor (relative, with acceleration), the pad click is a left click, a click
/// with two fingers down is a right click. One instance per device.
/// </summary>
public sealed class TouchpadMouseProcessor
{
    public const float PixelsPerPad = 1500f;

    /// <summary>The pad is about twice as wide as it is tall, and Y is normalized separately.</summary>
    private const float VerticalAspect = 0.5f;

    private const float MaxSpeed = 4f;

    private readonly TouchpadMouseSettings _settings;
    private bool _hadFinger;
    private float _lastX;
    private float _lastY;
    private double _lastMs;
    private float _remainderX;
    private float _remainderY;
    private MouseButtons _held;
    private bool _clickWasDown;

    public TouchpadMouseProcessor(TouchpadMouseSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!(settings.Sensitivity > 0f && settings.Acceleration >= 0f))
        {
            throw new ArgumentException("Touchpad sensitivity must be above 0 and acceleration cannot be negative.");
        }

        _settings = settings;
    }

    public MouseFrame Process(in InputState input, double nowMs)
    {
        var fingers = (input.Touch0.Down ? 1 : 0) + (input.Touch1.Down ? 1 : 0);
        var dx = 0;
        var dy = 0;

        if (fingers == 1)
        {
            var finger = input.Touch0.Down ? input.Touch0 : input.Touch1;
            if (_hadFinger)
            {
                (dx, dy) = Move(finger.X - _lastX, finger.Y - _lastY, (nowMs - _lastMs) / 1000.0);
            }

            _hadFinger = true;
            _lastX = finger.X;
            _lastY = finger.Y;
            _lastMs = nowMs;
        }
        else
        {
            // No finger, or two (right-click gesture): no movement, and no jump when one finger comes back.
            _hadFinger = false;
            _remainderX = 0f;
            _remainderY = 0f;
        }

        var click = (input.Buttons & ButtonId.Touchpad) != 0;
        if (click && !_clickWasDown)
        {
            _held = fingers >= 2 ? MouseButtons.Right : MouseButtons.Left;
        }
        else if (!click)
        {
            _held = MouseButtons.None;
        }

        _clickWasDown = click;
        return new MouseFrame(dx, dy, _held);
    }

    private (int X, int Y) Move(float deltaX, float deltaY, double seconds)
    {
        var distance = MathF.Sqrt(deltaX * deltaX + deltaY * deltaY);
        var speed = seconds > 0 ? Math.Min((float)(distance / seconds), MaxSpeed) : 0f;
        var gain = _settings.Sensitivity * PixelsPerPad * (1f + _settings.Acceleration * speed);

        _remainderX += deltaX * gain;
        _remainderY += deltaY * gain * VerticalAspect;
        var x = (int)MathF.Truncate(_remainderX);
        var y = (int)MathF.Truncate(_remainderY);
        _remainderX -= x;
        _remainderY -= y;
        return (x, y);
    }
}
