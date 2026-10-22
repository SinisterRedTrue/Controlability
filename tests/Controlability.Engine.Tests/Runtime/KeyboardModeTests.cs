using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;

namespace Controlability.Engine.Tests.Runtime;

public sealed class KeyboardModeTests : IDisposable
{
    private static readonly DeviceInfo Pad1 = FakeInputSource.MakeDevice("pad1");
    private static readonly DeviceInfo Pad2 = FakeInputSource.MakeDevice("pad2");

    private readonly string _profilesFolder = Directory.CreateTempSubdirectory("controlability-keyboard-tests").FullName;
    private readonly FakeClock _clock = new();
    private readonly FakeInputSource _source = new();
    private readonly FakeKeyboardSource _keyboard = new();
    private readonly FakeOutputSink _sink = new();
    private readonly List<Settings> _saved = [];
    private readonly Settings _settings = new();
    private Controlability.Engine.Engine? _engine;

    private Controlability.Engine.Engine Engine => _engine ??= new Controlability.Engine.Engine(
        [_source],
        _sink,
        new ProfileStore(_profilesFolder),
        _clock,
        _settings,
        saveSettings: _saved.Add,
        keyboard: _keyboard);

    public void Dispose()
    {
        _engine?.Dispose();
        Directory.Delete(_profilesFolder, recursive: true);
    }

    private void Plug(DeviceInfo device)
    {
        _ = Engine;
        _source.Plug(device);
    }

    private string?[] SlotDevices() => [.. Engine.Slots.Select(s => s.Device?.Value)];

    [Fact]
    public void Changing_the_target_games_saves_them_and_restarts_the_keyboard_pad_with_them()
    {
        Plug(Pad1);
        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();

        Engine.SetKeyboardTargets(["Dungeons"]);
        Engine.Tick();

        _keyboard.StartCount.Should().Be(2);
        _keyboard.TargetProcesses.Should().Equal("Dungeons");
        _settings.KeyboardTargetProcesses.Should().Equal("Dungeons");
        _saved.Should().Contain(s => s.KeyboardTargetProcesses.SequenceEqual(new[] { "Dungeons" }));
        SlotDevices().Should().Equal(KeyboardPad.Id.Value, "pad1", null, null);
    }

    [Fact]
    public void Changing_the_target_games_in_mode_A_only_saves_them()
    {
        Plug(Pad1);
        Engine.Tick();

        Engine.SetKeyboardTargets(["Dungeons"]);
        Engine.Tick();

        _keyboard.StartCount.Should().Be(0);
        _settings.KeyboardTargetProcesses.Should().Equal("Dungeons");
        SlotDevices().Should().Equal("pad1", null, null, null);
    }

    [Fact]
    public void Mode_A_never_touches_the_keyboard()
    {
        Plug(Pad1);
        Plug(Pad2);

        Engine.Tick();
        Engine.SetMode(KeyboardMode.KeyboardFirst);
        Engine.Tick();
        Engine.Tick();

        _keyboard.StartCount.Should().Be(0);
        _keyboard.PollCount.Should().Be(0, "no keyboard code runs in Mode A");
        SlotDevices().Should().Equal("pad1", "pad2", null, null);
        _sink.Connected.Should().Equal(0, 1);
    }

    [Fact]
    public void Switching_to_mode_B_puts_the_keyboard_in_slot_one_and_moves_controllers_up()
    {
        Plug(Pad1);
        Plug(Pad2);
        Engine.Tick();

        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();

        SlotDevices().Should().Equal(KeyboardPad.Id.Value, "pad1", "pad2", null);
        _keyboard.IsActive.Should().BeTrue();
        Engine.Mode.Should().Be(KeyboardMode.KeyboardAsController);
        _settings.Mode.Should().Be(KeyboardMode.KeyboardAsController);
        _saved.Should().Contain(s => s.Mode == KeyboardMode.KeyboardAsController);
    }

    [Fact]
    public void Switching_modes_disconnects_every_pad_then_reconnects_them_in_slot_order()
    {
        Plug(Pad1);
        Plug(Pad2);
        Engine.Tick();
        _sink.Connected.Should().Equal(0, 1);

        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();

        _sink.Disconnected.Should().Equal(0, 1);
        _sink.Connected.Skip(2).Should().Equal([0, 1, 2], "the keyboard pad must connect first so it is XInput player 1");
        Engine.Slots[1].XInputIndex.Should().Be(Engine.Slots[0].XInputIndex + 1);
        Engine.Slots[2].XInputIndex.Should().Be(Engine.Slots[0].XInputIndex + 2);
    }

    [Fact]
    public void Switching_back_to_mode_A_stops_the_keyboard_and_refills_the_slots_from_the_top()
    {
        Plug(Pad1);
        Plug(Pad2);
        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();

        Engine.SetMode(KeyboardMode.KeyboardFirst);
        Engine.Tick();

        _keyboard.IsActive.Should().BeFalse();
        _keyboard.StopCount.Should().Be(1);
        SlotDevices().Should().Equal("pad1", "pad2", null, null);
        Engine.Devices.Select(d => d.Id).Should().BeEquivalentTo([Pad1.Id, Pad2.Id], "the keyboard pad is gone");
        _sink.Connected.Skip(3).Should().Equal(0, 1);
    }

    [Fact]
    public void A_controller_that_arrives_in_mode_B_starts_at_slot_two()
    {
        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();

        Plug(Pad1);
        Engine.Tick();

        SlotDevices().Should().Equal(KeyboardPad.Id.Value, "pad1", null, null);
    }

    [Fact]
    public void The_keyboard_takes_slot_one_even_under_the_manual_policy()
    {
        _settings.SlotPolicy = SlotPolicy.Manual;
        Plug(Pad1);

        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();

        SlotDevices().Should().Equal(KeyboardPad.Id.Value, null, null, null);
    }

    [Fact]
    public void The_keyboard_pad_is_polled_and_its_state_reaches_virtual_pad_one()
    {
        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();

        _keyboard.SetState(new InputState { Buttons = ButtonId.South, LeftX = 1f });
        Engine.Tick();

        _keyboard.PollCount.Should().BeGreaterThan(0);
        var sent = _sink.LastSubmitted(0);
        sent.IsPressed(XboxButton.A).Should().BeTrue();
        sent.LeftX.Should().BeGreaterThan(30000);
    }

    [Fact]
    public void The_normal_keyboard_style_keeps_the_slot_but_sends_neutral_and_swallows_nothing()
    {
        _settings.KeyboardStyle = KeyboardInputStyle.NormalKeyboard;
        Plug(Pad1);
        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();

        _keyboard.SetState(new InputState { Buttons = ButtonId.South, LeftX = 1f });
        Engine.Tick();

        SlotDevices().Should().Equal(KeyboardPad.Id.Value, "pad1", null, null);
        _sink.Connected.Should().Contain(0, "the game still counts the keyboard's pad as connected");
        var sent = _sink.LastSubmitted(0);
        sent.IsPressed(XboxButton.A).Should().BeFalse();
        sent.LeftX.Should().Be(0);
        _keyboard.Blocking.Should().BeFalse("the real keys must reach the game");
    }

    [Fact]
    public void Switching_the_style_at_runtime_toggles_blocking_and_input_without_reconnecting_pads()
    {
        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();
        _keyboard.Blocking.Should().BeTrue();
        var connects = _sink.Connected.Count;

        Engine.SetKeyboardStyle(KeyboardInputStyle.NormalKeyboard);
        Engine.Tick();

        Engine.KeyboardStyle.Should().Be(KeyboardInputStyle.NormalKeyboard);
        _keyboard.Blocking.Should().BeFalse();
        _saved.Should().Contain(s => s.KeyboardStyle == KeyboardInputStyle.NormalKeyboard);
        _sink.Connected.Count.Should().Be(connects);

        Engine.SetKeyboardStyle(KeyboardInputStyle.XboxController);
        Engine.Tick();

        _keyboard.Blocking.Should().BeTrue();
    }

    [Fact]
    public void Blocking_is_on_only_while_the_keyboard_has_a_slot_and_the_engine_runs()
    {
        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();
        _keyboard.Blocking.Should().BeTrue();

        Engine.Pause(true);
        Engine.Tick();
        _keyboard.Blocking.Should().BeFalse("paused: the keys belong to the user again");

        Engine.Pause(false);
        Engine.Tick();
        _keyboard.Blocking.Should().BeTrue();

        Engine.Unassign(0);
        Engine.Tick();
        _keyboard.Blocking.Should().BeFalse("no pad would receive the swallowed keys");
    }

    [Fact]
    public void The_panic_hotkey_switches_to_mode_A_and_saves_it()
    {
        Plug(Pad1);
        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();
        _saved.Clear();

        _keyboard.Panic();
        _keyboard.Blocking.Should().BeFalse("the hook already freed the keyboard, before the engine did anything");
        Engine.Tick();

        Engine.Mode.Should().Be(KeyboardMode.KeyboardFirst);
        _keyboard.IsActive.Should().BeFalse();
        SlotDevices().Should().Equal("pad1", null, null, null);
        _saved.Should().Contain(s => s.Mode == KeyboardMode.KeyboardFirst);
    }

    [Fact]
    public void The_chosen_keyboard_and_the_profile_bindings_reach_the_source()
    {
        _settings.KeyboardDevice = @"\\?\HID#VID_046D&PID_C31C#1";
        var store = new ProfileStore(_profilesFolder);
        store.Save(new Profile
        {
            Id = "default-keyboard",
            Name = "Keyboard",
            AppliesTo = new ProfileTargets { ControllerTypes = [ControllerType.Keyboard] },
            Keyboard = new ProfileKeyboard { Keys = new() { ["W"] = "LeftStickUp", ["Space"] = "A" } },
        });

        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();

        _keyboard.StartedWithKeyboardId.Should().Be(@"\\?\HID#VID_046D&PID_C31C#1");
        _keyboard.Bindings!.KeyCount.Should().Be(2);
    }

    [Fact]
    public void Stopping_the_engine_releases_the_keyboard()
    {
        Engine.SetMode(KeyboardMode.KeyboardAsController);
        Engine.Tick();

        Engine.Shutdown();

        _keyboard.IsActive.Should().BeFalse();
        Engine.Slots.Should().OnlyContain(s => s.State == SlotState.Empty);
    }

    [Fact]
    public void Starting_the_engine_in_mode_B_starts_the_keyboard()
    {
        _settings.Mode = KeyboardMode.KeyboardAsController;
        Plug(Pad1);

        Engine.Start();
        SpinWait.SpinUntil(() => Engine.Slots[1].Device is not null, TimeSpan.FromSeconds(5)).Should().BeTrue();
        Engine.Stop();

        _keyboard.StartCount.Should().BeGreaterThan(0);
        _keyboard.StopCount.Should().BeGreaterThan(0, "stopping the engine must not leave the hooks installed");
        _keyboard.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Dispose_disposes_the_keyboard_source()
    {
        Engine.Dispose();

        _keyboard.Disposed.Should().BeTrue();
    }
}
