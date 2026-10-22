using Controlability.Core.Mapping;

namespace Controlability.Core.Tests;

public class ResponseCurveTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(1f, 1f)]
    public void Linear_IsIdentity(float input, float expected)
    {
        ResponseCurve.Linear.Apply(input).Should().BeApproximately(expected, 1e-6f);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.25f)]
    [InlineData(1f, 1f)]
    public void Quadratic_Squares(float input, float expected)
    {
        ResponseCurve.Quadratic.Apply(input).Should().BeApproximately(expected, 1e-6f);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.5f, 0.125f)]
    [InlineData(1f, 1f)]
    public void Cubic_Cubes(float input, float expected)
    {
        ResponseCurve.Cubic.Apply(input).Should().BeApproximately(expected, 1e-6f);
    }

    [Theory]
    [InlineData(-0.5f, -0.25f)]
    [InlineData(-1f, -1f)]
    public void Quadratic_KeepsNegativeSign(float input, float expected)
    {
        ResponseCurve.Quadratic.Apply(input).Should().BeApproximately(expected, 1e-6f);
    }

    [Fact]
    public void Cubic_KeepsNegativeSign()
    {
        ResponseCurve.Cubic.Apply(-0.5f).Should().BeApproximately(-0.125f, 1e-6f);
    }

    [Fact]
    public void Apply_ClampsBeyondUnitRange()
    {
        ResponseCurve.Quadratic.Apply(2f).Should().Be(1f);
        ResponseCurve.Quadratic.Apply(-2f).Should().Be(-1f);
    }

    [Fact]
    public void Custom_InterpolatesBetweenPoints()
    {
        var curve = ResponseCurve.Custom([new CurvePoint(0.5f, 0.2f)]);

        curve.Apply(0.25f).Should().BeApproximately(0.1f, 1e-6f);
        curve.Apply(0.5f).Should().BeApproximately(0.2f, 1e-6f);
        curve.Apply(0.75f).Should().BeApproximately(0.6f, 1e-6f);
        curve.Apply(1f).Should().BeApproximately(1f, 1e-6f);
    }

    [Fact]
    public void Custom_WithFivePoints_HitsEveryPoint()
    {
        CurvePoint[] points = [new(0.1f, 0.0f), new(0.3f, 0.1f), new(0.5f, 0.5f), new(0.7f, 0.6f), new(0.9f, 0.95f)];
        var curve = ResponseCurve.Custom(points);

        foreach (var point in points)
        {
            curve.Apply(point.X).Should().BeApproximately(point.Y, 1e-6f);
        }
    }

    [Fact]
    public void Custom_KeepsNegativeSign()
    {
        var curve = ResponseCurve.Custom([new CurvePoint(0.5f, 0.2f)]);

        curve.Apply(-0.5f).Should().BeApproximately(-0.2f, 1e-6f);
    }

    [Fact]
    public void Custom_CopiesItsPoints()
    {
        var points = new[] { new CurvePoint(0.5f, 0.2f) };
        var curve = ResponseCurve.Custom(points);

        points[0] = new CurvePoint(0.5f, 0.9f);

        curve.Apply(0.5f).Should().BeApproximately(0.2f, 1e-6f);
    }

    [Fact]
    public void Custom_RejectsMoreThanFivePoints()
    {
        CurvePoint[] points = [new(0.1f, 0.1f), new(0.2f, 0.2f), new(0.3f, 0.3f), new(0.4f, 0.4f), new(0.5f, 0.5f), new(0.6f, 0.6f)];

        var act = () => ResponseCurve.Custom(points);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Custom_RejectsEmpty()
    {
        var act = () => ResponseCurve.Custom([]);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0.5f, 0.5f, 0.4f, 0.4f)]
    [InlineData(0.0f, 0.5f, 0.4f, 0.4f)]
    [InlineData(1.0f, 0.5f, 0.4f, 0.4f)]
    [InlineData(0.5f, 1.5f, 0.4f, 0.4f)]
    public void Custom_RejectsInvalidPoints(float x1, float y1, float x2, float y2)
    {
        var act = () => ResponseCurve.Custom([new CurvePoint(x1, y1), new CurvePoint(x2, y2)]);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ApplyRadial_KeepsDirectionAndShapesLength()
    {
        ResponseCurve.Quadratic.ApplyRadial(0.3f, 0.4f, out var x, out var y);

        MathF.Sqrt((x * x) + (y * y)).Should().BeApproximately(0.25f, 1e-6f);
        (y / x).Should().BeApproximately(4f / 3f, 1e-5f);
    }

    [Fact]
    public void ApplyRadial_Zero_IsZero()
    {
        ResponseCurve.Cubic.ApplyRadial(0f, 0f, out var x, out var y);

        x.Should().Be(0f);
        y.Should().Be(0f);
    }
}
