using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;

namespace Controlability.Engine.Tests.Runtime;

public sealed class EngineTests : IDisposable
{
    private static readonly DeviceInfo Pad1 = FakeInputSource.MakeDevice("pad1");
    private static readonly DeviceInfo Pad2 = FakeInputSource.MakeDevice("pad2");
    private static readonly DeviceInfo Pad3 = FakeInputSource.MakeDevice("pad3");

    private readonly string _profilesFolder = Directory.CreateTempSubdirectory("controlability-engine-tests").FullName;
    private readonly FakeClock _clock = new();
    private readonly FakeInputSource _source = new();
    private readonly FakeOutputSink _sink = new();
    private readonly List<string> _log = [];
    private readonly List<Settings> _saved = [];
    private Settings _settings = new();
    private Controlability.Engine.Engine? _engine;

    private Controlability.Engine.Engine Engine => _engine ??= new Controlability.Engine.Engine(
        [_source],
        _sink,
        new ProfileStore(_profilesFolder),
        _clock,
        _settings,
        saveSettings: _saved.Add,
        log: _log.Add);

    public void Dispose()
    {
        _engine?.Dispose();
        Directory.Delete(_profilesFolder, recursive: true);
    }

    /// <summary>Creates the engine first: it only sees devices announced after it subscribed, like a real source.</summary>
    private void Plug(DeviceInfo device)
    {
        _ = Engine;
        _source.Plug(device);
    }

    private static InputState Pressed(ButtonId buttons, float leftX = 0) => new() { Buttons = buttons, LeftX = leftX };

    [Fact]
    public void AutoAssign_GivesDevicesSlotsInOrder_AndConnectsPadsInSlotOrder()
    {
        Plug(Pad1);
        Plug(Pad2);

        Engine.Tick();

        Engine.Slots[0].Device.Should().Be(Pad1.Id);
        Engine.Slots[1].Device.Should().Be(Pad2.Id);
        Engine.Slots[0].State.Should().Be(SlotState.Active);
        Engine.Slots[2].State.Should().Be(SlotState.Empty);
        _sink.Connected.Should().Equal(0, 1);
        Engine.Devices.Select(d => d.Id).Should().BeEquivalentTo([Pad1.Id, Pad2.Id]);
    }

    [Fact]
    public void RememberPolicy_ConnectsPadsInSlotOrderEvenWhenDevicesArriveReversed()
    {
        _settings = new Settings { SlotPolicy = SlotPolicy.Remember, RememberedSlots = new() { ["pad1"] = 0, ["pad2"] = 1 } };
        Plug(Pad2);
        Plug(Pad1);

        Engine.Tick();

        Engine.Slots[0].Device.Should().Be(Pad1.Id);
        Engine.Slots[1].Device.Should().Be(Pad2.Id);
        _sink.Connected.Should().Equal(0, 1);
    }

    [Fact]
    public void RememberPolicy_SavesTheSlotAssignment()
    {
        _settings = new Settings { SlotPolicy = SlotPolicy.Remember };
        Plug(Pad1);

        Engine.Tick();

        _saved.Should().NotBeEmpty();
        _saved[^1].RememberedSlots.Should().Contain("pad1", 0);
    }

    [Fact]
    public void ManualPolicy_AssignsOnlyOnRequest_AndSlotTwoAloneGetsXInputIndexZero()
    {
        _settings = new Settings { SlotPolicy = SlotPolicy.Manual };
        Plug(Pad1);
        Engine.Tick();
        Engine.Slots[0].State.Should().Be(SlotState.Empty);
        _sink.Connected.Should().BeEmpty();

        Engine.Assign(Pad1.Id, 1);
        Engine.Tick();

        Engine.Slots[1].State.Should().Be(SlotState.Active);
        _sink.Connected.Should().Equal(1);
        Engine.Slots[1].XInputIndex.Should().Be(0, "Windows numbers XInput users in connect order, not slot order");
    }

    [Fact]
    public void Unassign_DisconnectsThePad()
    {
        Plug(Pad1);
        Engine.Tick();

        Engine.Unassign(0);
        Engine.Tick();

        Engine.Slots[0].State.Should().Be(SlotState.Empty);
        _sink.Disconnected.Should().Equal(0);
    }

    [Fact]
    public void Tick_SubmitsTheMappedState()
    {
        Plug(Pad1);
        Engine.Tick();

        _source.SetState(Pad1.Id, Pressed(ButtonId.South, leftX: 1f));
        Engine.Tick();

        var sent = _sink.LastSubmitted(0);
        sent.IsPressed(XboxButton.A).Should().BeTrue();
        sent.LeftX.Should().BeGreaterThan(30000);
    }

    [Fact]
    public void Tick_DoesNotResubmitAnUnchangedState()
    {
        Plug(Pad1);
        Engine.Tick();
        _source.SetState(Pad1.Id, Pressed(ButtonId.South));

        Engine.Tick();
        Engine.Tick();
        Engine.Tick();

        _sink.SubmitCount(0).Should().Be(2, "one neutral report on connect and one for the button");

        _source.SetState(Pad1.Id, Pressed(ButtonId.East));
        Engine.Tick();

        _sink.SubmitCount(0).Should().Be(3);
    }

    [Fact]
    public void Pause_SubmitsNeutral_AndResumeSubmitsTheLiveStateAgain()
    {
        Plug(Pad1);
        _source.SetState(Pad1.Id, Pressed(ButtonId.South));
        Engine.Tick();
        _sink.LastSubmitted(0).IsPressed(XboxButton.A).Should().BeTrue();

        Engine.Pause(true);
        Engine.Tick();

        Engine.IsPaused.Should().BeTrue();
        Engine.Slots[0].State.Should().Be(SlotState.Paused);
        _sink.LastSubmitted(0).Should().Be(XboxState.Neutral);

        Engine.Pause(false);
        Engine.Tick();

        Engine.Slots[0].State.Should().Be(SlotState.Active);
        _sink.LastSubmitted(0).IsPressed(XboxButton.A).Should().BeTrue();
    }

    [Fact]
    public void Rumble_IsRoutedToTheDeviceInThatSlot()
    {
        Plug(Pad1);
        Plug(Pad2);
        Engine.Tick();

        _sink.RaiseRumble(1, 200, 100);

        _source.Rumbles.Should().ContainSingle(r => r.Id == Pad2.Id && r.Low == 200 && r.High == 100 && r.DurationMs > 0);
        _source.Rumbles.Should().NotContain(r => r.Id == Pad1.Id);
    }

    [Fact]
    public void Rumble_OffIsSentWithZeroDuration_AndEmptySlotsAreIgnored()
    {
        Plug(Pad1);
        Engine.Tick();

        _sink.RaiseRumble(0, 0, 0);
        _sink.RaiseRumble(3, 255, 255);

        _source.Rumbles.Should().Equal((Pad1.Id, (byte)0, (byte)0, 0));
    }

    [Fact]
    public void Led_UsesTheSlotColor()
    {
        Plug(Pad1);
        Plug(Pad2);
        Plug(Pad3);

        Engine.Tick();

        _source.Leds.Should().Equal(
            (Pad1.Id, (byte)0, (byte)0, (byte)255),
            (Pad2.Id, (byte)255, (byte)0, (byte)0),
            (Pad3.Id, (byte)0, (byte)255, (byte)0));
    }

    [Fact]
    public void Led_IsSkippedForDevicesWithoutALight()
    {
        Plug(FakeInputSource.MakeDevice("plain", DeviceCapabilities.None));

        Engine.Tick();

        _source.Leds.Should().BeEmpty();
    }

    [Fact]
    public void Disconnect_KeepsThePadForTheGracePeriod_AndHoldsNeutral()
    {
        Plug(Pad1);
        _source.SetState(Pad1.Id, Pressed(ButtonId.South));
        Engine.Tick();

        _source.Unplug(Pad1.Id);
        Engine.Tick();
        _clock.Advance(TimeSpan.FromSeconds(5));
        Engine.Tick();

        Engine.Slots[0].State.Should().Be(SlotState.Waiting);
        _sink.Disconnected.Should().BeEmpty();
        _sink.LastSubmitted(0).Should().Be(XboxState.Neutral);
    }

    [Fact]
    public void Reconnect_WithinGracePeriod_ReusesTheSlotAndPad()
    {
        Plug(Pad1);
        Plug(Pad2);
        Engine.Tick();
        _source.Unplug(Pad1.Id);
        Engine.Tick();
        _clock.Advance(TimeSpan.FromSeconds(5));

        Plug(Pad1);
        _source.SetState(Pad1.Id, Pressed(ButtonId.North));
        Engine.Tick();

        Engine.Slots[0].Device.Should().Be(Pad1.Id);
        Engine.Slots[0].State.Should().Be(SlotState.Active);
        _sink.Connected.Should().Equal(0, 1);
        _sink.Disconnected.Should().BeEmpty();
        _sink.LastSubmitted(0).IsPressed(XboxButton.Y).Should().BeTrue();
    }

    [Fact]
    public void GracePeriodTimeout_DisconnectsThePadAndEmptiesTheSlot()
    {
        Plug(Pad1);
        Engine.Tick();
        _source.Unplug(Pad1.Id);
        Engine.Tick();

        _clock.Advance(TimeSpan.FromSeconds(10));
        Engine.Tick();

        Engine.Slots[0].State.Should().Be(SlotState.Empty);
        _sink.Disconnected.Should().Equal(0);
        Engine.Devices.Should().BeEmpty();
    }

    [Fact]
    public void GracePeriod_FollowsTheSetting()
    {
        _settings = new Settings { GracePeriodSeconds = 2 };
        Plug(Pad1);
        Engine.Tick();
        _source.Unplug(Pad1.Id);
        Engine.Tick();

        _clock.Advance(TimeSpan.FromSeconds(3));
        Engine.Tick();

        _sink.Disconnected.Should().Equal(0);
    }

    [Fact]
    public void KeyboardMode_ReservesSlotOneForTheKeyboard()
    {
        _settings = new Settings { Mode = KeyboardMode.KeyboardAsController };
        Plug(Pad1);
        Plug(Pad2);

        Engine.Tick();

        Engine.Slots[0].State.Should().Be(SlotState.Empty);
        Engine.Slots[1].Device.Should().Be(Pad1.Id);
        Engine.Slots[2].Device.Should().Be(Pad2.Id);
        _sink.Connected.Should().Equal(1, 2);
    }

    [Fact]
    public void LiveState_ReturnsRawAndMappedForAnyKnownDevice()
    {
        Plug(Pad1);
        _source.SetState(Pad1.Id, Pressed(ButtonId.South, leftX: 0.5f));
        Engine.Tick();

        var ok = Engine.TryGetLiveState(Pad1.Id, out var raw, out var mapped);

        ok.Should().BeTrue();
        raw.LeftX.Should().Be(0.5f);
        raw.Buttons.Should().Be(ButtonId.South);
        mapped.IsPressed(XboxButton.A).Should().BeTrue();
        Engine.TryGetLiveState(new DeviceId("nobody"), out _, out _).Should().BeFalse();
    }

    [Fact]
    public void LiveState_IsAvailableForADeviceThatHasNoSlot()
    {
        _settings = new Settings { SlotPolicy = SlotPolicy.Manual };
        Plug(Pad1);
        _source.SetState(Pad1.Id, Pressed(ButtonId.East));
        Engine.Tick();

        Engine.TryGetLiveState(Pad1.Id, out _, out var mapped).Should().BeTrue();

        mapped.IsPressed(XboxButton.B).Should().BeTrue();
        _sink.Submitted.Should().BeEmpty();
    }

    [Fact]
    public void SetProfile_ChangesTheMapping()
    {
        File.WriteAllText(Path.Combine(_profilesFolder, "swap.json"), """{ "schemaVersion": 1, "id": "swap", "name": "Swap", "appliesTo": { "processNames": ["game.exe"] }, "buttons": { "South": "B" } }""");
        Plug(Pad1);
        _source.SetState(Pad1.Id, Pressed(ButtonId.South));
        Engine.Tick();
        _sink.LastSubmitted(0).IsPressed(XboxButton.A).Should().BeTrue();

        Engine.SetProfile(0, "swap");
        Engine.Tick();

        Engine.Slots[0].ProfileId.Should().Be("swap");
        _sink.LastSubmitted(0).IsPressed(XboxButton.B).Should().BeTrue();
    }

    [Fact]
    public void SetProfile_WithAnUnknownId_KeepsTheCurrentProfile()
    {
        Plug(Pad1);
        Engine.Tick();
        var before = Engine.Slots[0].ProfileId;

        Engine.SetProfile(0, "does-not-exist");
        Engine.Tick();

        Engine.Slots[0].ProfileId.Should().Be(before);
        _log.Should().Contain(m => m.Contains("does-not-exist"));
    }

    [Fact]
    public void Changed_IsRaisedOnlyWhenSomethingChanged()
    {
        var raised = 0;
        Engine.Changed += () => raised++;

        Engine.Tick();
        raised.Should().Be(0);

        Plug(Pad1);
        Engine.Tick();
        raised.Should().BeGreaterThan(0);

        var after = raised;
        Engine.Tick();
        Engine.Tick();
        raised.Should().Be(after);
    }

    [Fact]
    public void ATickThatThrows_IsCountedAndLoggedAndTheNextTickRuns()
    {
        Plug(Pad1);
        var fail = true;
        _source.OnPoll = () =>
        {
            if (fail)
            {
                fail = false;
                throw new InvalidOperationException("boom");
            }
        };

        Engine.Tick();
        Engine.Tick();

        Engine.TickErrorCount.Should().Be(1);
        _log.Should().ContainSingle(m => m.Contains("boom"));
        Engine.Slots[0].Device.Should().Be(Pad1.Id);
    }

    [Fact]
    public void DriverStatus_ReportsTheSinkAvailability()
    {
        _sink.IsAvailable = false;

        var status = Engine.DriverStatus;

        status.ViGEmAvailable.Should().BeFalse();
        status.HidHideAvailable.Should().BeFalse();
    }

    [Fact]
    public void RealThread_RunsTheLoop_AndStopRemovesThePads()
    {
        _settings = new Settings { PollingRateHz = 500 };
        Plug(Pad1);

        Engine.Start();
        WaitFor(() => _sink.SubmitCount(0) >= 1).Should().BeTrue();
        Engine.IsRunning.Should().BeTrue();
        _source.SetState(Pad1.Id, Pressed(ButtonId.South));
        WaitFor(() => _sink.Submitted.Count > 0 && _sink.LastSubmitted(0).IsPressed(XboxButton.A)).Should().BeTrue();

        Engine.Stop();

        Engine.IsRunning.Should().BeFalse();
        _sink.Disconnected.Should().Equal(0);
        Engine.Slots[0].State.Should().Be(SlotState.Empty);
    }

    [Fact]
    public void Restart_AssignsKnownDevicesAgain()
    {
        Plug(Pad1);
        Engine.Start();
        WaitFor(() => _sink.Connected.Count == 1).Should().BeTrue();
        Engine.Stop();

        Engine.Start();

        WaitFor(() => _sink.Connected.Count == 2).Should().BeTrue();
        Engine.Slots[0].Device.Should().Be(Pad1.Id);
    }

    [Fact]
    public void Dispose_DisposesTheSources()
    {
        Engine.Dispose();

        _source.Disposed.Should().BeTrue();
    }

    private static bool WaitFor(Func<bool> condition)
    {
        var end = Environment.TickCount64 + 5000;
        while (Environment.TickCount64 < end)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(5);
        }

        return condition();
    }
}
