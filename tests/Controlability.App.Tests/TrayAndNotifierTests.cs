using Controlability.App.ViewModels;
using Controlability.Core.Models;
using Controlability.Core.Settings;
using Controlability.Engine;

namespace Controlability.App.Tests;

public sealed class TrayViewModelTests : IDisposable
{
    private readonly FakeEngine _engine = new();
    private readonly FakeUiThread _ui = new();
    private readonly FakeClock _clock = new();
    private readonly FakeShell _shell = new();
    private readonly TrayViewModel _viewModel;

    public TrayViewModelTests()
    {
        _viewModel = new TrayViewModel(_engine, _shell, _ui, _clock);
    }

    public void Dispose() => _viewModel.Dispose();

    [Fact]
    public void Starts_normal_with_four_empty_slot_lines()
    {
        _viewModel.State.Should().Be(TrayState.Normal);
        _viewModel.SlotLines.Should().Equal("Player 1: empty", "Player 2: empty", "Player 3: empty", "Player 4: empty");
    }

    [Fact]
    public void Slot_lines_name_the_devices()
    {
        var pad = FakeEngine.MakeDevice("DS4");
        _engine.Slots = [new SlotStatus(0, null, null, null, SlotState.Empty, null), FakeEngine.Active(1, pad), .. _engine.Slots.Skip(2)];

        _viewModel.Refresh();

        _viewModel.SlotLines[1].Should().Be("Player 2: DS4");
    }

    [Fact]
    public void Paused_and_missing_driver_change_the_icon_state()
    {
        _engine.IsPaused = true;
        _viewModel.Refresh();
        _viewModel.State.Should().Be(TrayState.Paused);
        _viewModel.IsPaused.Should().BeTrue();

        _engine.DriverStatus = new DriverStatus(ViGEmAvailable: false, HidHideAvailable: true);
        _viewModel.Refresh();
        _viewModel.State.Should().Be(TrayState.Error, "a driver problem outranks pause");

        _engine.DriverStatus = new DriverStatus(ViGEmAvailable: true, HidHideAvailable: false);
        _engine.IsPaused = false;
        _viewModel.Refresh();
        _viewModel.State.Should().Be(TrayState.Normal, "HidHide is optional");
    }

    [Fact]
    public void Commands_reach_the_engine_and_the_shell()
    {
        _viewModel.TogglePauseCommand.Execute(null);
        _viewModel.SetModeCommand.Execute(KeyboardMode.KeyboardAsController);
        _viewModel.OpenCommand.Execute(null);
        _viewModel.ExitCommand.Execute(null);

        _engine.Calls.Should().Equal("Pause:True", "SetMode:KeyboardAsController");
        _shell.ShowCount.Should().Be(1);
        _shell.ExitCount.Should().Be(1);
    }

    [Fact]
    public void Engine_changes_raise_Updated_through_the_throttle()
    {
        var updates = 0;
        _viewModel.Updated += () => updates++;

        _engine.RaiseChanged();
        _engine.RaiseChanged();
        _ui.RunAll();

        updates.Should().Be(1);
    }
}

public sealed class ConnectionNotifierTests : IDisposable
{
    private readonly FakeEngine _engine = new();
    private readonly FakeUiThread _ui = new();
    private readonly FakeClock _clock = new();
    private readonly FakeNotifier _notifier = new();
    private ConnectionNotifier? _notifierUnderTest;

    private ConnectionNotifier Create() => _notifierUnderTest ??= new ConnectionNotifier(_engine, _notifier, _ui, _clock);

    public void Dispose() => _notifierUnderTest?.Dispose();

    private void Change(Action change)
    {
        change();
        _engine.RaiseChanged();
        _ui.RunAll();
        _clock.Advance(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void A_new_controller_says_which_player_it_became()
    {
        Create();
        var pad = FakeEngine.MakeDevice("DS4");

        Change(() =>
        {
            _engine.Devices = [pad];
            _engine.Slots = [new SlotStatus(0, null, null, null, SlotState.Empty, null), FakeEngine.Active(1, pad), .. _engine.Slots.Skip(2)];
        });

        _notifier.Notifications.Should().ContainSingle().Which.Message.Should().Be("DS4 connected as Player 2");
    }

    [Fact]
    public void A_controller_without_a_slot_is_announced_without_a_player()
    {
        Create();

        Change(() => _engine.Devices = [FakeEngine.MakeDevice("DS4")]);

        _notifier.Notifications.Should().ContainSingle().Which.Message.Should().Be("DS4 connected");
    }

    [Fact]
    public void Unplugging_is_announced_once()
    {
        Create();
        var pad = FakeEngine.MakeDevice("DS4");
        Change(() => _engine.Devices = [pad]);
        _notifier.Notifications.Clear();

        Change(() => _engine.Devices = []);
        Change(() => _engine.Devices = []);

        _notifier.Notifications.Should().ContainSingle().Which.Message.Should().Be("DS4 disconnected");
    }

    [Fact]
    public void Devices_that_were_there_at_the_start_are_not_announced()
    {
        _engine.Devices = [FakeEngine.MakeDevice("DS4")];
        Create();

        Change(() => { });

        _notifier.Notifications.Should().BeEmpty();
    }

    [Fact]
    public void The_keyboard_pad_is_never_announced()
    {
        Create();

        Change(() => _engine.Devices = [FakeEngine.MakeDevice("keyboard-mouse", ControllerType.Keyboard, ConnectionType.Unknown)]);
        Change(() => _engine.Devices = []);

        _notifier.Notifications.Should().BeEmpty();
    }
}
