using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;
using Controlability.Engine.Tests.Runtime;

namespace Controlability.Engine.Tests.HidHide;

public sealed class EngineHidingTests : IDisposable
{
    private static readonly DeviceInfo Pad1 = FakeInputSource.MakeDevice("pad1");
    private static readonly DeviceInfo Pad2 = FakeInputSource.MakeDevice("pad2");

    private readonly string _profilesFolder = Directory.CreateTempSubdirectory("controlability-hiding-tests").FullName;
    private readonly List<string> _events = [];
    private readonly FakeClock _clock = new();
    private readonly FakeInputSource _source = new();
    private readonly FakeOutputSink _sink = new();
    private readonly FakeDeviceHider _hider;
    private Settings _settings = new();
    private Controlability.Engine.Engine? _engine;

    public EngineHidingTests()
    {
        _hider = new FakeDeviceHider(_events);
        _sink.OnConnect = slot => _events.Add($"connect {slot}");
    }

    private Controlability.Engine.Engine Engine => _engine ??= new Controlability.Engine.Engine(
        [_source], _sink, new ProfileStore(_profilesFolder), _clock, _settings, _hider);

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

    [Fact]
    public void A_device_is_hidden_before_its_virtual_pad_connects()
    {
        Plug(Pad1);
        Plug(Pad2);

        Engine.Tick();

        _events.Should().Equal("hide pad1", "hide pad2", "connect 0", "connect 1");
    }

    [Fact]
    public void Unassigning_unhides_the_device_after_removing_its_pad()
    {
        _settings = new Settings { SlotPolicy = SlotPolicy.Manual };
        Plug(Pad1);
        Engine.Tick();
        Engine.Assign(Pad1.Id, 0);
        Engine.Tick();
        _events.Clear();

        Engine.Unassign(0);
        Engine.Tick();

        _events.Should().Equal("unhide pad1");
        _sink.Disconnected.Should().Equal(0);
    }

    [Fact]
    public void Pausing_unhides_and_resuming_hides_again()
    {
        Plug(Pad1);
        Engine.Tick();
        _events.Clear();

        Engine.Pause(true);
        Engine.Tick();
        Engine.Pause(false);
        Engine.Tick();

        _events.Should().Equal("unhide pad1", "hide pad1");
    }

    [Fact]
    public void Pausing_keeps_the_device_hidden_when_the_setting_is_off()
    {
        _settings = new Settings { UnhideWhilePaused = false };
        Plug(Pad1);
        Engine.Tick();
        _events.Clear();

        Engine.Pause(true);
        Engine.Tick();

        _events.Should().BeEmpty();
    }

    [Fact]
    public void A_device_assigned_while_paused_is_not_hidden_until_resume()
    {
        Engine.Pause(true);
        Engine.Tick();
        _events.Clear();

        Plug(Pad1);
        Engine.Tick();
        _events.Should().Equal("connect 0");

        Engine.Pause(false);
        Engine.Tick();
        _events.Should().Equal("connect 0", "hide pad1");
    }

    [Fact]
    public void Stopping_the_engine_unhides_every_device()
    {
        Plug(Pad1);
        Engine.Start();
        SpinWait.SpinUntil(() => Engine.Slots[0].State == SlotState.Active, TimeSpan.FromSeconds(5)).Should().BeTrue();

        Engine.Stop();

        _events.Should().Contain("unhide pad1");
        _events.IndexOf("unhide pad1").Should().BeGreaterThan(_events.IndexOf("connect 0"));
    }

    [Fact]
    public void Dispose_calls_unhide_all_as_a_safety_net()
    {
        Plug(Pad1);
        Engine.Tick();

        Engine.Dispose();

        _events.Should().EndWith("unhide-all");
    }

    [Fact]
    public void Start_whitelists_the_app()
    {
        Engine.Start();
        Engine.Stop();

        _hider.WhitelistCalls.Should().Be(1);
    }

    [Fact]
    public void Nothing_is_hidden_when_the_setting_is_off()
    {
        _settings = new Settings { HidHideEnabled = false };
        Plug(Pad1);

        Engine.Tick();

        _events.Should().Equal("connect 0");
    }

    [Fact]
    public void Nothing_is_hidden_when_hidhide_is_not_installed()
    {
        _hider.IsAvailable = false;
        Plug(Pad1);

        Engine.Tick();

        _events.Should().Equal("connect 0");
        Engine.DriverStatus.HidHideAvailable.Should().BeFalse();
    }
}
