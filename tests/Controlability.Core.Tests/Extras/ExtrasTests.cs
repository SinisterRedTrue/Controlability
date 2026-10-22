using System.Numerics;
using Controlability.Core.Abstractions;
using Controlability.Core.Extras;
using Controlability.Core.Keyboard;
using Controlability.Core.Models;

namespace Controlability.Core.Tests.Extras;

internal sealed class FakeClock : IClock
{
    public long Ticks { get; private set; }

    public double ToMs(long ticks) => ticks / 1000.0;

    public void Advance(double ms) => Ticks += (long)(ms * 1000);
}

public sealed class GyroProcessorTests
{
    private const float QuarterTurn = 90f * MathF.PI / 180f;

    private static InputState Turning(float yaw = 0, float pitch = 0, ButtonId buttons = ButtonId.None)
        => new() { HasGyro = true, Gyro = new Vector3(pitch, yaw, 0), Buttons = buttons };

    private static GyroSettings Raw(Action<GyroSettingsBuilder>? tweak = null)
    {
        var builder = new GyroSettingsBuilder();
        tweak?.Invoke(builder);
        return builder.Build();
    }

    private sealed class GyroSettingsBuilder
    {
        public float SensitivityX = 1f;
        public float SensitivityY = 1f;
        public bool InvertX;
        public bool InvertY;
        public GyroActivation Activation = GyroActivation.Always;
        public ButtonId Button;

        public GyroSettings Build() => new()
        {
            Enabled = true,
            SensitivityX = SensitivityX,
            SensitivityY = SensitivityY,
            InvertX = InvertX,
            InvertY = InvertY,
            Smoothing = 0f,
            Activation = Activation,
            Button = Button,
        };
    }

    [Fact]
    public void Scaling_HalfOfFullDeflectionRateGivesHalfDeflection()
    {
        var gyro = new GyroProcessor(Raw());
        var input = Turning(yaw: -QuarterTurn);

        gyro.Update(in input, 0.001f, out var x, out var y);

        x.Should().BeApproximately(0.5f, 1e-4f, "turning right (negative yaw) pushes the stick right");
        y.Should().Be(0f);
    }

    [Fact]
    public void Scaling_UsesTheSensitivityOfEachAxis()
    {
        var gyro = new GyroProcessor(Raw(b => { b.SensitivityX = 0.5f; b.SensitivityY = 2f; }));
        var input = Turning(yaw: -QuarterTurn, pitch: QuarterTurn / 2);

        gyro.Update(in input, 0.001f, out var x, out var y);

        x.Should().BeApproximately(0.25f, 1e-4f);
        y.Should().BeApproximately(0.5f, 1e-4f);
    }

    [Fact]
    public void Invert_FlipsTheAxis()
    {
        var gyro = new GyroProcessor(Raw(b => { b.InvertX = true; b.InvertY = true; }));
        var input = Turning(yaw: -QuarterTurn, pitch: QuarterTurn);

        gyro.Update(in input, 0.001f, out var x, out var y);

        x.Should().BeApproximately(-0.5f, 1e-4f);
        y.Should().BeApproximately(-0.5f, 1e-4f);
    }

    [Fact]
    public void Output_IsClampedToFullDeflection()
    {
        var gyro = new GyroProcessor(Raw(b => b.SensitivityX = 10f));
        var input = Turning(yaw: -10f);

        gyro.Update(in input, 0.001f, out var x, out _);

        x.Should().Be(1f);
    }

    [Theory]
    [InlineData(short.MaxValue, 1f, short.MaxValue)]
    [InlineData(short.MinValue, -1f, short.MinValue)]
    [InlineData(20000, 0.5f, short.MaxValue)]
    [InlineData(-10000, 0.5f, 6384)]
    [InlineData(0, 0f, 0)]
    public void Add_ClampsToTheShortRange(short axis, float offset, short expected)
        => GyroProcessor.Add(axis, offset).Should().Be(expected);

    [Fact]
    public void Smoothing_EasesTowardsTheTargetInsteadOfJumping()
    {
        var gyro = new GyroProcessor(Raw() with { Smoothing = 1f });
        var input = Turning(yaw: -QuarterTurn * 2);

        gyro.Update(in input, 0.01f, out var first, out _);
        gyro.Update(in input, 0.01f, out var second, out _);
        for (var i = 0; i < 200; i++)
        {
            gyro.Update(in input, 0.01f, out _, out _);
        }

        gyro.Update(in input, 0.01f, out var settled, out _);

        first.Should().BeInRange(0.05f, 0.2f);
        second.Should().BeGreaterThan(first);
        settled.Should().BeApproximately(1f, 1e-3f);
    }

    [Fact]
    public void WithoutGyroData_TheOffsetIsZero()
    {
        var gyro = new GyroProcessor(Raw());
        var input = new InputState { HasGyro = false, Gyro = new Vector3(5, 5, 5) };

        gyro.Update(in input, 0.001f, out var x, out var y);

        (x, y).Should().Be((0f, 0f));
    }

    [Fact]
    public void WhileHeld_SteersOnlyWhileTheButtonIsDown()
    {
        var gyro = new GyroProcessor(Raw(b => { b.Activation = GyroActivation.WhileHeld; b.Button = ButtonId.LeftShoulder; }));

        var idle = Turning(yaw: -QuarterTurn);
        gyro.Update(in idle, 0.001f, out var released, out _);
        var held = Turning(yaw: -QuarterTurn, buttons: ButtonId.LeftShoulder);
        gyro.Update(in held, 0.001f, out var down, out _);
        gyro.Update(in idle, 0.001f, out var releasedAgain, out _);

        released.Should().Be(0f);
        down.Should().BeApproximately(0.5f, 1e-4f);
        releasedAgain.Should().Be(0f);
    }

    [Fact]
    public void Toggle_SwitchesOnAndOffWithEachPress_AndStaysWhenReleased()
    {
        var gyro = new GyroProcessor(Raw(b => { b.Activation = GyroActivation.Toggle; b.Button = ButtonId.RightShoulder; }));
        float Step(ButtonId buttons)
        {
            var input = Turning(yaw: -QuarterTurn, buttons: buttons);
            gyro.Update(in input, 0.001f, out var x, out _);
            return x;
        }

        Step(ButtonId.None).Should().Be(0f, "starts off");
        Step(ButtonId.RightShoulder).Should().BeApproximately(0.5f, 1e-4f, "first press switches on");
        Step(ButtonId.RightShoulder).Should().BeApproximately(0.5f, 1e-4f, "holding does not flip again");
        Step(ButtonId.None).Should().BeApproximately(0.5f, 1e-4f, "stays on after release");
        Step(ButtonId.RightShoulder).Should().Be(0f, "second press switches off");
        Step(ButtonId.None).Should().Be(0f);
    }

    [Fact]
    public void ActivationWithoutAButton_CountsAsAlwaysOn()
    {
        var gyro = new GyroProcessor(Raw(b => b.Activation = GyroActivation.WhileHeld));
        var input = Turning(yaw: -QuarterTurn);

        gyro.Update(in input, 0.001f, out var x, out _);

        x.Should().BeApproximately(0.5f, 1e-4f);
    }

    [Fact]
    public void InvalidSettings_AreRejected()
    {
        FluentActions.Invoking(() => new GyroProcessor(Raw() with { Smoothing = 1.5f })).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new GyroProcessor(Raw() with { SensitivityX = -1f })).Should().Throw<ArgumentException>();
    }
}

public sealed class TurboAndToggleTests
{
    private const ushort A = (ushort)XboxButton.A;
    private const ushort B = (ushort)XboxButton.B;

    private static TurboProcessor Turbo(int hz) => new(new Dictionary<XboxButton, int> { [XboxButton.A] = hz });

    [Fact]
    public void Turbo_PressesAndReleasesAtTheConfiguredRate()
    {
        var clock = new FakeClock();
        var turbo = Turbo(10);
        var samples = new List<bool>();

        // 10 Hz = 100 ms period, 50 ms on / 50 ms off, sampled every 25 ms for 300 ms.
        for (var i = 0; i < 12; i++)
        {
            samples.Add((turbo.Process(A, clock.ToMs(clock.Ticks)) & A) != 0);
            clock.Advance(25);
        }

        samples.Should().Equal(true, true, false, false, true, true, false, false, true, true, false, false);
    }

    [Fact]
    public void Turbo_ChangesOnlyTheConfiguredButton()
    {
        var turbo = Turbo(10);

        turbo.Process(A | B, 0);
        var off = turbo.Process(A | B, 60);

        off.Should().Be(B, "A is in its off phase, B has no turbo");
    }

    [Fact]
    public void Turbo_RestartsWithAnOnPhaseOnEveryNewPress()
    {
        var turbo = Turbo(10);

        turbo.Process(A, 0);
        turbo.Process(0, 80);
        var again = turbo.Process(A, 90);

        (again & A).Should().NotBe(0, "a new press starts in the on phase, whatever the clock says");
    }

    [Fact]
    public void Turbo_RejectsRatesOutsideTheRange()
    {
        FluentActions.Invoking(() => Turbo(0)).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => Turbo(TurboProcessor.MaxHz + 1)).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Toggle_HoldsAfterOnePress_AndReleasesAfterTheNext()
    {
        var toggle = new ToggleProcessor([XboxButton.A]);

        toggle.Process(A).Should().Be(A, "pressed");
        toggle.Process(0).Should().Be(A, "released but latched");
        toggle.Process(A).Should().Be(0, "second press lets go");
        toggle.Process(0).Should().Be(0);
        toggle.Process(A).Should().Be(A, "third press latches again");
    }

    [Fact]
    public void Toggle_PassesOtherButtonsThrough()
    {
        var toggle = new ToggleProcessor([XboxButton.A]);

        toggle.Process(A | B).Should().Be(A | B);
        toggle.Process(B).Should().Be(A | B, "A stays held, B follows the finger");
        toggle.Process(0).Should().Be(A);
    }

    [Fact]
    public void ToggleThenTurbo_PulsesAHeldLatch()
    {
        var clock = new FakeClock();
        var extras = new ExtrasProcessor(
            new ExtrasSettings(
                GyroSettings.Off,
                TouchpadMouseSettings.Off,
                new Dictionary<XboxButton, int> { [XboxButton.A] = 10 },
                [XboxButton.A]),
            clock);
        var input = new InputState();
        var mapped = new XboxState { Buttons = A };
        extras.Apply(in input, ref mapped);
        mapped.Buttons.Should().Be(A, "press latches and starts the turbo in its on phase");

        clock.Advance(60);
        mapped = new XboxState { Buttons = 0 };
        extras.Apply(in input, ref mapped);
        mapped.Buttons.Should().Be(0, "latched, but 60 ms into a 100 ms period is the off half");

        clock.Advance(50);
        mapped = new XboxState { Buttons = 0 };
        extras.Apply(in input, ref mapped);
        mapped.Buttons.Should().Be(A, "the next period starts on");
    }
}

public sealed class ExtrasProcessorTests
{
    [Fact]
    public void Gyro_IsAddedToTheMappedRightStick_AndClamped()
    {
        var clock = new FakeClock();
        var extras = new ExtrasProcessor(
            ExtrasSettings.None with { Gyro = new GyroSettings { Enabled = true, Smoothing = 0f, SensitivityX = 4f } },
            clock);
        var input = new InputState { HasGyro = true, Gyro = new Vector3(0, -MathF.PI, 0) };
        var mapped = new XboxState { RightX = 20000, RightY = -5 };

        extras.Apply(in input, ref mapped);

        mapped.RightX.Should().Be(short.MaxValue);
        mapped.RightY.Should().Be(-5);
    }

    [Fact]
    public void Prepare_MasksTheTouchpadClickOnlyWhileTheTouchpadIsAMouse()
    {
        var clock = new FakeClock();
        var input = new InputState { Buttons = ButtonId.Touchpad | ButtonId.South };

        new ExtrasProcessor(ExtrasSettings.None, clock).Prepare(in input).Buttons.Should().Be(ButtonId.Touchpad | ButtonId.South);

        var mouse = new ExtrasProcessor(ExtrasSettings.None with { Touchpad = new TouchpadMouseSettings { Enabled = true } }, clock);
        mouse.Prepare(in input).Buttons.Should().Be(ButtonId.South);
    }

    [Fact]
    public void NoneIsEmpty()
    {
        ExtrasSettings.None.IsEmpty.Should().BeTrue();
        (ExtrasSettings.None with { Toggle = [XboxButton.A] }).IsEmpty.Should().BeFalse();
    }
}

public sealed class TouchpadMouseTests
{
    private static InputState OneFinger(float x, float y, ButtonId buttons = ButtonId.None)
        => new() { Touch0 = new TouchPoint(x, y, true), Buttons = buttons };

    private static TouchpadMouseProcessor Pad(float sensitivity = 1f, float acceleration = 0f)
        => new(new TouchpadMouseSettings { Enabled = true, Sensitivity = sensitivity, Acceleration = acceleration });

    [Fact]
    public void FirstTouch_DoesNotJumpTheCursor()
    {
        var pad = Pad();

        var input = OneFinger(0.8f, 0.8f);
        var frame = pad.Process(in input, 0);

        (frame.Dx, frame.Dy).Should().Be((0, 0));
    }

    [Fact]
    public void Sliding_MovesRelativeToTheLastPosition()
    {
        var pad = Pad();
        var start = OneFinger(0.5f, 0.5f);
        var end = OneFinger(0.6f, 0.5f);
        pad.Process(in start, 0);

        var frame = pad.Process(in end, 10);

        frame.Dx.Should().BeInRange(149, 150, "0.1 of a pad width at sensitivity 1");
        frame.Dy.Should().Be(0);
    }

    [Fact]
    public void Acceleration_MakesFastSwipesTravelFurtherThanSlowOnes()
    {
        var slow = Pad(acceleration: 1f);
        var fast = Pad(acceleration: 1f);
        var start = OneFinger(0.5f, 0.5f);
        var end = OneFinger(0.52f, 0.5f);
        slow.Process(in start, 0);
        fast.Process(in start, 0);

        var slowFrame = slow.Process(in end, 1000);
        var fastFrame = fast.Process(in end, 10);

        fastFrame.Dx.Should().BeGreaterThan(slowFrame.Dx);
    }

    [Fact]
    public void SubPixelMovementIsKeptUntilItAddsUp()
    {
        var pad = Pad(sensitivity: 0.1f);
        var total = 0;
        var x = 0.5f;
        var first = OneFinger(x, 0.5f);
        pad.Process(in first, 0);

        for (var i = 1; i <= 100; i++)
        {
            x += 0.0001f;
            var input = OneFinger(x, 0.5f);
            total += pad.Process(in input, i * 10).Dx;
        }

        total.Should().BeInRange(1, 2, "100 steps of 0.0001 pad widths at 150 px per pad width are about 1.5 px");
    }

    [Fact]
    public void LiftingTheFinger_ForgetsThePosition()
    {
        var pad = Pad();
        var down = OneFinger(0.2f, 0.2f);
        var lifted = new InputState();
        var elsewhere = OneFinger(0.9f, 0.9f);
        pad.Process(in down, 0);
        pad.Process(in lifted, 10);

        var frame = pad.Process(in elsewhere, 20);

        (frame.Dx, frame.Dy).Should().Be((0, 0));
    }

    [Fact]
    public void Click_IsALeftClick_HeldUntilReleased()
    {
        var pad = Pad();
        var clicked = OneFinger(0.5f, 0.5f, ButtonId.Touchpad);
        var open = OneFinger(0.5f, 0.5f);

        pad.Process(in clicked, 0).Buttons.Should().Be(MouseButtons.Left);
        pad.Process(in clicked, 10).Buttons.Should().Be(MouseButtons.Left);
        pad.Process(in open, 20).Buttons.Should().Be(MouseButtons.None);
    }

    [Fact]
    public void ClickWithTwoFingers_IsARightClick_AndTheButtonSticksEvenIfAFingerLifts()
    {
        var pad = Pad();
        var two = new InputState
        {
            Touch0 = new TouchPoint(0.3f, 0.5f, true),
            Touch1 = new TouchPoint(0.7f, 0.5f, true),
            Buttons = ButtonId.Touchpad,
        };
        var oneLeft = OneFinger(0.3f, 0.5f, ButtonId.Touchpad);

        pad.Process(in two, 0).Buttons.Should().Be(MouseButtons.Right);
        pad.Process(in oneLeft, 10).Buttons.Should().Be(MouseButtons.Right);
    }

    [Fact]
    public void TwoFingersDoNotMoveTheCursor()
    {
        var pad = Pad();
        var a = new InputState { Touch0 = new TouchPoint(0.3f, 0.5f, true), Touch1 = new TouchPoint(0.7f, 0.5f, true) };
        var b = new InputState { Touch0 = new TouchPoint(0.4f, 0.5f, true), Touch1 = new TouchPoint(0.8f, 0.5f, true) };
        pad.Process(in a, 0);

        var frame = pad.Process(in b, 10);

        (frame.Dx, frame.Dy).Should().Be((0, 0));
    }
}

public sealed class BatteryAlertTrackerTests
{
    private static readonly DeviceId Pad = new("pad");

    private static BatteryInfo Level(int percent, BatteryState state = BatteryState.Discharging) => new(state, (byte)percent);

    [Fact]
    public void WarnsOnceBelowTwentyPercent_AndOnceMoreBelowTen()
    {
        var tracker = new BatteryAlertTracker();

        tracker.Update(Pad, Level(50)).Should().BeNull();
        tracker.Update(Pad, Level(20)).Should().BeNull("20 is not below 20");
        tracker.Update(Pad, Level(19)).Should().Be(BatteryAlertLevel.Low);
        tracker.Update(Pad, Level(18)).Should().BeNull("only once per crossing");
        tracker.Update(Pad, Level(10)).Should().BeNull("10 is not below 10");
        tracker.Update(Pad, Level(9)).Should().Be(BatteryAlertLevel.Critical);
        tracker.Update(Pad, Level(5)).Should().BeNull();
    }

    [Fact]
    public void JumpingStraightBelowTen_GivesOnlyTheCriticalWarning()
    {
        var tracker = new BatteryAlertTracker();

        tracker.Update(Pad, Level(60));
        tracker.Update(Pad, Level(8)).Should().Be(BatteryAlertLevel.Critical);
        tracker.Update(Pad, Level(15)).Should().BeNull("already warned at the lower level");
    }

    [Fact]
    public void WarnsAgainAfterChargingBackUp()
    {
        var tracker = new BatteryAlertTracker();
        tracker.Update(Pad, Level(15)).Should().Be(BatteryAlertLevel.Low);

        tracker.Update(Pad, Level(40, BatteryState.Charging)).Should().BeNull();
        tracker.Update(Pad, Level(35)).Should().BeNull();
        tracker.Update(Pad, Level(19)).Should().Be(BatteryAlertLevel.Low);
    }

    [Fact]
    public void ChargingOrWiredPadsNeverWarn()
    {
        var tracker = new BatteryAlertTracker();

        tracker.Update(Pad, Level(5, BatteryState.Charging)).Should().BeNull();
        tracker.Update(Pad, Level(0, BatteryState.Wired)).Should().BeNull();
        tracker.Update(Pad, Level(0, BatteryState.Unknown)).Should().BeNull();
    }

    [Fact]
    public void UnknownReadingsDoNotResetTheWarning()
    {
        var tracker = new BatteryAlertTracker();
        tracker.Update(Pad, Level(15));

        tracker.Update(Pad, Level(0, BatteryState.Unknown));

        tracker.Update(Pad, Level(14)).Should().BeNull();
    }

    [Fact]
    public void DevicesAreTrackedSeparately_AndForgetStartsOver()
    {
        var tracker = new BatteryAlertTracker();
        var other = new DeviceId("other");

        tracker.Update(Pad, Level(15)).Should().Be(BatteryAlertLevel.Low);
        tracker.Update(other, Level(15)).Should().Be(BatteryAlertLevel.Low);

        tracker.Forget(Pad);
        tracker.Update(Pad, Level(15)).Should().Be(BatteryAlertLevel.Low);
        tracker.Update(other, Level(15)).Should().BeNull();
    }
}
