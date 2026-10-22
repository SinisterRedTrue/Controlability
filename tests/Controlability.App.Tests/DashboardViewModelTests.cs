using Controlability.App.Services;
using Controlability.App.ViewModels;
using Controlability.Core.Models;
using Controlability.Core.Settings;
using Controlability.Engine;

namespace Controlability.App.Tests;

public sealed class DashboardViewModelTests : IDisposable
{
    private static readonly DeviceInfo Pad1 = FakeEngine.MakeDevice("pad1");
    private static readonly DeviceInfo Pad2 = FakeEngine.MakeDevice("pad2", ControllerType.SwitchPro, ConnectionType.Bluetooth);

    private readonly FakeEngine _engine = new();
    private readonly FakeUiThread _ui = new();
    private readonly FakeClock _clock = new();
    private readonly FakeUrlLauncher _launcher = new();
    private readonly FakeProfileCatalog _profiles = new(new ProfileChoice("default-ps4", "PlayStation default"), new ProfileChoice("custom", "My profile"));
    private DashboardViewModel? _viewModel;

    private DashboardViewModel ViewModel => _viewModel ??= new DashboardViewModel(_engine, _profiles, _ui, _clock, _launcher);

    public void Dispose() => _viewModel?.Dispose();

    private void Plug(params (DeviceInfo Device, int? Slot)[] devices)
    {
        _engine.Devices = [.. devices.Select(d => d.Device)];
        _engine.Slots =
        [
            .. Enumerable.Range(0, 4).Select(i =>
            {
                var match = devices.FirstOrDefault(d => d.Slot == i);
                return match.Device is null ? new SlotStatus(i, null, null, null, SlotState.Empty, null) : FakeEngine.Active(i, match.Device);
            }),
        ];
    }

    [Fact]
    public void Starts_with_four_empty_cards_and_an_empty_device_list()
    {
        ViewModel.Cards.Should().HaveCount(4);
        ViewModel.Cards.Should().OnlyContain(card => card.IsEmpty && card.StatusText == "Empty");
        ViewModel.Cards.Select(card => card.Title).Should().Equal("Player 1", "Player 2", "Player 3", "Player 4");
        ViewModel.Cards.Select(card => card.SlotColor).Distinct().Should().HaveCount(4);
        ViewModel.HasNoDevices.Should().BeTrue();
        ViewModel.HasDevices.Should().BeFalse();
    }

    [Fact]
    public void Refresh_fills_the_card_and_the_device_list()
    {
        Plug((Pad1, 0), (Pad2, null));
        _engine.Live[Pad1.Id] = new InputState { Battery = new BatteryInfo(BatteryState.Discharging, 80) };

        ViewModel.Refresh();

        var card = ViewModel.Cards[0];
        card.IsEmpty.Should().BeFalse();
        card.IsOccupied.Should().BeTrue();
        card.DeviceName.Should().Be("pad1");
        card.TypeText.Should().Be("PS4");
        card.ConnectionText.Should().Be("USB");
        card.BatteryText.Should().Be("80%");
        card.XInputText.Should().Be("XInput 1");
        card.ProfileText.Should().Be("Profile: PlayStation default");
        card.StatusText.Should().Be("Active");
        card.StatusColor.Should().Be("#22C55E");

        ViewModel.HasDevices.Should().BeTrue();
        ViewModel.HasNoDevices.Should().BeFalse();
        ViewModel.Devices.Select(d => d.Name).Should().Equal("pad1", "pad2");
        ViewModel.Devices[0].AssignmentText.Should().Be("On Player 1");
        ViewModel.Devices[1].AssignmentText.Should().Be("Not assigned");
        ViewModel.Devices[1].DetailText.Should().Be("Switch - Bluetooth");
    }

    [Fact]
    public void A_waiting_slot_keeps_its_device_details_and_turns_yellow()
    {
        Plug((Pad1, 0));
        ViewModel.Refresh();

        _engine.Devices = [];
        _engine.Slots = [new SlotStatus(0, Pad1.Id, Pad1.Name, "default-ps4", SlotState.Waiting, 0), .. _engine.Slots.Skip(1)];
        ViewModel.Refresh();

        ViewModel.Cards[0].StatusText.Should().Be("Waiting for reconnect");
        ViewModel.Cards[0].StatusColor.Should().Be("#EAB308");
        ViewModel.Cards[0].TypeText.Should().Be("PS4");
        ViewModel.Devices.Should().BeEmpty();
    }

    [Fact]
    public void Unassigning_a_card_calls_the_engine_only_while_it_holds_a_device()
    {
        Plug((Pad1, 1));
        ViewModel.Refresh();

        ViewModel.Cards[0].UnassignCommand.CanExecute(null).Should().BeFalse();
        ViewModel.Cards[1].UnassignCommand.CanExecute(null).Should().BeTrue();
        ViewModel.Cards[1].UnassignCommand.Execute(null);

        _engine.Calls.Should().Equal("Unassign:1");
    }

    [Fact]
    public void Identify_goes_to_the_engine()
    {
        Plug((Pad1, 2));
        ViewModel.Refresh();

        ViewModel.Cards[2].IdentifyCommand.Execute(null);

        _engine.Calls.Should().Equal("Identify:2");
    }

    [Fact]
    public void Dropping_a_device_assigns_it()
    {
        ViewModel.AssignDevice(Pad2.Id, 3);

        _engine.Calls.Should().Equal("Assign:pad2:3");
    }

    [Fact]
    public void The_device_context_menu_offers_every_player()
    {
        Plug((Pad1, null));
        ViewModel.Refresh();

        var options = ViewModel.Devices[0].AssignOptions;
        options.Select(o => o.Header).Should().Equal("Assign to Player 1", "Assign to Player 2", "Assign to Player 3", "Assign to Player 4");
        options[2].Command.Execute(null);

        _engine.Calls.Should().Equal("Assign:pad1:2");
    }

    [Fact]
    public void Changing_the_profile_marks_the_current_one_and_calls_the_engine()
    {
        Plug((Pad1, 0));
        ViewModel.Refresh();

        var options = ViewModel.Cards[0].ProfileOptions;
        options.Select(o => o.Header).Should().Equal("PlayStation default", "My profile");
        options.Select(o => o.IsChecked).Should().Equal(true, false);
        options[1].Command.Execute(null);

        _engine.Calls.Should().Equal("SetProfile:0:custom");
    }

    [Fact]
    public void Toggling_pause_calls_the_engine()
    {
        ViewModel.IsPaused = true;

        _engine.Calls.Should().Equal("Pause:True");
    }

    [Fact]
    public void A_refresh_copies_the_pause_state_without_sending_it_back()
    {
        _engine.IsPaused = true;

        ViewModel.Refresh();

        ViewModel.IsPaused.Should().BeTrue();
        _engine.Calls.Should().BeEmpty();
    }

    [Fact]
    public void Mode_buttons_follow_the_engine_and_switch_it()
    {
        ViewModel.IsModeA.Should().BeTrue();
        ViewModel.IsModeB.Should().BeFalse();

        ViewModel.SetModeCommand.Execute(KeyboardMode.KeyboardAsController);
        _engine.Calls.Should().Equal("SetMode:KeyboardAsController");

        _engine.Mode = KeyboardMode.KeyboardAsController;
        ViewModel.Refresh();
        ViewModel.IsModeB.Should().BeTrue();
        ViewModel.IsModeA.Should().BeFalse();
    }

    [Fact]
    public void The_keyboard_style_follows_the_engine_and_switches_it()
    {
        ViewModel.IsStyleXbox.Should().BeTrue();
        ViewModel.IsStyleKeyboard.Should().BeFalse();

        ViewModel.SetKeyboardStyleCommand.Execute(KeyboardInputStyle.NormalKeyboard);
        _engine.Calls.Should().Equal("SetKeyboardStyle:NormalKeyboard");

        _engine.KeyboardStyle = KeyboardInputStyle.NormalKeyboard;
        ViewModel.Refresh();
        ViewModel.IsStyleKeyboard.Should().BeTrue();
        ViewModel.IsStyleXbox.Should().BeFalse();
    }

    [Fact]
    public void Missing_drivers_show_red_chips_and_banners()
    {
        _engine.DriverStatus = new DriverStatus(ViGEmAvailable: false, HidHideAvailable: true);

        ViewModel.Refresh();

        ViewModel.ShowViGEmBanner.Should().BeTrue();
        ViewModel.ShowHidHideBanner.Should().BeFalse();
        ViewModel.ViGEmChipText.Should().Contain("✗");
        ViewModel.HidHideChipText.Should().Contain("✓");
        ViewModel.ViGEmChipAutomationName.Should().Be("ViGEmBus is missing");
    }

    [Fact]
    public void Installed_drivers_show_no_banner()
    {
        ViewModel.Refresh();

        ViewModel.ShowViGEmBanner.Should().BeFalse();
        ViewModel.ShowHidHideBanner.Should().BeFalse();
    }

    [Fact]
    public void The_banner_buttons_open_the_release_pages()
    {
        ViewModel.OpenViGEmPageCommand.Execute(null);
        ViewModel.OpenHidHidePageCommand.Execute(null);

        _launcher.Opened.Should().Equal(DashboardViewModel.ViGEmReleasesUrl, DashboardViewModel.HidHideReleasesUrl);
    }

    [Fact]
    public void Engine_changes_are_folded_into_one_refresh_on_the_ui_thread()
    {
        _ = ViewModel;
        Plug((Pad1, 0));

        _engine.RaiseChanged();
        _engine.RaiseChanged();
        _engine.RaiseChanged();

        _ui.Posted.Should().ContainSingle();
        ViewModel.Cards[0].IsEmpty.Should().BeTrue("nothing runs until the UI thread picks the work up");

        _ui.RunAll();

        ViewModel.Cards[0].DeviceName.Should().Be("pad1");
    }

    [Fact]
    public void Refreshes_are_at_least_50_ms_apart()
    {
        _ = ViewModel;

        _engine.RaiseChanged();
        _ui.RunAll();
        _clock.Advance(TimeSpan.FromMilliseconds(20));
        _engine.RaiseChanged();

        _ui.Posted.Should().ContainSingle().Which.Delay.TotalMilliseconds.Should().BeApproximately(30, 0.001);

        _ui.RunAll();
        _clock.Advance(TimeSpan.FromMilliseconds(80));
        _engine.RaiseChanged();

        _ui.Posted.Should().ContainSingle().Which.Delay.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void A_change_during_a_refresh_schedules_the_next_one()
    {
        _ = ViewModel;
        _engine.RaiseChanged();
        _ui.RunAll();

        _engine.RaiseChanged();

        _ui.Posted.Should().HaveCount(1);
    }

    [Fact]
    public void Battery_is_polled_on_a_slow_timer()
    {
        Plug((Pad1, 0));
        _ = ViewModel;
        _ui.Timers.Should().ContainSingle().Which.Interval.Should().Be(TimeSpan.FromSeconds(5));

        _engine.Live[Pad1.Id] = new InputState { Battery = new BatteryInfo(BatteryState.Charging, 40) };
        _ui.Timers[0].Action();

        ViewModel.Cards[0].BatteryText.Should().Be("40% charging");
    }

    [Fact]
    public void Keyboard_cards_show_no_battery()
    {
        var keyboard = FakeEngine.MakeDevice("keyboard-mouse", ControllerType.Keyboard, ConnectionType.Unknown);
        Plug((keyboard, 0));
        _engine.Live[keyboard.Id] = new InputState { Battery = new BatteryInfo(BatteryState.Discharging, 50) };

        ViewModel.Refresh();

        ViewModel.Cards[0].BatteryText.Should().BeEmpty();
        ViewModel.Cards[0].TypeText.Should().Be("Keyboard");
    }

    [Fact]
    public void Dispose_stops_listening_to_the_engine()
    {
        _ = ViewModel;
        _engine.ChangedSubscribers.Should().Be(1);

        ViewModel.Dispose();

        _engine.ChangedSubscribers.Should().Be(0);
        _ui.Timers.Should().BeEmpty();
    }
}
