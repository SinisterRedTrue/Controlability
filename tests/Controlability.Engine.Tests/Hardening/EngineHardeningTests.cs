using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;
using Controlability.Engine.Tests.HidHide;
using Controlability.Engine.Tests.Runtime;

namespace Controlability.Engine.Tests.Hardening;

public sealed class EngineHardeningTests : IDisposable
{
    private static readonly DeviceInfo Pad1 = FakeInputSource.MakeDevice("pad1");
    private static readonly DeviceInfo Pad2 = FakeInputSource.MakeDevice("pad2");

    private readonly string _profilesFolder = Directory.CreateTempSubdirectory("controlability-hardening-tests").FullName;
    private readonly List<string> _events = [];
    private readonly FakeClock _clock = new();
    private readonly FakeInputSource _source = new();
    private readonly RestartSpy _restartable = new();
    private readonly FakeOutputSink _sink = new();
    private readonly FakeDeviceHider _hider;
    private Settings _settings = new();
    private Controlability.Engine.Engine? _engine;

    public EngineHardeningTests() => _hider = new FakeDeviceHider(_events);

    private Controlability.Engine.Engine Engine => _engine ??= new Controlability.Engine.Engine(
        [_source, _restartable], _sink, new ProfileStore(_profilesFolder), _clock, _settings, _hider);

    public void Dispose()
    {
        _engine?.Dispose();
        Directory.Delete(_profilesFolder, recursive: true);
    }

    private void Plug(DeviceInfo device)
    {
        _ = Engine; // the engine listens for DeviceAdded from its constructor on
        _source.Plug(device);
    }

    [Fact]
    public void Turning_hiding_off_gives_the_hidden_devices_back()
    {
        Plug(Pad1);
        Engine.Tick();
        Engine.HiddenDevices.Should().Equal(Pad1.Id);
        _events.Clear();

        _settings.HidHideEnabled = false;
        Engine.ApplySettings();
        Engine.Tick();

        Engine.HiddenDevices.Should().BeEmpty();
        _events.Should().Equal("unhide-all");
    }

    [Fact]
    public void Turning_hiding_on_hides_the_devices_that_have_a_slot()
    {
        _settings = new Settings { HidHideEnabled = false };
        Plug(Pad1);
        Engine.Tick();
        _events.Should().NotContain("hide pad1");

        _settings.HidHideEnabled = true;
        Engine.ApplySettings();
        Engine.Tick();

        _events.Should().Contain("hide pad1");
        _hider.WhitelistCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_new_slot_policy_applies_to_the_next_device_without_a_restart()
    {
        _settings = new Settings { SlotPolicy = SlotPolicy.Manual };
        Plug(Pad1);
        Engine.Tick();
        Engine.Slots[0].Device.Should().BeNull();

        _settings.SlotPolicy = SlotPolicy.Auto;
        Engine.ApplySettings();
        Engine.Tick();
        Plug(Pad2);
        Engine.Tick();

        Engine.Slots.Should().Contain(slot => slot.Device == Pad2.Id);
    }

    [Fact]
    public void UnhideAll_on_a_stopped_engine_unhides_at_once_and_clears_the_hidden_list()
    {
        Plug(Pad1);
        Engine.Tick();
        _events.Clear();

        Engine.UnhideAll();

        _events.Should().Equal("unhide-all");
        Engine.HiddenDevices.Should().BeEmpty();
    }

    [Fact]
    public void RestartInput_asks_every_restartable_source_to_restart()
    {
        Engine.RestartInput();
        _restartable.RestartCount.Should().Be(0, "the restart runs on the input thread, on the next tick");

        Engine.Tick();

        _restartable.RestartCount.Should().Be(1);
    }

    [Fact]
    public void The_loop_rate_can_change_while_it_runs()
    {
        using var loop = new InputLoop(() => { }, 1000);

        loop.Rate.Should().Be(1000);
        loop.Rate = 250;

        loop.Rate.Should().Be(250);
    }

    private sealed class RestartSpy : IInputSource, ISourceRestart
    {
        public int RestartCount { get; private set; }

#pragma warning disable CS0067 // Never raised: this source has no devices.
        public event Action<DeviceInfo>? DeviceAdded;

        public event Action<DeviceId>? DeviceRemoved;
#pragma warning restore CS0067

        public void Restart() => RestartCount++;

        public void Poll()
        {
        }

        public bool TryGetState(DeviceId id, out InputState state)
        {
            state = default;
            return false;
        }

        public void SetRumble(DeviceId id, byte low, byte high, int durationMs)
        {
        }

        public void SetLed(DeviceId id, byte r, byte g, byte b)
        {
        }

        public void Dispose()
        {
        }
    }
}
