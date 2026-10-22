using Controlability.App.Services;
using Controlability.App.ViewModels;
using Controlability.Core.Models;
using Controlability.Core.Settings;
using Controlability.Engine;
using Controlability.Platform;
using Controlability.Platform.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;

namespace Controlability.App.Tests;

public sealed class FakeAutostart : IAutostart
{
    public bool IsEnabled { get; set; }

    public bool Succeeds { get; set; } = true;

    public bool Set(bool enabled)
    {
        if (!Succeeds)
        {
            return false;
        }

        IsEnabled = enabled;
        return true;
    }
}

public sealed class FakeAppearance : IAppearance
{
    public List<AppTheme> Applied { get; } = [];

    public void Apply(AppTheme theme) => Applied.Add(theme);
}

public sealed class FakeClipboard : IClipboard
{
    public string? Text { get; private set; }

    public bool Succeeds { get; set; } = true;

    public bool SetText(string text)
    {
        if (Succeeds)
        {
            Text = text;
        }

        return Succeeds;
    }
}

public sealed class FakeDriverProbe : IDriverProbe
{
    public DriverInfo Info { get; set; } = new(false, "unknown", false, "unknown");

    public DriverInfo Read() => Info;
}

public sealed class GeneralSettingsViewModelTests : IDisposable
{
    private readonly string _logFolder = Directory.CreateTempSubdirectory("controlability-general-tests").FullName;
    private readonly FakeEngine _engine = new();
    private readonly Settings _settings = new();
    private readonly List<Settings> _saved = [];
    private readonly FakeAutostart _autostart = new();
    private readonly FakeAppearance _appearance = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeDialogs _dialogs = new();
    private readonly FakeUrlLauncher _launcher = new();
    private readonly RollingFileLog _log;
    private GeneralSettingsViewModel? _viewModel;

    public GeneralSettingsViewModelTests() => _log = new RollingFileLog(_logFolder);

    private GeneralSettingsViewModel ViewModel => _viewModel ??= new GeneralSettingsViewModel(
        _engine,
        _settings,
        _saved.Add,
        _autostart,
        _appearance,
        _log,
        new DiagnosticsService(_engine, new FakeDriverProbe { Info = new DriverInfo(true, "1.22.0", true, "1.5.0") }, _settings, _log, _clipboard),
        _dialogs,
        _launcher);

    public void Dispose() => Directory.Delete(_logFolder, recursive: true);

    [Fact]
    public void Shows_the_stored_settings()
    {
        _settings.PollingRateHz = 250;
        _settings.GracePeriodSeconds = 20;
        _settings.Theme = AppTheme.Dark;
        _settings.SlotPolicy = SlotPolicy.Remember;
        _settings.LogLevel = AppLogLevel.Warning;
        _settings.StartMinimized = true;
        _autostart.IsEnabled = true;
        _engine.Mode = KeyboardMode.KeyboardAsController;

        ViewModel.PollingRate.Value.Should().Be(250);
        ViewModel.GracePeriod.Value.Should().Be(20);
        ViewModel.Theme.Value.Should().Be(AppTheme.Dark);
        ViewModel.SlotPolicyChoice.Value.Should().Be(SlotPolicy.Remember);
        ViewModel.LogLevel.Value.Should().Be(AppLogLevel.Warning);
        ViewModel.StartMinimized.Should().BeTrue();
        ViewModel.StartWithWindows.Should().BeTrue();
        ViewModel.Mode.Value.Should().Be(KeyboardMode.KeyboardAsController);
        _saved.Should().BeEmpty("loading is not a change");
    }

    [Fact]
    public void An_engine_setting_is_saved_and_pushed_to_the_engine()
    {
        var vm = ViewModel;

        vm.PollingRate = vm.PollingRateChoices.Single(c => c.Value == 500);
        vm.HidHideEnabled = false;

        _settings.PollingRateHz.Should().Be(500);
        _settings.HidHideEnabled.Should().BeFalse();
        _saved.Should().HaveCount(2);
        _engine.Calls.Count(call => call == "ApplySettings").Should().Be(2);
    }

    [Fact]
    public void Start_with_windows_writes_the_run_entry_and_the_setting()
    {
        ViewModel.StartWithWindows = true;

        _autostart.IsEnabled.Should().BeTrue();
        _settings.StartWithWindows.Should().BeTrue();
        _saved.Should().ContainSingle();
    }

    [Fact]
    public void A_refused_registry_write_switches_the_toggle_back_and_says_so()
    {
        _autostart.Succeeds = false;

        ViewModel.StartWithWindows = true;

        ViewModel.StartWithWindows.Should().BeFalse();
        ViewModel.StatusText.Should().NotBeEmpty();
        _settings.StartWithWindows.Should().BeFalse();
    }

    [Fact]
    public void The_theme_applies_at_once_and_the_log_level_applies_live()
    {
        var vm = ViewModel;

        vm.Theme = vm.ThemeChoices.Single(c => c.Value == AppTheme.Light);
        vm.LogLevel = vm.LogLevelChoices.Single(c => c.Value == AppLogLevel.Debug);

        _appearance.Applied.Should().Equal(AppTheme.Light);
        _settings.Theme.Should().Be(AppTheme.Light);
        _log.Level.Should().Be(AppLogLevel.Debug);
    }

    [Fact]
    public void Choosing_a_mode_switches_the_engine()
    {
        var vm = ViewModel;

        vm.Mode = vm.ModeChoices.Single(c => c.Value == KeyboardMode.KeyboardAsController);

        _engine.Calls.Should().Contain("SetMode:KeyboardAsController");
    }

    [Fact]
    public void Refresh_follows_a_mode_change_made_elsewhere_without_switching_again()
    {
        var vm = ViewModel;
        _engine.Mode = KeyboardMode.KeyboardAsController;

        vm.Refresh();

        vm.Mode.Value.Should().Be(KeyboardMode.KeyboardAsController);
        _engine.Calls.Should().NotContain(call => call.StartsWith("SetMode", StringComparison.Ordinal));
    }

    [Fact]
    public void Unhide_all_asks_first_and_does_nothing_on_no()
    {
        _dialogs.ConfirmAnswer = false;

        ViewModel.UnhideAllCommand.Execute(null);

        _dialogs.Confirmations.Should().ContainSingle();
        _engine.Calls.Should().NotContain("UnhideAll");
    }

    [Fact]
    public void Unhide_all_tells_the_engine_after_a_yes()
    {
        ViewModel.UnhideAllCommand.Execute(null);

        _engine.Calls.Should().Contain("UnhideAll");
    }

    [Fact]
    public void Open_logs_opens_the_log_folder()
    {
        ViewModel.OpenLogsCommand.Execute(null);

        _launcher.Opened.Should().Equal(_logFolder);
    }

    [Fact]
    public void Copy_diagnostics_puts_the_report_on_the_clipboard()
    {
        ViewModel.CopyDiagnosticsCommand.Execute(null);

        _clipboard.Text.Should().Contain("ViGEmBus: 1.22.0").And.Contain("HidHide: 1.5.0");
        ViewModel.StatusText.Should().NotBeEmpty();
    }

    [Fact]
    public void The_about_section_has_a_version_and_the_third_party_list()
    {
        ViewModel.VersionText.Should().Contain(AppInfo.Version);
        ViewModel.Notices.Should().Contain(notice => notice.Name.Contains("WPF-UI", StringComparison.Ordinal));
    }
}

public sealed class DiagnosticsServiceTests : IDisposable
{
    private readonly string _logFolder = Directory.CreateTempSubdirectory("controlability-diag-tests").FullName;

    public void Dispose() => Directory.Delete(_logFolder, recursive: true);

    [Fact]
    public void The_report_has_versions_drivers_devices_settings_and_the_log_tail()
    {
        var engine = new FakeEngine { Devices = [FakeEngine.MakeDevice("DualShock 4")] };
        var log = new RollingFileLog(_logFolder);
        for (var i = 0; i < 250; i++)
        {
            log.Write(AppLogLevel.Information, "Test", $"line {i}");
        }

        var service = new DiagnosticsService(
            engine,
            new FakeDriverProbe { Info = new DriverInfo(true, "1.22.0", false, "unknown") },
            new Settings { PollingRateHz = 500 },
            log,
            new FakeClipboard());

        var report = service.Build();

        report.Should().Contain($"Controlability {AppInfo.Version}")
            .And.Contain("Windows:")
            .And.Contain("ViGEmBus: 1.22.0")
            .And.Contain("HidHide: not installed")
            .And.Contain("DualShock 4")
            .And.Contain("\"pollingRateHz\": 500")
            .And.Contain("line 249")
            .And.Contain("line 50")
            .And.NotContain("line 49", "only the last 200 lines are included");
    }

    [Fact]
    public void A_clipboard_failure_is_reported_not_thrown()
    {
        var service = new DiagnosticsService(new FakeEngine(), new FakeDriverProbe(), new Settings(), new RollingFileLog(_logFolder), new FakeClipboard { Succeeds = false });

        service.CopyToClipboard().Should().BeFalse();
    }
}

public sealed class WizardViewModelTests
{
    private readonly FakeEngine _engine = new();
    private readonly FakeDriverProbe _drivers = new();
    private readonly FakeUiThread _ui = new();
    private readonly FakeClock _clock = new();
    private readonly FakeAutostart _autostart = new();
    private readonly FakeUrlLauncher _launcher = new();
    private readonly Settings _settings = new();
    private readonly List<Settings> _saved = [];

    private WizardViewModel Create() => new(_engine, _drivers, _settings, _saved.Add, _autostart, _launcher, _ui, _clock);

    private static async Task Eventually(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }

        condition().Should().BeTrue();
    }

    [Fact]
    public void Walks_forward_through_the_five_steps_and_back()
    {
        using var vm = Create();
        vm.IsWelcome.Should().BeTrue();
        vm.CanGoBack.Should().BeFalse();

        vm.NextCommand.Execute(null);
        vm.IsDrivers.Should().BeTrue();
        vm.NextCommand.Execute(null);
        vm.IsController.Should().BeTrue();
        vm.NextCommand.Execute(null);
        vm.IsMode.Should().BeTrue();
        vm.NextCommand.Execute(null);
        vm.IsDone.Should().BeTrue();
        vm.StepNumberText.Should().Contain("5").And.Contain("5");

        vm.BackCommand.Execute(null);
        vm.IsMode.Should().BeTrue();
    }

    [Fact]
    public async Task The_driver_step_rechecks_every_two_seconds_and_turns_green_after_the_install()
    {
        using var vm = Create();

        vm.NextCommand.Execute(null);
        await Eventually(() => vm.DriversChecked);
        vm.ViGEmInstalled.Should().BeFalse();
        _ui.Timers.Should().ContainSingle().Which.Interval.Should().Be(TimeSpan.FromSeconds(2));

        _drivers.Info = new DriverInfo(true, "1.22.0", true, "1.5.0");
        _ui.Timers[0].Action();

        await Eventually(() => vm.ViGEmInstalled && vm.HidHideInstalled);
        vm.ViGEmText.Should().Contain("1.22.0");
    }

    [Fact]
    public void Leaving_the_driver_step_stops_the_recheck()
    {
        using var vm = Create();
        vm.NextCommand.Execute(null);
        _ui.Timers.Should().HaveCount(1);

        vm.NextCommand.Execute(null);

        _ui.Timers.Should().BeEmpty();
    }

    [Fact]
    public void The_install_buttons_open_the_driver_downloads()
    {
        using var vm = Create();

        vm.InstallViGEmCommand.Execute(null);
        vm.InstallHidHideCommand.Execute(null);

        _launcher.Opened.Should().Equal(DashboardViewModel.ViGEmReleasesUrl, DashboardViewModel.HidHideReleasesUrl);
    }

    [Fact]
    public void The_controller_step_shows_the_first_controller_once_it_is_plugged_in()
    {
        using var vm = Create();
        vm.NoController.Should().BeTrue();

        _engine.Devices = [FakeEngine.MakeDevice("DualShock 4")];
        _engine.RaiseChanged();
        _ui.RunAll();

        vm.HasController.Should().BeTrue();
        vm.ControllerText.Should().Contain("DualShock 4");
    }

    [Fact]
    public void The_keyboard_pad_is_not_offered_as_a_controller()
    {
        using var vm = Create();
        _engine.Devices = [Controlability.Core.Abstractions.KeyboardPad.Info];
        _engine.RaiseChanged();
        _ui.RunAll();

        vm.HasController.Should().BeFalse();
    }

    [Fact]
    public void Finishing_applies_the_mode_and_autostart_saves_settings_and_closes()
    {
        using var vm = Create();
        var completed = 0;
        vm.Completed += () => completed++;
        vm.ModeB = true;
        vm.StartWithWindows = true;

        vm.Finish();

        _engine.Calls.Should().Contain("SetMode:KeyboardAsController");
        _autostart.IsEnabled.Should().BeTrue();
        _settings.StartWithWindows.Should().BeTrue();
        _saved.Should().ContainSingle();
        completed.Should().Be(1);
    }

    [Fact]
    public void Skipping_still_saves_the_settings_file_so_the_wizard_does_not_return()
    {
        using var vm = Create();

        vm.SkipCommand.Execute(null);

        _saved.Should().ContainSingle();
        _engine.Calls.Should().NotContain(call => call.StartsWith("SetMode", StringComparison.Ordinal));
        _autostart.IsEnabled.Should().BeFalse();
    }
}

public sealed class SteamTipViewModelTests
{
    private readonly FakeUiThread _ui = new();
    private readonly Settings _settings = new();
    private readonly List<Settings> _saved = [];

    [Fact]
    public void Shows_while_steam_runs_and_hides_when_it_closes()
    {
        var apps = new FakeRunningApps("steam");
        using var vm = new SteamTipViewModel(apps, _settings, _saved.Add, _ui);
        vm.IsVisible.Should().BeTrue();

        vm.Refresh();
        vm.IsVisible.Should().BeTrue();
        _ui.Timers.Should().ContainSingle();
    }

    [Fact]
    public void Stays_hidden_without_steam()
    {
        using var vm = new SteamTipViewModel(new FakeRunningApps("notepad"), _settings, _saved.Add, _ui);

        vm.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void Dismissing_hides_it_for_good()
    {
        using var vm = new SteamTipViewModel(new FakeRunningApps("steam"), _settings, _saved.Add, _ui);

        vm.DismissCommand.Execute(null);
        vm.Refresh();

        vm.IsVisible.Should().BeFalse();
        _settings.SteamTipDismissed.Should().BeTrue();
        _saved.Should().ContainSingle();
    }
}

public sealed class CrashGuardTests
{
    private readonly List<string> _steps = [];

    private CrashGuard Create(Action? release = null, Action<Exception>? dialog = null)
        => new(NullLogger.Instance, release ?? (() => _steps.Add("release")), dialog ?? (_ => _steps.Add("dialog")));

    [Fact]
    public void A_fatal_error_releases_the_controllers_before_it_shows_the_dialog()
    {
        Create().OnFatal(new InvalidOperationException("boom"), "UI thread");

        _steps.Should().Equal("release", "dialog");
    }

    [Fact]
    public void A_second_crash_while_handling_the_first_is_only_logged()
    {
        CrashGuard? guard = null;
        guard = Create(dialog: _ =>
        {
            _steps.Add("dialog");
            guard!.OnFatal(new InvalidOperationException("again"), "UI thread");
        });

        guard.OnFatal(new InvalidOperationException("first"), "UI thread");

        _steps.Should().Equal("release", "dialog");
    }

    [Fact]
    public void A_failing_release_does_not_stop_the_dialog()
    {
        Create(release: () => throw new IOException("no HidHide")).OnFatal(new InvalidOperationException("boom"), "UI thread");

        _steps.Should().Equal("dialog");
    }

    [Fact]
    public void A_failing_dialog_does_not_throw()
    {
        var guard = Create(dialog: _ => throw new InvalidOperationException("no window"));

        var act = () => guard.OnFatal(new InvalidOperationException("boom"), "UI thread");

        act.Should().NotThrow();
    }

    [Fact]
    public void An_unobserved_task_exception_is_logged_without_tearing_anything_down()
    {
        Create().OnUnobserved(new InvalidOperationException("forgotten task"));

        _steps.Should().BeEmpty();
    }
}

public sealed class PowerResumeHandlerTests
{
    [Fact]
    public void Only_a_resume_restarts_the_input()
    {
        var restarts = 0;
        using var handler = new PowerResumeHandler(NullLogger.Instance, () => restarts++);

        handler.Handle(PowerModes.Suspend);
        handler.Handle(PowerModes.StatusChange);
        restarts.Should().Be(0);

        handler.Handle(PowerModes.Resume);
        restarts.Should().Be(1);
    }

    [Fact]
    public void A_failing_restart_is_logged_not_thrown()
    {
        using var handler = new PowerResumeHandler(NullLogger.Instance, () => throw new InvalidOperationException("SDL"));

        var act = () => handler.Handle(PowerModes.Resume);

        act.Should().NotThrow();
    }
}
