using Controlability.Core.Models;
using Controlability.Platform.HidHide;

namespace Controlability.Engine.Tests.HidHide;

public sealed class HidHideServiceTests : IDisposable
{
    private const string SelfPath = @"C:\app\Controlability.exe";
    private const string UsbHidPath = @"\\?\HID#VID_054C&PID_09CC&MI_03#9&343547ec&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string UsbInstancePath = @"HID\VID_054C&PID_09CC&MI_03\9&343547ec&0&0000";
    private const string SecondHidPath = @"\\?\HID#VID_054C&PID_09CC&MI_03#second#{4d1e55b2-f16f-11cf-88cb-001111000030}";
    private const string SecondInstancePath = @"HID\VID_054C&PID_09CC&MI_03\second";

    private static readonly DeviceInfo Pad = new(
        new DeviceId("pad1"), "PS4 Controller", 0x054C, 0x09CC, "pad1", UsbHidPath, ConnectionType.Usb, ControllerType.PS4, DeviceCapabilities.None);

    private static readonly DeviceInfo Pad2 = Pad with { Id = new DeviceId("pad2"), Serial = "pad2", HidPath = SecondHidPath };

    private readonly string _folder = Directory.CreateTempSubdirectory("controlability-hidhide-tests").FullName;
    private readonly FakeHidHideCli _cli = new();
    private readonly List<string> _log = [];

    public HidHideServiceTests()
    {
        _cli.Gaming.Add(new HidDevice(UsbHidPath.ToLowerInvariant(), UsbInstancePath, null, null, Present: true, Gaming: true));
        _cli.Gaming.Add(new HidDevice(SecondHidPath.ToLowerInvariant(), SecondInstancePath, null, null, Present: true, Gaming: true));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string JournalFile => Path.Combine(_folder, "hidden.json");

    private HidHideService Service(IHidHideCli? cli = null) => new(cli ?? _cli, new HiddenJournal(JournalFile), SelfPath, _log.Add);

    [Fact]
    public void Without_the_cli_nothing_is_available_and_nothing_throws()
    {
        var service = new HidHideService(null, new HiddenJournal(JournalFile), SelfPath);

        service.IsAvailable.Should().BeFalse();
        service.Hide(Pad);
        service.Unhide(Pad);
        service.UnhideAll();
        service.WhitelistSelf();
        service.GetStatus().Should().BeNull();
    }

    [Fact]
    public void Hide_whitelists_us_first_journals_the_device_hides_it_and_turns_cloaking_on()
    {
        Service().Hide(Pad);

        _cli.Calls.Should().Equal($"app-reg {SelfPath}", $"hide {UsbInstancePath}", "cloak-on");
        new HiddenJournal(JournalFile).Load().Should().Equal(new HiddenEntry("pad1", UsbInstancePath));
    }

    [Fact]
    public void The_journal_entry_exists_before_the_cli_hides_anything()
    {
        var seen = new List<HiddenEntry>();
        var cli = new JournalPeekingCli(_cli, () => seen.AddRange(new HiddenJournal(JournalFile).Load()));

        Service(cli).Hide(Pad);

        seen.Should().Equal(new HiddenEntry("pad1", UsbInstancePath));
    }

    [Fact]
    public void Whitelisting_happens_once_and_cloaking_is_left_alone_when_already_on()
    {
        _cli.Cloak = true;
        var service = Service();

        service.Hide(Pad);
        service.Hide(Pad2);

        _cli.Calls.Count(c => c.StartsWith("app-reg")).Should().Be(1);
        _cli.Calls.Should().NotContain("cloak-on");
    }

    [Fact]
    public void Unhide_unhides_the_device_and_removes_the_journal_entry()
    {
        var service = Service();
        service.Hide(Pad);

        service.Unhide(Pad);

        _cli.Hidden.Should().BeEmpty();
        File.Exists(JournalFile).Should().BeFalse();
    }

    [Fact]
    public void A_device_that_was_hidden_before_we_started_is_never_touched()
    {
        _cli.Hidden.Add(UsbInstancePath);
        var service = Service();

        service.Hide(Pad);
        service.Unhide(Pad);
        service.UnhideAll();

        _cli.Hidden.Should().Equal(UsbInstancePath);
        _cli.Calls.Should().NotContain(c => c.StartsWith("hide ") || c.StartsWith("unhide "));
        new HiddenJournal(JournalFile).Load().Should().BeEmpty();
    }

    [Fact]
    public void Unhide_only_affects_the_given_device()
    {
        var service = Service();
        service.Hide(Pad);
        service.Hide(Pad2);

        service.Unhide(Pad);

        _cli.Hidden.Should().Equal(SecondInstancePath);
        new HiddenJournal(JournalFile).Load().Should().ContainSingle().Which.DeviceId.Should().Be("pad2");
    }

    [Fact]
    public void A_failed_hide_leaves_no_journal_entry_and_does_not_throw()
    {
        _cli.FailHide = true;

        Service().Hide(Pad);

        new HiddenJournal(JournalFile).Load().Should().BeEmpty();
        _log.Should().Contain(m => m.Contains("could not hide"));
    }

    [Fact]
    public void A_device_that_cannot_be_resolved_stays_visible()
    {
        var unknown = Pad with { HidPath = null, Vid = 0x1234 };

        Service().Hide(unknown);

        _cli.Hidden.Should().BeEmpty();
        _log.Should().Contain(m => m.Contains("instance path"));
    }

    [Fact]
    public void UnhideAll_gives_back_everything_in_the_journal()
    {
        var service = Service();
        service.Hide(Pad);

        service.UnhideAll();

        _cli.Hidden.Should().BeEmpty();
        service.HiddenByUs.Should().BeEmpty();
    }

    [Fact]
    public void Recovery_after_a_crash_unhides_what_the_dead_run_left_in_the_journal()
    {
        Service().Hide(Pad); // the process "dies" here: no Unhide, the journal stays on disk
        _cli.Hidden.Should().Equal(UsbInstancePath);

        var nextRun = Service();
        nextRun.RecoverFromJournal().Should().Be(1);

        _cli.Hidden.Should().BeEmpty();
        File.Exists(JournalFile).Should().BeFalse();
        nextRun.RecoverFromJournal().Should().Be(0);
    }

    [Fact]
    public void An_unhide_that_fails_keeps_the_entry_for_the_next_start()
    {
        var service = Service();
        service.Hide(Pad);
        _cli.FailUnhide = true;

        service.UnhideAll();

        service.HiddenByUs.Should().ContainSingle();
        _cli.FailUnhide = false;
        service.RecoverFromJournal();
        _cli.Hidden.Should().BeEmpty();
    }

    [Fact]
    public void Whitelist_is_skipped_when_we_are_already_registered_in_any_letter_case()
    {
        _cli.Apps.Add(SelfPath.ToUpperInvariant());

        Service().WhitelistSelf();

        _cli.Calls.Should().BeEmpty();
    }

    /// <summary>Runs a callback right before <see cref="Hide"/> reaches the inner fake.</summary>
    private sealed class JournalPeekingCli(FakeHidHideCli inner, Action beforeHide) : IHidHideCli
    {
        public string ExePath => inner.ExePath;

        public string Version() => inner.Version();

        public bool IsCloakOn() => inner.IsCloakOn();

        public void SetCloak(bool on) => inner.SetCloak(on);

        public bool IsInverseOn() => inner.IsInverseOn();

        public IReadOnlyList<string> GetApps() => inner.GetApps();

        public void RegisterApp(string exePath) => inner.RegisterApp(exePath);

        public IReadOnlyList<string> GetHiddenDevices() => inner.GetHiddenDevices();

        public void Hide(string instancePath)
        {
            beforeHide();
            inner.Hide(instancePath);
        }

        public void Unhide(string instancePath) => inner.Unhide(instancePath);

        public IReadOnlyList<HidDevice> GetGamingDevices() => inner.GetGamingDevices();
    }
}
