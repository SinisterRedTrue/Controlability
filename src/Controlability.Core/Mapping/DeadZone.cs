namespace Controlability.Core.Mapping;

public enum DeadZoneMode
{
    Radial,
    Axial,
}

/// <summary>
/// Stick dead zone with rescale: just outside the inner zone the output is ~0, the outer edge gives 1.0.
/// Radial treats (x, y) as one vector; axial treats each axis on its own.
/// </summary>
public sealed record DeadZone
{
    public static readonly DeadZone Default = new(DeadZoneMode.Radial, 0.08f, 0.98f);

    public DeadZone(DeadZoneMode mode, float inner, float outer)
    {
        if (!(inner >= 0f && inner < 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(inner), inner, "Inner dead zone must be in [0, 1).");
        }

        if (!(outer > inner && outer <= 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(outer), outer, "Outer dead zone must be in (inner, 1].");
        }

        Mode = mode;
        Inner = inner;
        Outer = outer;
    }

    public DeadZoneMode Mode { get; }

    public float Inner { get; }

    public float Outer { get; }

    public void Apply(float x, float y, out float outX, out float outY)
    {
        if (Mode == DeadZoneMode.Axial)
        {
            outX = Rescale(x);
            outY = Rescale(y);
            return;
        }

        var magnitude = MathF.Sqrt((x * x) + (y * y));
        if (!(magnitude > Inner))
        {
            outX = 0f;
            outY = 0f;
            return;
        }

        var scale = ((MathF.Min(magnitude, Outer) - Inner) / (Outer - Inner)) / magnitude;
        outX = x * scale;
        outY = y * scale;
    }

    /// <summary>One-axis dead zone with rescale; keeps the sign.</summary>
    public float Rescale(float value)
    {
        var abs = MathF.Abs(value);
        if (!(abs > Inner))
        {
            return 0f;
        }

        var scaled = (MathF.Min(abs, Outer) - Inner) / (Outer - Inner);
        return value < 0f ? -scaled : scaled;
    }
}
