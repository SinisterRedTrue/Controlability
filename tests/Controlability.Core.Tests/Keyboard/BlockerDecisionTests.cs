using Controlability.Core.Keyboard;
using Controlability.Core.Profiles;

namespace Controlability.Core.Tests.Keyboard;

public class BlockerDecisionTests
{
    private const int W = 0x57, Z = 0x5A, LeftControl = 0xA2, RightControl = 0xA3, LeftShift = 0xA0;

    private static readonly KeyBindings Bindings = KeyBindings.From(new ProfileKeyboard
    {
        Keys = new Dictionary<string, string>
        {
            ["W"] = "LeftStickUp",
            ["Ctrl"] = "LeftThumb",
            ["Shift"] = "B",
            ["MouseLeft"] = "RightTrigger",
        },
    });

    [Fact]
    public void Bound_keys_are_swallowed_and_unbound_keys_pass()
    {
        KeySwallowTracker.ShouldSwallowKey(Bindings, W, injected: false).Should().BeTrue();
        KeySwallowTracker.ShouldSwallowKey(Bindings, Z, injected: false).Should().BeFalse();
    }

    [Fact]
    public void An_empty_profile_swallows_nothing()
    {
        KeySwallowTracker.ShouldSwallowKey(KeyBindings.Empty, W, injected: false).Should().BeFalse();
    }

    [Fact]
    public void Left_and_right_modifiers_match_the_generic_binding()
    {
        KeySwallowTracker.ShouldSwallowKey(Bindings, LeftControl, false).Should().BeTrue();
        KeySwallowTracker.ShouldSwallowKey(Bindings, RightControl, false).Should().BeTrue();
        KeySwallowTracker.ShouldSwallowKey(Bindings, LeftShift, false).Should().BeTrue();
    }

    [Fact]
    public void Injected_events_always_pass()
    {
        KeySwallowTracker.ShouldSwallowKey(Bindings, W, injected: true).Should().BeFalse();

        var tracker = new KeySwallowTracker();
        tracker.DecideKey(Bindings, W, isDown: true, injected: true).Should().BeFalse();
        tracker.DecideMouse(Bindings, MouseButtons.Left, isDown: true, injected: true).Should().BeFalse();
    }

    [Fact]
    public void Key_up_follows_the_key_down_decision()
    {
        var tracker = new KeySwallowTracker();

        tracker.DecideKey(Bindings, W, true, false).Should().BeTrue();
        tracker.DecideKey(Bindings, W, true, false).Should().BeTrue("auto-repeat of a swallowed key");
        tracker.DecideKey(Bindings, W, false, false).Should().BeTrue();
    }

    [Fact]
    public void A_key_held_from_before_blocking_began_gets_its_key_up_through()
    {
        var tracker = new KeySwallowTracker();

        // No key-down was ever seen by the tracker: the game saw it, so it must see the release too.
        tracker.DecideKey(Bindings, W, isDown: false, injected: false).Should().BeFalse();
    }

    [Fact]
    public void Unbound_keys_stay_free_down_and_up()
    {
        var tracker = new KeySwallowTracker();

        tracker.DecideKey(Bindings, Z, true, false).Should().BeFalse();
        tracker.DecideKey(Bindings, Z, false, false).Should().BeFalse();
    }

    [Fact]
    public void Mouse_buttons_follow_the_same_rules()
    {
        var tracker = new KeySwallowTracker();

        tracker.DecideMouse(Bindings, MouseButtons.Left, true, false).Should().BeTrue();
        tracker.DecideMouse(Bindings, MouseButtons.Left, false, false).Should().BeTrue();
        tracker.DecideMouse(Bindings, MouseButtons.Right, true, false).Should().BeFalse("MouseRight is not bound");
        tracker.DecideMouse(Bindings, MouseButtons.Right, false, false).Should().BeFalse();
    }

    [Fact]
    public void Reset_forgets_what_was_swallowed()
    {
        var tracker = new KeySwallowTracker();
        tracker.DecideKey(Bindings, W, true, false);

        tracker.Reset();

        tracker.DecideKey(Bindings, W, false, false).Should().BeFalse();
    }
}

public class PanicHotkeyDetectorTests
{
    private const int Pause = KeyCodes.Pause;

    [Fact]
    public void Ctrl_Alt_Pause_fires()
    {
        var detector = new PanicHotkeyDetector();

        detector.OnKey(KeyCodes.LeftControl, true).Should().BeFalse();
        detector.OnKey(KeyCodes.LeftAlt, true).Should().BeFalse();
        detector.OnKey(Pause, true).Should().BeTrue();
    }

    [Fact]
    public void Right_hand_modifiers_and_the_generic_codes_count()
    {
        var detector = new PanicHotkeyDetector();
        detector.OnKey(KeyCodes.RightControl, true);
        detector.OnKey(KeyCodes.RightAlt, true);
        detector.OnKey(Pause, true).Should().BeTrue();

        var generic = new PanicHotkeyDetector();
        generic.OnKey(KeyCodes.Control, true);
        generic.OnKey(KeyCodes.Alt, true);
        generic.OnKey(Pause, true).Should().BeTrue();
    }

    [Fact]
    public void Ctrl_Break_counts_as_Pause()
    {
        var detector = new PanicHotkeyDetector();
        detector.OnKey(KeyCodes.LeftControl, true);
        detector.OnKey(KeyCodes.LeftAlt, true);

        detector.OnKey(KeyCodes.Cancel, true).Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Without_both_modifiers_it_does_not_fire(bool ctrl, bool alt)
    {
        var detector = new PanicHotkeyDetector();
        if (ctrl)
        {
            detector.OnKey(KeyCodes.LeftControl, true);
        }

        if (alt)
        {
            detector.OnKey(KeyCodes.LeftAlt, true);
        }

        detector.OnKey(Pause, true).Should().BeFalse();
    }

    [Fact]
    public void A_released_modifier_no_longer_counts()
    {
        var detector = new PanicHotkeyDetector();
        detector.OnKey(KeyCodes.LeftControl, true);
        detector.OnKey(KeyCodes.LeftAlt, true);
        detector.OnKey(KeyCodes.LeftAlt, false);

        detector.OnKey(Pause, true).Should().BeFalse();
    }

    [Fact]
    public void Releasing_Pause_does_not_fire()
    {
        var detector = new PanicHotkeyDetector();
        detector.OnKey(KeyCodes.LeftControl, true);
        detector.OnKey(KeyCodes.LeftAlt, true);

        detector.OnKey(Pause, false).Should().BeFalse();
    }
}

public class HookWatchdogTests
{
    [Fact]
    public void Reinstalls_when_raw_input_saw_keys_the_hook_missed_for_two_seconds()
    {
        HookWatchdog.ShouldReinstall(blocking: true, lastRawKeyMs: 10_000, lastHookKeyMs: 7_000).Should().BeTrue();
    }

    [Fact]
    public void Does_nothing_while_both_see_the_same_keys()
    {
        HookWatchdog.ShouldReinstall(true, 10_000, 9_999).Should().BeFalse();
        HookWatchdog.ShouldReinstall(true, 10_000, 10_000).Should().BeFalse();
    }

    [Fact]
    public void Does_nothing_below_the_threshold()
    {
        HookWatchdog.ShouldReinstall(true, 10_000, 8_500).Should().BeFalse();
    }

    [Fact]
    public void Does_nothing_when_blocking_is_off_or_no_key_was_seen()
    {
        HookWatchdog.ShouldReinstall(false, 10_000, 0).Should().BeFalse();
        HookWatchdog.ShouldReinstall(true, 0, 0).Should().BeFalse();
    }
}

public class KeyCodesTests
{
    [Theory]
    [InlineData("W", 0x57)]
    [InlineData("w", 0x57)]
    [InlineData("Space", 0x20)]
    [InlineData("Esc", 0x1B)]
    [InlineData("Escape", 0x1B)]
    [InlineData("Shift", 0x10)]
    [InlineData("LeftShift", 0x10)]
    [InlineData("Ctrl", 0x11)]
    [InlineData("F5", 0x74)]
    [InlineData("NumPad3", 0x63)]
    [InlineData("5", 0x35)]
    public void Names_parse_to_normalized_codes(string name, int expected)
    {
        KeyCodes.TryParse(name, out var code).Should().BeTrue();
        code.Should().Be(expected);
    }

    [Fact]
    public void Unknown_names_do_not_parse()
    {
        KeyCodes.TryParse("Banana", out _).Should().BeFalse();
        KeyCodes.TryParse("", out _).Should().BeFalse();
    }

    [Fact]
    public void Every_canonical_name_round_trips()
    {
        for (var vk = 1; vk < 256; vk++)
        {
            var name = KeyCodes.NameOf(vk);
            if (name.StartsWith("VK_", StringComparison.Ordinal))
            {
                continue;
            }

            KeyCodes.TryParse(name, out var back).Should().BeTrue(name);
            back.Should().Be(KeyCodes.Normalize(vk), name);
        }
    }

    [Fact]
    public void Unnamed_keys_print_their_code()
    {
        KeyCodes.NameOf(0xE8).Should().Be("VK_0xE8");
    }
}
