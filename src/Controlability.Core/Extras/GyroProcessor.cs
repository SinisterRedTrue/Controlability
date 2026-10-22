using System.Numerics;
using Controlability.Core.Models;

namespace Controlability.Core.Extras;

/// <summary>Turns the angular velocity of a controller into an offset for the right stick. One instance per device.</summary>
public sealed class GyroProcessor
{
    /// <summary>Turn rate that tilts the stick fully at sensitivity 1.</summary>
    public const float FullDeflectionDegPerSec = 180f;

    private const float SmoothingTimeConstantSeconds = 0.1f;

    private readonly GyroSettings _settings;
    private ButtonId _previousButtons;
    private bool _toggledOn;
    private float _x;
    private float _y;

    public GyroProcessor(GyroSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!(settings.SensitivityX >= 0f && settings.SensitivityY >= 0f))
        {
            throw new ArgumentException("Gyro sensitivity cannot be negative.");
        }

        if (!(settings.Smoothing is >= 0f and <= 1f))
        {
            throw new ArgumentException("Gyro smoothing must be between 0 and 1.");
        }

        _settings = settings;
    }

    /// <summary>Is the gyro steering right now (for the given activation mode)? Call <see cref="Update"/> first.</summary>
    public bool IsActive { get; private set; }

    /// <summary>
    /// Computes the stick offset (-1..+1, up = +1) for this tick. <paramref name="deltaSeconds"/> is the time since the
    /// previous call; it only matters for smoothing.
    /// </summary>
    public void Update(in InputState input, float deltaSeconds, out float offsetX, out float offsetY)
    {
        var buttons = input.Buttons;
        var pressed = buttons & ~_previousButtons;
        _previousButtons = buttons;

        IsActive = _settings.Activation switch
        {
            _ when _settings.Button == ButtonId.None => true,
            GyroActivation.WhileHeld => (buttons & _settings.Button) != 0,
            GyroActivation.Toggle => ToggleState((pressed & _settings.Button) != 0),
            _ => true,
        };

        if (!IsActive || !input.HasGyro)
        {
            _x = 0f;
            _y = 0f;
            offsetX = 0f;
            offsetY = 0f;
            return;
        }

        var (targetX, targetY) = ToStick(input.Gyro);
        var tau = _settings.Smoothing * SmoothingTimeConstantSeconds;
        var alpha = tau <= 0f || deltaSeconds <= 0f ? 1f : 1f - MathF.Exp(-deltaSeconds / tau);
        _x += (targetX - _x) * alpha;
        _y += (targetY - _y) * alpha;
        offsetX = Math.Clamp(_x, -1f, 1f);
        offsetY = Math.Clamp(_y, -1f, 1f);
    }

    /// <summary>Adds the offset to a stick axis and clamps to the short range.</summary>
    public static short Add(short axis, float offset)
        => (short)Math.Clamp(axis + (int)MathF.Round(offset * short.MaxValue), short.MinValue, short.MaxValue);

    private bool ToggleState(bool buttonWentDown)
    {
        if (buttonWentDown)
        {
            _toggledOn = !_toggledOn;
        }

        return _toggledOn;
    }

    /// <summary>
    /// Yaw (around the up axis, SDL's Y) steers sideways, pitch (around X) steers up and down. Turning the pad to the
    /// right is a negative yaw rate in SDL's right-handed axes, which is why X is negated.
    /// </summary>
    private (float X, float Y) ToStick(Vector3 gyro)
    {
        const float RadPerSecAtFull = FullDeflectionDegPerSec * MathF.PI / 180f;
        var x = -gyro.Y / RadPerSecAtFull * _settings.SensitivityX * (_settings.InvertX ? -1f : 1f);
        var y = gyro.X / RadPerSecAtFull * _settings.SensitivityY * (_settings.InvertY ? -1f : 1f);
        return (Math.Clamp(x, -1f, 1f), Math.Clamp(y, -1f, 1f));
    }
}
