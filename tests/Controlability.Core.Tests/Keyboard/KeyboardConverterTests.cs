using Controlability.Core.Keyboard;
using Controlability.Core.Models;
using Controlability.Core.Profiles;

namespace Controlability.Core.Tests.Keyboard;

public class KeyboardConverterTests
{
    private const int W = 0x57, A = 0x41, S = 0x53, D = 0x44, Space = 0x20, LeftShift = 0xA0;

    private static KeyBindings Bindings(float sensitivity = 1f, int decayMs = 60) => KeyBindings.From(new ProfileKeyboard
    {
        Keys = new Dictionary<string, string>
        {
            ["W"] = "LeftStickUp",
            ["A"] = "LeftStickLeft",
            ["S"] = "LeftStickDown",
            ["D"] = "LeftStickRight",
            ["Space"] = "A",
            ["Shift"] = "B",
            ["MouseLeft"] = "RightTrigger",
            ["MouseRight"] = "LeftTrigger",
            ["Up"] = "DpadUp",
            ["I"] = "RightStickUp",
        },
        MouseSensitivity = sensitivity,
        DecayMs = decayMs,
    });

    private static InputState Convert(KeyboardConverter converter, params int[] keys)
        => converter.Convert([.. keys], MouseButtons.None, 0, 0, 1);

    [Fact]
    public void No_keys_is_neutral()
    {
        var state = Convert(new KeyboardConverter(Bindings()));

        state.Should().BeEquivalentTo(InputState.Neutral);
    }

    [Theory]
    [InlineData(W, 0f, 1f)]
    [InlineData(S, 0f, -1f)]
    [InlineData(A, -1f, 0f)]
    [InlineData(D, 1f, 0f)]
    public void Single_direction_key_is_full_deflection(int key, float x, float y)
    {
        var state = Convert(new KeyboardConverter(Bindings()), key);

        state.LeftX.Should().Be(x);
        state.LeftY.Should().Be(y);
    }

    [Theory]
    [InlineData(W, D, 1, 1)]
    [InlineData(W, A, -1, 1)]
    [InlineData(S, D, 1, -1)]
    [InlineData(S, A, -1, -1)]
    public void Diagonals_are_normalized_to_length_one(int first, int second, int signX, int signY)
    {
        var state = Convert(new KeyboardConverter(Bindings()), first, second);

        state.LeftX.Should().BeApproximately(signX * 0.70710678f, 1e-5f);
        state.LeftY.Should().BeApproximately(signY * 0.70710678f, 1e-5f);
        Math.Sqrt((state.LeftX * state.LeftX) + (state.LeftY * state.LeftY)).Should().BeApproximately(1.0, 1e-5);
    }

    [Theory]
    [InlineData(W, S)]
    [InlineData(A, D)]
    public void Opposite_keys_cancel(int first, int second)
    {
        var state = Convert(new KeyboardConverter(Bindings()), first, second);

        state.LeftX.Should().Be(0f);
        state.LeftY.Should().Be(0f);
    }

    [Fact]
    public void One_axis_cancelling_leaves_the_other_at_full_length()
    {
        // W+S cancel, D stays: a pure right, not a diagonal.
        var state = Convert(new KeyboardConverter(Bindings()), W, S, D);

        state.LeftX.Should().Be(1f);
        state.LeftY.Should().Be(0f);
    }

    [Fact]
    public void Buttons_triggers_and_dpad_come_from_the_bindings()
    {
        var converter = new KeyboardConverter(Bindings());

        var state = converter.Convert([Space, 0x26], MouseButtons.Left | MouseButtons.Right, 0, 0, 1);

        state.Buttons.Should().Be(ButtonId.South | ButtonId.DpadUp);
        state.RightTrigger.Should().Be(1f);
        state.LeftTrigger.Should().Be(1f);
    }

    [Fact]
    public void Unbound_keys_are_ignored()
    {
        var state = Convert(new KeyboardConverter(Bindings()), 0x5A /* Z */, 0x70 /* F1 */);

        state.Should().BeEquivalentTo(InputState.Neutral);
    }

    [Fact]
    public void Left_shift_matches_a_binding_named_Shift()
    {
        var state = Convert(new KeyboardConverter(Bindings()), LeftShift);

        state.Buttons.Should().Be(ButtonId.East);
    }

    [Fact]
    public void Right_stick_keys_work_and_the_mouse_adds_to_them()
    {
        var converter = new KeyboardConverter(Bindings(decayMs: 0));

        var keysOnly = converter.Convert([0x49], MouseButtons.None, 0, 0, 1);
        keysOnly.RightY.Should().Be(1f);

        var withMouse = converter.Convert([0x49], MouseButtons.None, 5, 0, 1);
        withMouse.RightX.Should().BeGreaterThan(0f);
        Math.Sqrt((withMouse.RightX * withMouse.RightX) + (withMouse.RightY * withMouse.RightY)).Should().BeApproximately(1.0, 1e-5);
    }

    [Fact]
    public void Mouse_moving_right_pushes_the_right_stick_right_and_moving_up_pushes_it_up()
    {
        var converter = new KeyboardConverter(Bindings(decayMs: 0));

        var state = converter.Convert([], MouseButtons.None, 0.5f, -0.5f, 1);

        state.RightX.Should().BeApproximately(0.5f, 1e-5f);
        state.RightY.Should().BeApproximately(0.5f, 1e-5f, "screen-up is negative dy and must become stick-up (+1)");
    }

    [Fact]
    public void Shipped_keyboard_profile_compiles_without_warnings()
    {
        var store = new ProfileStore(Samples.RepoProfilesFolder());
        var profile = store.Get("default-keyboard");
        var warnings = new List<string>();

        var bindings = KeyBindings.From(profile!.Keyboard, warnings.Add);

        warnings.Should().BeEmpty();
        bindings.KeyCount.Should().Be(15);
        bindings.BoundMouseButtons.Should().Be(MouseButtons.Left | MouseButtons.Right);
        bindings.ControlOf(W).Should().Be(KeyboardControl.LeftStickUp);
        bindings.ControlOf(MouseButtons.Left).Should().Be(KeyboardControl.RightTrigger);
        bindings.ControlOf(0xA2 /* left Ctrl */).Should().Be(KeyboardControl.LeftThumb);
    }

    [Fact]
    public void Unknown_keys_and_controls_are_skipped_with_a_warning()
    {
        var warnings = new List<string>();

        var bindings = KeyBindings.From(
            new ProfileKeyboard { Keys = new() { ["W"] = "Nope", ["Banana"] = "A", ["Q"] = "Y", ["E"] = "5" } },
            warnings.Add);

        bindings.KeyCount.Should().Be(1);
        warnings.Should().HaveCount(3);
    }
}
