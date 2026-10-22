using Controlability.Core.Mapping;

namespace Controlability.Core.Tests;

public class DeadZoneTests
{
    private static readonly DeadZone Radial = new(DeadZoneMode.Radial, 0.1f, 0.9f);
    private static readonly DeadZone Axial = new(DeadZoneMode.Axial, 0.1f, 0.9f);

    [Fact]
    public void Radial_Zero_StaysZero()
    {
        Radial.Apply(0f, 0f, out var x, out var y);

        x.Should().Be(0f);
        y.Should().Be(0f);
    }

    [Theory]
    [InlineData(0.05f, 0.0f)]
    [InlineData(0.0f, -0.09f)]
    [InlineData(0.06f, 0.06f)]
    public void Radial_InsideInnerZone_IsZero(float inX, float inY)
    {
        Radial.Apply(inX, inY, out var x, out var y);

        x.Should().Be(0f);
        y.Should().Be(0f);
    }

    [Fact]
    public void Radial_ExactlyOnInnerBoundary_IsZero()
    {
        Radial.Apply(0.1f, 0f, out var x, out var y);

        x.Should().Be(0f);
        y.Should().Be(0f);
    }

    [Fact]
    public void Radial_JustOutsideInnerZone_IsNearZero()
    {
        Radial.Apply(0.101f, 0f, out var x, out _);

        x.Should().BeInRange(0f, 0.005f);
    }

    [Fact]
    public void Radial_MidRange_IsRescaledLinearly()
    {
        Radial.Apply(0.5f, 0f, out var x, out _);

        x.Should().BeApproximately(0.5f, 1e-5f);
    }

    [Theory]
    [InlineData(0.9f)]
    [InlineData(0.95f)]
    [InlineData(1.0f)]
    public void Radial_AtOrBeyondOuterZone_IsFullScale(float input)
    {
        Radial.Apply(input, 0f, out var x, out _);

        x.Should().BeApproximately(1f, 1e-5f);
    }

    [Fact]
    public void Radial_Diagonal_KeepsDirectionAndRescalesMagnitude()
    {
        Radial.Apply(0.5f, 0.5f, out var x, out var y);

        x.Should().BeApproximately(y, 1e-6f);
        MathF.Sqrt((x * x) + (y * y)).Should().BeApproximately((MathF.Sqrt(0.5f) - 0.1f) / 0.8f, 1e-5f);
    }

    [Fact]
    public void Radial_FullDiagonalCorner_IsClampedToUnitCircle()
    {
        Radial.Apply(1f, 1f, out var x, out var y);

        MathF.Sqrt((x * x) + (y * y)).Should().BeApproximately(1f, 1e-5f);
        x.Should().BeApproximately(y, 1e-6f);
    }

    [Fact]
    public void Radial_KeepsSigns()
    {
        Radial.Apply(-0.6f, 0.3f, out var x, out var y);

        x.Should().BeNegative();
        y.Should().BePositive();
    }

    [Fact]
    public void Radial_SmallAxisSurvivesWhenOtherAxisIsLarge()
    {
        Radial.Apply(0.8f, 0.05f, out _, out var y);

        y.Should().BePositive();
    }

    [Fact]
    public void Axial_SmallAxisIsKilledIndependently()
    {
        Axial.Apply(0.5f, 0.05f, out var x, out var y);

        x.Should().BeApproximately(0.5f, 1e-5f);
        y.Should().Be(0f);
    }

    [Theory]
    [InlineData(1.0f, 1.0f)]
    [InlineData(-1.0f, -1.0f)]
    [InlineData(0.9f, 1.0f)]
    [InlineData(-0.5f, -0.5f)]
    public void Axial_Rescale_KeepsSignAndReachesFullScale(float input, float expected)
    {
        Axial.Rescale(input).Should().BeApproximately(expected, 1e-5f);
    }

    [Fact]
    public void Default_UsesPlannedValues()
    {
        DeadZone.Default.Inner.Should().Be(0.08f);
        DeadZone.Default.Outer.Should().Be(0.98f);
        DeadZone.Default.Mode.Should().Be(DeadZoneMode.Radial);
    }

    [Theory]
    [InlineData(-0.1f, 0.9f)]
    [InlineData(1.0f, 1.0f)]
    [InlineData(0.5f, 0.5f)]
    [InlineData(0.2f, 1.1f)]
    [InlineData(float.NaN, 0.9f)]
    public void Constructor_RejectsInvalidRanges(float inner, float outer)
    {
        var act = () => new DeadZone(DeadZoneMode.Radial, inner, outer);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
