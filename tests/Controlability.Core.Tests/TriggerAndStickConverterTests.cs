using Controlability.Core.Mapping;

namespace Controlability.Core.Tests;

public class TriggerProcessorTests
{
    private static readonly TriggerSettings Analog = new(DeadZone: 0.1f);
    private static readonly TriggerSettings Digital = new(Digital: true);

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.05f, 0f)]
    [InlineData(0.1f, 0f)]
    [InlineData(0.55f, 0.5f)]
    [InlineData(1f, 1f)]
    [InlineData(1.5f, 1f)]
    [InlineData(-0.2f, 0f)]
    public void Analog_AppliesDeadZoneAndRescale(float input, float expected)
    {
        TriggerProcessor.Process(input, Analog).Should().BeApproximately(expected, 1e-5f);
    }

    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.49f, 0f)]
    [InlineData(0.5f, 1f)]
    [InlineData(0.51f, 1f)]
    [InlineData(1f, 1f)]
    public void Digital_SwitchesAtHalf(float input, float expected)
    {
        TriggerProcessor.Process(input, Digital).Should().Be(expected);
    }

    [Fact]
    public void Digital_HonoursCustomThreshold()
    {
        var settings = new TriggerSettings(Digital: true, DigitalThreshold: 0.8f);

        TriggerProcessor.Process(0.7f, settings).Should().Be(0f);
        TriggerProcessor.Process(0.8f, settings).Should().Be(1f);
    }

    [Fact]
    public void Process_NaN_IsZero()
    {
        TriggerProcessor.Process(float.NaN, Analog).Should().Be(0f);
    }

    [Fact]
    public void Process_DeadZoneOfOne_DoesNotDivideByZero()
    {
        var settings = new TriggerSettings(DeadZone: 1f);

        TriggerProcessor.Process(1f, settings).Should().Be(0f);
    }

    [Theory]
    [InlineData(0f, 0)]
    [InlineData(0.5f, 128)]
    [InlineData(1f, 255)]
    [InlineData(2f, 255)]
    [InlineData(-1f, 0)]
    public void ToByte_ScalesAndClamps(float input, int expected)
    {
        TriggerProcessor.ToByte(input).Should().Be((byte)expected);
    }
}

public class StickConverterTests
{
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 32767)]
    [InlineData(-1f, -32767)]
    [InlineData(0.5f, 16384)]
    [InlineData(-0.5f, -16384)]
    public void ToShort_ConvertsKnownValues(float input, int expected)
    {
        StickConverter.ToShort(input).Should().Be((short)expected);
    }

    [Theory]
    [InlineData(1.5f, 32767)]
    [InlineData(100f, 32767)]
    [InlineData(-2f, -32768)]
    [InlineData(-100f, -32768)]
    public void ToShort_ClampsBeyondRange(float input, int expected)
    {
        StickConverter.ToShort(input).Should().Be((short)expected);
    }

    [Theory]
    [InlineData(float.NaN)]
    public void ToShort_NaN_IsZero(float input)
    {
        StickConverter.ToShort(input).Should().Be(0);
    }

    [Theory]
    [InlineData(float.PositiveInfinity, 32767)]
    [InlineData(float.NegativeInfinity, -32768)]
    public void ToShort_Infinity_Clamps(float input, int expected)
    {
        StickConverter.ToShort(input).Should().Be((short)expected);
    }

    [Fact]
    public void ToShort_UpIsPositive()
    {
        StickConverter.ToShort(0.25f).Should().BePositive();
    }
}
