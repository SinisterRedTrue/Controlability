using Controlability.Core.Mapping;
using Controlability.Core.Models;

namespace Controlability.Core.Tests;

public class MapperTests
{
    private static readonly MappingSettings PlayStation = MappingSettings.Default with
    {
        Buttons = DefaultMappings.PlayStation,
    };

    [Fact]
    public void NeutralInput_GivesNeutralOutput()
    {
        var state = Mapper.Map(in InputState.Neutral, PlayStation);

        state.Should().Be(XboxState.Neutral);
    }

    [Fact]
    public void NeutralInput_GivesNeutralOutput_WithCustomCurveAndDigitalTriggers()
    {
        var settings = PlayStation with
        {
            LeftStick = new StickSettings(DeadZone.Default, ResponseCurve.Cubic),
            Triggers = new TriggerSettings(Digital: true),
        };

        Mapper.Map(in InputState.Neutral, settings).Should().Be(XboxState.Neutral);
    }

    [Fact]
    public void StickUp_IsPositiveY()
    {
        var input = new InputState { LeftY = 1f, RightY = 1f };

        var state = Mapper.Map(in input, PlayStation);

        state.LeftY.Should().Be(32767);
        state.RightY.Should().Be(32767);
        state.LeftX.Should().Be(0);
        state.RightX.Should().Be(0);
    }

    [Fact]
    public void StickDownLeft_IsNegative()
    {
        var input = new InputState { LeftX = -1f, LeftY = -1f };

        var state = Mapper.Map(in input, PlayStation);

        state.LeftX.Should().BeNegative();
        state.LeftY.Should().BeNegative();
    }

    [Fact]
    public void StickDrift_InsideDeadZone_IsRemoved()
    {
        var input = new InputState { LeftX = 0.05f, LeftY = -0.04f, RightX = 0.03f };

        Mapper.Map(in input, PlayStation).Should().Be(XboxState.Neutral);
    }

    [Fact]
    public void StickAtOuterEdge_ReachesFullScale()
    {
        var input = new InputState { RightX = 0.98f };

        Mapper.Map(in input, PlayStation).RightX.Should().Be(32767);
    }

    [Fact]
    public void LeftAndRightSticks_UseTheirOwnSettings()
    {
        var settings = PlayStation with
        {
            LeftStick = new StickSettings(new DeadZone(DeadZoneMode.Radial, 0.5f, 1f), ResponseCurve.Linear),
        };
        var input = new InputState { LeftX = 0.3f, RightX = 0.3f };

        var state = Mapper.Map(in input, settings);

        state.LeftX.Should().Be(0);
        state.RightX.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AxialStick_UsesPerAxisCurve()
    {
        var settings = PlayStation with
        {
            LeftStick = new StickSettings(new DeadZone(DeadZoneMode.Axial, 0f, 1f), ResponseCurve.Quadratic),
        };
        var input = new InputState { LeftX = 0.5f, LeftY = -0.5f };

        var state = Mapper.Map(in input, settings);

        state.LeftX.Should().Be(StickConverter.ToShort(0.25f));
        state.LeftY.Should().Be(StickConverter.ToShort(-0.25f));
    }

    [Fact]
    public void Triggers_AreMappedToBytes()
    {
        var input = new InputState { LeftTrigger = 1f, RightTrigger = 0.5f };

        var state = Mapper.Map(in input, PlayStation);

        state.LeftTrigger.Should().Be(255);
        state.RightTrigger.Should().BeInRange((byte)124, (byte)128);
    }

    [Fact]
    public void Triggers_InsideDeadZone_AreZero()
    {
        var input = new InputState { LeftTrigger = 0.01f, RightTrigger = 0.02f };

        var state = Mapper.Map(in input, PlayStation);

        state.LeftTrigger.Should().Be(0);
        state.RightTrigger.Should().Be(0);
    }

    [Fact]
    public void DigitalTriggers_AreAllOrNothing()
    {
        var settings = PlayStation with { Triggers = new TriggerSettings(Digital: true) };
        var input = new InputState { LeftTrigger = 0.4f, RightTrigger = 0.6f };

        var state = Mapper.Map(in input, settings);

        state.LeftTrigger.Should().Be(0);
        state.RightTrigger.Should().Be(255);
    }

    [Fact]
    public void Buttons_AreRemapped()
    {
        var input = new InputState { Buttons = ButtonId.South | ButtonId.Start };

        var state = Mapper.Map(in input, PlayStation);

        state.Buttons.Should().Be((ushort)(XboxButton.A | XboxButton.Start));
        state.IsPressed(XboxButton.A).Should().BeTrue();
        state.IsPressed(XboxButton.B).Should().BeFalse();
    }

    [Fact]
    public void Map_DoesNotModifyTheInput()
    {
        var input = new InputState { LeftX = 0.7f, Buttons = ButtonId.North };
        var copy = input;

        _ = Mapper.Map(in input, PlayStation);

        input.Should().Be(copy);
    }

    [Fact]
    public void Map_RejectsNullSettings()
    {
        var act = () => Mapper.Map(in InputState.Neutral, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Map_NaNInput_DoesNotProduceGarbage()
    {
        var input = new InputState { LeftX = float.NaN, LeftY = float.NaN, LeftTrigger = float.NaN };

        var state = Mapper.Map(in input, PlayStation);

        state.Should().Be(XboxState.Neutral);
    }

    [Fact]
    public void Map_DoesNotAllocate()
    {
        var settings = PlayStation with
        {
            LeftStick = new StickSettings(DeadZone.Default, ResponseCurve.Custom([new CurvePoint(0.5f, 0.3f)])),
            RightStick = new StickSettings(new DeadZone(DeadZoneMode.Axial, 0.1f, 0.9f), ResponseCurve.Quadratic),
        };
        var input = new InputState
        {
            LeftX = 0.6f,
            LeftY = -0.4f,
            RightX = 0.9f,
            RightY = 0.2f,
            LeftTrigger = 0.7f,
            RightTrigger = 0.3f,
            Buttons = ButtonId.South | ButtonId.DpadUp | ButtonId.Touchpad,
        };

        var sink = 0;
        for (var i = 0; i < 2_000; i++)
        {
            sink += Mapper.Map(in input, settings).LeftX;
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100_000; i++)
        {
            sink += Mapper.Map(in input, settings).LeftX;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
        sink.Should().NotBe(int.MinValue);
    }
}
