namespace Controlability.Core.Keyboard;

/// <summary>
/// The mouse as a right stick. Mouse movement is added as a deflection and then decays exponentially back to center.
/// <para>
/// Scaling: each pixel adds <c>sensitivity / decayMs</c>, so a steady speed of <c>v</c> pixels per millisecond settles at
/// a deflection of about <c>v * sensitivity</c>, whatever the decay time or poll rate (1.0 = 1000 px/s for full tilt). The
/// decay time only sets how smooth and how long the tail is. With a decay of 0 the stick follows the speed directly.
/// </para>
/// </summary>
public sealed class MouseStick
{
    /// <summary>Below this the stick snaps to exactly 0, so it really returns to center.</summary>
    public const float Epsilon = 0.001f;

    /// <summary>Longest time step used; a stalled engine must not turn one big delta into a jump.</summary>
    public const double MaxStepMs = 100;

    public float X { get; private set; }

    /// <summary>Up = +1, like every stick in <see cref="Models.InputState"/>; the mouse's screen-down is negated here.</summary>
    public float Y { get; private set; }

    public void Reset()
    {
        X = 0;
        Y = 0;
    }

    /// <param name="dx">Mouse movement in pixels since the last step (right = positive).</param>
    /// <param name="dy">Mouse movement in pixels since the last step (down = positive).</param>
    /// <param name="dtMs">Time since the last step.</param>
    public void Update(float dx, float dy, double dtMs, float sensitivity, int decayMs)
    {
        dtMs = Math.Clamp(dtMs, 0, MaxStepMs);
        if (decayMs <= 0)
        {
            X = dtMs > 0 ? (float)(dx * sensitivity / dtMs) : 0;
            Y = dtMs > 0 ? (float)(-dy * sensitivity / dtMs) : 0;
        }
        else
        {
            var keep = (float)Math.Exp(-dtMs / decayMs);
            X = (X * keep) + (dx * sensitivity / decayMs);
            Y = (Y * keep) - (dy * sensitivity / decayMs);
        }

        var length = MathF.Sqrt((X * X) + (Y * Y));
        if (length > 1f)
        {
            X /= length;
            Y /= length;
        }

        if (MathF.Abs(X) < Epsilon)
        {
            X = 0;
        }

        if (MathF.Abs(Y) < Epsilon)
        {
            Y = 0;
        }
    }
}
