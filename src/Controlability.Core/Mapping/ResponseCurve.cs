namespace Controlability.Core.Mapping;

public enum CurveType
{
    Linear,
    Quadratic,
    Cubic,
    Custom,
}

/// <summary>A control point of a custom curve: input magnitude X and output magnitude Y, both 0..1.</summary>
public readonly record struct CurvePoint(float X, float Y);

/// <summary>
/// Maps a magnitude 0..1 to a magnitude 0..1; the sign of the input is kept.
/// A custom curve is piecewise-linear from (0,0) through up to <see cref="MaxCustomPoints"/> points to (1,1).
/// </summary>
public sealed class ResponseCurve
{
    public const int MaxCustomPoints = 5;

    public static readonly ResponseCurve Linear = new(CurveType.Linear, [], []);
    public static readonly ResponseCurve Quadratic = new(CurveType.Quadratic, [], []);
    public static readonly ResponseCurve Cubic = new(CurveType.Cubic, [], []);

    private readonly float[] _xs;
    private readonly float[] _ys;

    private ResponseCurve(CurveType type, float[] xs, float[] ys)
    {
        Type = type;
        _xs = xs;
        _ys = ys;
    }

    public CurveType Type { get; }

    /// <summary>Creates a custom curve. X must be strictly increasing and inside (0, 1), Y inside [0, 1].</summary>
    public static ResponseCurve Custom(ReadOnlySpan<CurvePoint> points)
    {
        if (points.Length is < 1 or > MaxCustomPoints)
        {
            throw new ArgumentException($"A custom curve needs 1 to {MaxCustomPoints} points.", nameof(points));
        }

        var xs = new float[points.Length];
        var ys = new float[points.Length];
        var previousX = 0f;
        for (var i = 0; i < points.Length; i++)
        {
            var (x, y) = points[i];
            if (!(x > previousX && x < 1f))
            {
                throw new ArgumentException("Point X values must be strictly increasing and inside (0, 1).", nameof(points));
            }

            if (!(y >= 0f && y <= 1f))
            {
                throw new ArgumentException("Point Y values must be inside [0, 1].", nameof(points));
            }

            xs[i] = x;
            ys[i] = y;
            previousX = x;
        }

        return new ResponseCurve(CurveType.Custom, xs, ys);
    }

    public float Apply(float value)
    {
        var shaped = Shape(MathF.Min(MathF.Abs(value), 1f));
        return value < 0f ? -shaped : shaped;
    }

    /// <summary>Shapes the length of the vector and keeps its direction (used with the radial dead zone).</summary>
    public void ApplyRadial(float x, float y, out float outX, out float outY)
    {
        var magnitude = MathF.Sqrt((x * x) + (y * y));
        if (!(magnitude > 0f))
        {
            outX = 0f;
            outY = 0f;
            return;
        }

        var scale = Shape(MathF.Min(magnitude, 1f)) / magnitude;
        outX = x * scale;
        outY = y * scale;
    }

    private float Shape(float t)
    {
        switch (Type)
        {
            case CurveType.Quadratic:
                return t * t;
            case CurveType.Cubic:
                return t * t * t;
            case CurveType.Custom:
                var previousX = 0f;
                var previousY = 0f;
                for (var i = 0; i < _xs.Length; i++)
                {
                    if (t <= _xs[i])
                    {
                        return previousY + ((_ys[i] - previousY) * (t - previousX) / (_xs[i] - previousX));
                    }

                    previousX = _xs[i];
                    previousY = _ys[i];
                }

                return previousY + ((1f - previousY) * (t - previousX) / (1f - previousX));
            default:
                return t;
        }
    }
}
