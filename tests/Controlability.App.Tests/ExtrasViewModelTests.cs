using Controlability.App.ViewModels;
using Controlability.Core.Extras;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Engine;

namespace Controlability.App.Tests;

public sealed class BatteryNotifierTests : IDisposable
{
    private static readonly DeviceInfo Pad = FakeEngine.MakeDevice("DS4");

    private readonly FakeEngine _engine = new();
    private readonly FakeNotifier _notifier = new();
    private readonly BatteryNotifier _batteryNotifier;

    public BatteryNotifierTests()
    {
        _engine.Devices = [Pad];
        _batteryNotifier = new BatteryNotifier(_engine, _notifier, new FakeUiThread(), new FakeClock());
    }

    public void Dispose() => _batteryNotifier.Dispose();

    private void Report(int percent, BatteryState state = BatteryState.Discharging)
    {
        _engine.Live[Pad.Id] = new InputState { Battery = new BatteryInfo(state, (byte)percent) };
        _batteryNotifier.Check();
    }

    [Fact]
    public void Toasts_onceBelowTwenty_andOnceBelowTen()
    {
        Report(50);
        Report(19);
        Report(18);
        Report(9);
        Report(8);

        _notifier.Notifications.Select(n => n.Message).Should().Equal("DS4: battery low (19%)", "DS4: battery almost empty (9%)");
    }

    [Fact]
    public void Charging_resets_so_the_next_drop_toasts_again()
    {
        Report(15);
        Report(60, BatteryState.Charging);
        Report(30);
        Report(19);

        _notifier.Notifications.Should().HaveCount(2);
    }

    [Fact]
    public void A_keyboard_pad_and_a_wired_pad_never_toast()
    {
        var keyboard = FakeEngine.MakeDevice("kbd", ControllerType.Keyboard);
        _engine.Devices = [keyboard, Pad];
        _engine.Live[keyboard.Id] = new InputState { Battery = new BatteryInfo(BatteryState.Discharging, 1) };

        Report(5, BatteryState.Wired);

        _notifier.Notifications.Should().BeEmpty();
    }

    [Fact]
    public void A_pad_that_comes_back_low_after_unplugging_warns_again()
    {
        Report(15);
        _engine.Devices = [];
        _batteryNotifier.Check();
        _engine.Devices = [Pad];

        Report(15);

        _notifier.Notifications.Should().HaveCount(2);
    }

    [Fact]
    public void Stops_listening_when_disposed()
    {
        _engine.ChangedSubscribers.Should().Be(1);

        _batteryNotifier.Dispose();

        _engine.ChangedSubscribers.Should().Be(0);
    }
}

public sealed class BatteryAndGameDisplayTests
{
    private static readonly DeviceInfo Pad = FakeEngine.MakeDevice("DS4");

    private readonly FakeEngine _engine = new();

    public BatteryAndGameDisplayTests()
    {
        _engine.Devices = [Pad];
        _engine.Slots = [FakeEngine.Active(0, Pad), .. _engine.Slots.Skip(1)];
    }

    [Fact]
    public void Tray_tooltip_lists_the_battery_of_each_slot()
    {
        _engine.Live[Pad.Id] = new InputState { Battery = new BatteryInfo(BatteryState.Discharging, 72) };

        using var tray = new TrayViewModel(_engine, new FakeShell(), new FakeUiThread(), new FakeClock());

        tray.Tooltip.Should().Be("Controlability" + Environment.NewLine + "P1 72%");
    }

    [Fact]
    public void Tray_tooltip_has_no_battery_line_for_wired_or_unknown_pads()
    {
        _engine.Live[Pad.Id] = new InputState { Battery = new BatteryInfo(BatteryState.Wired, 100) };

        using var tray = new TrayViewModel(_engine, new FakeShell(), new FakeUiThread(), new FakeClock());

        tray.Tooltip.Should().Be("Controlability");
    }

    [Fact]
    public void Player_card_shows_the_battery()
    {
        _engine.Live[Pad.Id] = new InputState { Battery = new BatteryInfo(BatteryState.Discharging, 42) };

        using var dashboard = new DashboardViewModel(
            _engine, new FakeProfileCatalog(), new FakeUiThread(), new FakeClock(), new FakeUrlLauncher());

        dashboard.Cards[0].BatteryText.Should().Be("42%");
    }

    [Fact]
    public void Dashboard_shows_the_active_game_only_while_there_is_one()
    {
        using var dashboard = new DashboardViewModel(
            _engine, new FakeProfileCatalog(), new FakeUiThread(), new FakeClock(), new FakeUrlLauncher());
        dashboard.HasActiveGame.Should().BeFalse();

        _engine.ActiveGame = "eldenring";
        dashboard.Refresh();
        dashboard.ActiveGameText.Should().Be("Game profile: eldenring");
        dashboard.HasActiveGame.Should().BeTrue();

        _engine.ActiveGame = null;
        dashboard.Refresh();
        dashboard.HasActiveGame.Should().BeFalse();
    }
}

public sealed class EditorExtrasTests
{
    private static readonly DeviceInfo Pad = FakeEngine.MakeDevice("pad");

    private readonly FakeEngine _engine = new();
    private readonly FakeProfileEditor _store = new();

    public EditorExtrasTests()
    {
        _engine.Devices = [Pad];
        _store.Profiles["mine"] = new Profile { Id = "mine", Name = "Mine" };
    }

    private ProfileEditorViewModel Editor()
        => new(_store.Get("mine")!, _store, _engine, new FakeRunningApps(), new FakeDialogs(), new FakeUiThread(), new FakeClock());

    [Fact]
    public void Extras_start_with_the_profile_values()
    {
        _store.Profiles["mine"].Extras.GyroToRightStick = true;
        _store.Profiles["mine"].Extras.Turbo["A"] = 15;
        _store.Profiles["mine"].Extras.Toggle.Add("LeftThumb");

        var editor = Editor();

        editor.GyroEnabled.Should().BeTrue();
        editor.TouchpadMouse.Should().BeFalse("off by default");
        var a = editor.ExtraRows.Single(r => r.Button == XboxButton.A);
        (a.IsTurbo, a.TurboHz, a.IsToggle).Should().Be((true, 15, false));
        editor.ExtraRows.Single(r => r.Button == XboxButton.LeftThumb).IsToggle.Should().BeTrue();
    }

    [Fact]
    public void Editing_the_gyro_marks_the_draft_dirty_and_saves_the_values()
    {
        var editor = Editor();
        editor.IsDirty.Should().BeFalse();

        editor.GyroEnabled = true;
        editor.GyroSensitivityX = 2.5;
        editor.GyroInvertY = true;
        editor.GyroSmoothing = 0.6;
        editor.GyroActivation = GyroActivation.WhileHeld;
        editor.GyroButton = "LeftShoulder";
        editor.IsDirty.Should().BeTrue();
        editor.Save();

        var extras = _store.Profiles["mine"].Extras;
        extras.GyroToRightStick.Should().BeTrue();
        extras.GyroSensitivityX.Should().Be(2.5f);
        extras.GyroInvertY.Should().BeTrue();
        extras.GyroSmoothing.Should().Be(0.6f);
        extras.GyroActivation.Should().Be(GyroActivation.WhileHeld);
        extras.GyroButton.Should().Be("LeftShoulder");
        _engine.Calls.Should().Contain("ReloadProfile:mine");
    }

    [Fact]
    public void The_gyro_button_choice_only_shows_for_hold_and_toggle()
    {
        var editor = Editor();
        editor.GyroNeedsButton.Should().BeFalse();

        editor.GyroActivation = GyroActivation.Toggle;

        editor.GyroNeedsButton.Should().BeTrue();
        editor.GyroButtonOptions.First().Value.Should().BeEmpty("the first entry is (none)");
        editor.GyroButtonOptions.Should().Contain(o => o.Value == "LeftShoulder" && o.Label == "L1");
    }

    [Fact]
    public void Turbo_and_toggle_rows_write_into_the_draft()
    {
        var editor = Editor();
        var a = editor.ExtraRows.Single(r => r.Button == XboxButton.A);

        a.IsTurbo = true;
        a.TurboHz = 20;
        a.IsToggle = true;
        editor.Save();

        var extras = _store.Profiles["mine"].Extras;
        extras.Turbo.Should().BeEquivalentTo(new Dictionary<string, int> { ["A"] = 20 });
        extras.Toggle.Should().Equal("A");

        a.IsTurbo = false;
        a.IsToggle = false;
        editor.Save();
        extras = _store.Profiles["mine"].Extras;
        extras.Turbo.Should().BeEmpty();
        extras.Toggle.Should().BeEmpty();
    }

    [Fact]
    public void Turbo_defaults_to_ten_presses_per_second()
    {
        var editor = Editor();
        var b = editor.ExtraRows.Single(r => r.Button == XboxButton.B);

        b.IsTurbo = true;

        editor.Save();
        _store.Profiles["mine"].Extras.Turbo["B"].Should().Be(10);
    }

    [Fact]
    public void Revert_puts_the_extra_rows_back()
    {
        var editor = Editor();
        editor.ExtraRows[0].IsTurbo = true;
        editor.TouchpadMouse = true;

        editor.Revert();

        editor.ExtraRows[0].IsTurbo.Should().BeFalse();
        editor.TouchpadMouse.Should().BeFalse();
        editor.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void An_unknown_gyro_button_in_the_file_is_reported_and_blocks_saving()
    {
        _store.Profiles["mine"].Extras.GyroButton = "Banana";

        var editor = Editor();

        editor.IsValid.Should().BeFalse();
        editor.ValidationText.Should().Contain("Banana");
    }
}
