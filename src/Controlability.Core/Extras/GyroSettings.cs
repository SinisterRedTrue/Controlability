using Controlability.Core.Models;

namespace Controlability.Core.Extras;

public enum GyroActivation
{
    /// <summary>The gyro steers all the time.</summary>
    Always,

    /// <summary>The gyro steers while the chosen button is held.</summary>
    WhileHeld,

    /// <summary>The chosen button switches the gyro on and off.</summary>
    Toggle,
}

/// <summary>
/// Gyro to right stick. Sensitivity 1 means a turn rate of <see cref="GyroProcessor.FullDeflectionDegPerSec"/> tilts
/// the stick fully.
/// </summary>
public sealed record GyroSettings
{
    public static readonly GyroSettings Off = new();

    public bool Enabled { get; init; }

    public float SensitivityX { get; init; } = 1f;

    public float SensitivityY { get; init; } = 1f;

    public bool InvertX { get; init; }

    public bool InvertY { get; init; }

    /// <summary>0 = raw, 1 = very smooth (time constant 100 ms).</summary>
    public float Smoothing { get; init; } = 0.3f;

    public GyroActivation Activation { get; init; } = GyroActivation.Always;

    /// <summary>The button for <see cref="GyroActivation.WhileHeld"/> and <see cref="GyroActivation.Toggle"/>; None counts as always on.</summary>
    public ButtonId Button { get; init; }
}
