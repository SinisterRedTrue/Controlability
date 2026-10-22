using Controlability.Core.Keyboard;

namespace Controlability.Core.Tests.Keyboard;

public class MouseStickTests
{
    [Fact]
    public void A_burst_then_silence_decays_exponentially_with_the_decay_time()
    {
        var stick = new MouseStick();
        stick.Update(30, 0, 1, sensitivity: 1f, decayMs: 60); // 30 px / 60 ms = 0.5
        var start = stick.X;
        start.Should().BeApproximately(0.5f, 1e-5f);

        for (var i = 0; i < 60; i++)
        {
            stick.Update(0, 0, 1, 1f, 60);
        }

        // 60 ms later = one time constant: e^-1 of the start value.
        stick.X.Should().BeApproximately((float)(start * Math.Exp(-1)), 1e-4f);
    }

    [Fact]
    public void Decay_does_not_depend_on_the_step_size()
    {
        var fine = new MouseStick();
        var coarse = new MouseStick();
        fine.Update(30, 0, 1, 1f, 60);
        coarse.Update(30, 0, 1, 1f, 60);

        for (var i = 0; i < 100; i++)
        {
            fine.Update(0, 0, 1, 1f, 60);
        }

        for (var i = 0; i < 10; i++)
        {
            coarse.Update(0, 0, 10, 1f, 60);
        }

        coarse.X.Should().BeApproximately(fine.X, 1e-4f);
    }

    [Fact]
    public void It_returns_exactly_to_center_when_the_mouse_stops()
    {
        var stick = new MouseStick();
        stick.Update(60, -60, 1, 1f, 60);

        for (var i = 0; i < 2000; i++)
        {
            stick.Update(0, 0, 1, 1f, 60);
        }

        stick.X.Should().Be(0f);
        stick.Y.Should().Be(0f);
    }

    [Theory]
    [InlineData(1f, 60)]
    [InlineData(1f, 120)]
    [InlineData(0.5f, 60)]
    public void A_steady_speed_settles_at_speed_times_sensitivity_whatever_the_decay(float sensitivity, int decayMs)
    {
        var stick = new MouseStick();

        for (var i = 0; i < 3000; i++)
        {
            stick.Update(0.5f, 0, 1, sensitivity, decayMs); // 0.5 px per ms
        }

        stick.X.Should().BeApproximately(0.5f * sensitivity, 0.01f);
    }

    [Fact]
    public void Moving_the_mouse_down_the_screen_pushes_the_stick_down()
    {
        var stick = new MouseStick();

        stick.Update(0, 6, 1, 1f, 60);

        stick.Y.Should().BeApproximately(-0.1f, 1e-5f);
        stick.X.Should().Be(0f);
    }

    [Fact]
    public void The_deflection_never_exceeds_one()
    {
        var stick = new MouseStick();

        stick.Update(5000, 5000, 1, 4f, 60);

        Math.Sqrt((stick.X * stick.X) + (stick.Y * stick.Y)).Should().BeApproximately(1.0, 1e-5);
    }

    [Fact]
    public void A_decay_of_zero_follows_the_speed_directly()
    {
        var stick = new MouseStick();

        stick.Update(0.5f, 0, 1, 1f, 0);
        stick.X.Should().BeApproximately(0.5f, 1e-5f);

        stick.Update(0, 0, 1, 1f, 0);
        stick.X.Should().Be(0f);
    }

    [Fact]
    public void A_huge_time_step_is_capped()
    {
        var capped = new MouseStick();
        var normal = new MouseStick();
        capped.Update(10, 0, 1, 1f, 60);
        normal.Update(10, 0, 1, 1f, 60);

        capped.Update(0, 0, 60_000, 1f, 60);
        normal.Update(0, 0, MouseStick.MaxStepMs, 1f, 60);

        capped.X.Should().Be(normal.X);
    }
}
