using System.Numerics;
using Controlability.Core.Abstractions;
using Controlability.Core.Extras;
using Controlability.Core.Keyboard;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;
using Controlability.Platform;

namespace Controlability.Engine.Tests.Runtime;

/// <summary>An input source whose gyro can be switched, like <c>SdlControllerSource</c>.</summary>
public sealed class FakeSensorSource : IInputSource, ISensorControl
{
    private readonly FakeInputSource _inner = new();

    public event Action<DeviceInfo>? DeviceAdded
    {
        add => _inner.DeviceAdded += value;
        remove => _inner.DeviceAdded -= value;
    }

    public event Action<DeviceId>? DeviceRemoved
    {
        add => _inner.DeviceRemoved += value;
        remove => _inner.DeviceRemoved -= value;
    }

    public List<(DeviceId Id, bool Enabled)> GyroRequests { get; } = [];

    public void Plug(DeviceInfo device) => _inner.Plug(device);

    public void SetState(DeviceId id, InputState state) => _inner.SetState(id, state);

    public void Poll() => _inner.Poll();

    public bool TryGetState(DeviceId id, out InputState state) => _inner.TryGetState(id, out state);

    public void SetRumble(DeviceId id, byte low, byte high, int durationMs) => _inner.SetRumble(id, low, high, durationMs);

    public void SetLed(DeviceId id, byte r, byte g, byte b) => _inner.SetLed(id, r, g, b);

    public void SetGyroEnabled(DeviceId id, bool enabled) => GyroRequests.Add((id, enabled));

    public void Dispose() => _inner.Dispose();
}

public sealed class FakeMouse : IMouseInjector
{
    public int TotalDx { get; private set; }

    public int TotalDy { get; private set; }

    public List<(MouseButtons Button, bool Down)> Clicks { get; } = [];

    public void Move(int dx, int dy)
    {
        TotalDx += dx;
        TotalDy += dy;
    }

    public void SetButton(MouseButtons button, bool down) => Clicks.Add((button, down));
}

public sealed class FakeGameWatcher : IGameProcessWatcher
{
    public event Action<string?>? ForegroundProcessChanged;

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public void Start() => StartCount++;

    public void Stop() => StopCount++;

    public void Raise(string? process) => ForegroundProcessChanged?.Invoke(process);

    public void Dispose()
    {
    }
}

public sealed class ExtrasEngineTests : IDisposable
{
    private static readonly DeviceInfo Pad = FakeInputSource.MakeDevice("pad", DeviceCapabilities.Rumble | DeviceCapabilities.Led | DeviceCapabilities.Gyro | DeviceCapabilities.Touchpad);

    private readonly string _folder = Directory.CreateTempSubdirectory("controlability-extras-tests").FullName;
    private readonly FakeClock _clock = new();
    private readonly FakeSensorSource _source = new();
    private readonly FakeOutputSink _sink = new();
    private readonly FakeMouse _mouse = new();
    private readonly FakeGameWatcher _watcher = new();
    private readonly ProfileStore _store;
    private readonly Controlability.Engine.Engine _engine;

    public ExtrasEngineTests()
    {
        _store = new ProfileStore(_folder);
        _engine = new Controlability.Engine.Engine(
            [_source],
            _sink,
            _store,
            _clock,
            new Settings(),
            mouse: _mouse,
            gameWatcher: _watcher);
    }

    public void Dispose()
    {
        _engine.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private static Profile MakeProfile(string id, Action<Profile>? tweak = null, string[]? processes = null)
    {
        var profile = new Profile { Id = id, Name = id };
        profile.AppliesTo.ProcessNames.AddRange(processes ?? []);
        tweak?.Invoke(profile);
        return profile;
    }

    private void Connect()
    {
        _source.Plug(Pad);
        _engine.Tick();
    }

    private void Tick(InputState state, double advanceMs = 0)
    {
        _clock.Advance(TimeSpan.FromMilliseconds(advanceMs));
        _source.SetState(Pad.Id, state);
        _engine.Tick();
    }

    // ---- Gyro ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Gyro_IsSwitchedOnOnlyWhileTheProfileNeedsIt()
    {
        _store.Save(MakeProfile("default-generic", p => p.Extras.GyroToRightStick = true));
        Connect();
        _source.GyroRequests.Should().Equal([(Pad.Id, true)]);

        _store.Save(MakeProfile("default-generic"));
        _engine.ReloadProfile("default-generic");
        _engine.Tick();

        _source.GyroRequests.Should().Equal([(Pad.Id, true), (Pad.Id, false)]);
    }

    [Fact]
    public void Gyro_StaysOffForAProfileWithoutIt()
    {
        _store.Save(MakeProfile("default-generic"));

        Connect();

        _source.GyroRequests.Should().BeEmpty("the sensor is never asked for, so it costs no battery");
    }

    [Fact]
    public void Gyro_SteersTheRightStickInTheSubmittedReport()
    {
        _store.Save(MakeProfile("default-generic", p =>
        {
            p.Extras.GyroToRightStick = true;
            p.Extras.GyroSmoothing = 0;
        }));
        Connect();

        Tick(new InputState { HasGyro = true, Gyro = new Vector3(0, -MathF.PI / 2, 0) }, 1);

        _sink.LastSubmitted(0).RightX.Should().BeInRange((short)16000, (short)16500);
    }

    [Fact]
    public void Unassigning_SwitchesTheGyroOffAgain()
    {
        _store.Save(MakeProfile("default-generic", p => p.Extras.GyroToRightStick = true));
        Connect();

        _engine.Unassign(0);
        _engine.Tick();

        _source.GyroRequests.Last().Should().Be((Pad.Id, false));
    }

    // ---- Turbo and toggle ---------------------------------------------------------------------------------------

    [Fact]
    public void Turbo_PulsesTheButtonInTheReports_OnTheClock()
    {
        _store.Save(MakeProfile("default-generic", p => p.Extras.Turbo["A"] = 10));
        Connect();
        var held = new InputState { Buttons = ButtonId.South };
        var seen = new List<bool>();

        for (var i = 0; i < 8; i++)
        {
            Tick(held, i == 0 ? 0 : 25);
            seen.Add(_sink.LastSubmitted(0).IsPressed(XboxButton.A));
        }

        seen.Should().Equal(true, true, false, false, true, true, false, false);
    }

    [Fact]
    public void Toggle_LatchesTheButtonAcrossReleases()
    {
        _store.Save(MakeProfile("default-generic", p => p.Extras.Toggle.Add("A")));
        Connect();

        Tick(new InputState { Buttons = ButtonId.South });
        Tick(new InputState());
        _sink.LastSubmitted(0).IsPressed(XboxButton.A).Should().BeTrue();

        Tick(new InputState { Buttons = ButtonId.South });
        Tick(new InputState());
        _sink.LastSubmitted(0).IsPressed(XboxButton.A).Should().BeFalse();
    }

    [Fact]
    public void Pausing_DropsALatchedToggle()
    {
        _store.Save(MakeProfile("default-generic", p => p.Extras.Toggle.Add("A")));
        Connect();
        Tick(new InputState { Buttons = ButtonId.South });
        Tick(new InputState());

        _engine.Pause(true);
        _engine.Tick();
        _engine.Pause(false);
        Tick(new InputState());

        _sink.LastSubmitted(0).IsPressed(XboxButton.A).Should().BeFalse();
    }

    // ---- Touchpad mouse -----------------------------------------------------------------------------------------

    [Fact]
    public void TouchpadMouse_IsOffByDefault()
    {
        _store.Save(MakeProfile("default-generic", p => p.Buttons["Touchpad"] = "Back"));
        Connect();

        Tick(new InputState { Touch0 = new TouchPoint(0.2f, 0.2f, true) });
        Tick(new InputState { Touch0 = new TouchPoint(0.6f, 0.2f, true), Buttons = ButtonId.Touchpad }, 10);

        _mouse.TotalDx.Should().Be(0);
        _mouse.Clicks.Should().BeEmpty();
        _sink.LastSubmitted(0).IsPressed(XboxButton.Back).Should().BeTrue("without the extra the pad click is still Back");
    }

    [Fact]
    public void TouchpadMouse_MovesAndClicks_AndTheClickNoLongerReachesTheGame()
    {
        _store.Save(MakeProfile("default-generic", p =>
        {
            p.Buttons["Touchpad"] = "Back";
            p.Extras.TouchpadMouse = true;
        }));
        Connect();

        Tick(new InputState { Touch0 = new TouchPoint(0.2f, 0.2f, true) });
        Tick(new InputState { Touch0 = new TouchPoint(0.3f, 0.2f, true), Buttons = ButtonId.Touchpad }, 10);

        _mouse.TotalDx.Should().BeGreaterThan(100);
        _mouse.Clicks.Should().Equal([(MouseButtons.Left, true)]);
        _sink.LastSubmitted(0).IsPressed(XboxButton.Back).Should().BeFalse();

        Tick(new InputState(), 10);
        _mouse.Clicks.Should().Equal([(MouseButtons.Left, true), (MouseButtons.Left, false)]);
    }

    [Fact]
    public void TouchpadMouse_ReleasesAHeldButtonWhenTheSlotIsEmptied()
    {
        _store.Save(MakeProfile("default-generic", p => p.Extras.TouchpadMouse = true));
        Connect();
        Tick(new InputState { Touch0 = new TouchPoint(0.2f, 0.2f, true), Buttons = ButtonId.Touchpad });

        _engine.Unassign(0);
        _engine.Tick();

        _mouse.Clicks.Last().Should().Be((MouseButtons.Left, false));
    }

    [Fact]
    public void TouchpadMouse_StaysQuietWhilePaused()
    {
        _store.Save(MakeProfile("default-generic", p => p.Extras.TouchpadMouse = true));
        Connect();
        _engine.Pause(true);
        _engine.Tick();

        Tick(new InputState { Touch0 = new TouchPoint(0.2f, 0.2f, true) });
        Tick(new InputState { Touch0 = new TouchPoint(0.8f, 0.2f, true), Buttons = ButtonId.Touchpad }, 10);

        _mouse.TotalDx.Should().Be(0);
        _mouse.Clicks.Should().BeEmpty();
    }

    [Fact]
    public void ABrokenExtra_IsLoggedAndTurnedOff_WithoutBreakingTheMapping()
    {
        var log = new List<string>();
        using var engine = new Controlability.Engine.Engine([_source], _sink, _store, _clock, new Settings(), log: log.Add, mouse: _mouse);
        _store.Save(MakeProfile("default-generic", p => p.Extras.Turbo["Banana"] = 10));
        _source.Plug(Pad);
        engine.Tick();

        _source.SetState(Pad.Id, new InputState { Buttons = ButtonId.South });
        engine.Tick();

        log.Should().Contain(line => line.Contains("Banana"));
        _sink.LastSubmitted(0).IsPressed(XboxButton.A).Should().BeTrue();
    }

    // ---- Game auto-profile --------------------------------------------------------------------------------------

    [Fact]
    public void AGameComingToTheFront_SwitchesTheSlotsProfile_AndLeavingRestoresIt()
    {
        _store.Save(MakeProfile("default-generic"));
        _store.Save(MakeProfile("mine"));
        _store.Save(MakeProfile("shooter", processes: ["shooter.exe"]));
        Connect();
        _engine.SetProfile(0, "mine");
        _engine.Tick();

        _watcher.Raise("Shooter");
        _engine.Tick();

        _engine.Slots[0].ProfileId.Should().Be("shooter");
        _engine.ActiveGame.Should().Be("Shooter");

        _watcher.Raise("notepad");
        _engine.Tick();

        _engine.Slots[0].ProfileId.Should().Be("mine", "the profile from before the game comes back");
        _engine.ActiveGame.Should().BeNull();
    }

    [Fact]
    public void SwitchingFromOneGameToAnother_StillRestoresTheOriginalProfile()
    {
        _store.Save(MakeProfile("default-generic"));
        _store.Save(MakeProfile("game-a", processes: ["a"]));
        _store.Save(MakeProfile("game-b", processes: ["b"]));
        Connect();

        _watcher.Raise("a");
        _engine.Tick();
        _watcher.Raise("b");
        _engine.Tick();
        _engine.Slots[0].ProfileId.Should().Be("game-b");

        _watcher.Raise(null);
        _engine.Tick();

        _engine.Slots[0].ProfileId.Should().Be("default-generic");
    }

    [Fact]
    public void AProfileChosenByHandWhileInAGame_IsNotTakenAway()
    {
        _store.Save(MakeProfile("default-generic"));
        _store.Save(MakeProfile("mine"));
        _store.Save(MakeProfile("shooter", processes: ["shooter"]));
        Connect();
        _watcher.Raise("shooter");
        _engine.Tick();

        _engine.SetProfile(0, "mine");
        _engine.Tick();
        _watcher.Raise("notepad");
        _engine.Tick();

        _engine.Slots[0].ProfileId.Should().Be("mine");
    }

    [Fact]
    public void ADeviceAssignedWhileAGameIsInFront_StartsOnTheGameProfile()
    {
        _store.Save(MakeProfile("default-generic"));
        _store.Save(MakeProfile("shooter", processes: ["shooter"]));
        _watcher.Raise("shooter");
        _engine.Tick();

        Connect();

        _engine.Slots[0].ProfileId.Should().Be("shooter");
        _engine.ActiveGame.Should().Be("shooter");
    }

    [Fact]
    public void OnlyTheControllerTypesOfAGameProfileSwitch()
    {
        var xbox = FakeInputSource.MakeDevice("xbox");
        _store.Save(MakeProfile("default-generic"));
        _store.Save(MakeProfile("ps4-shooter", p => p.AppliesTo.ControllerTypes.Add(ControllerType.PS4), ["shooter"]));
        _source.Plug(Pad);
        _source.Plug(xbox with { Type = ControllerType.Xbox });
        _engine.Tick();

        _watcher.Raise("shooter");
        _engine.Tick();

        _engine.Slots[0].ProfileId.Should().Be("ps4-shooter");
        _engine.Slots[1].ProfileId.Should().Be("default-generic");
    }

    [Fact]
    public void TheWatcherStartsAndStopsWithTheEngine()
    {
        _engine.Start();
        _engine.Stop();

        _watcher.StartCount.Should().Be(1);
        _watcher.StopCount.Should().BeGreaterThanOrEqualTo(1);
    }
}

public sealed class GameProcessWatcherTests
{
    [Fact]
    public void RaisesOnlyWhenTheForegroundProcessChanges()
    {
        var current = "game.exe";
        var watcher = new GameProcessWatcher(() => current);
        var seen = new List<string?>();
        watcher.ForegroundProcessChanged += seen.Add;

        watcher.Poll();
        watcher.Poll();
        current = "GAME";
        watcher.Poll();
        current = "browser";
        watcher.Poll();

        seen.Should().Equal("game", "browser");
        watcher.Current.Should().Be("browser");
    }

    [Fact]
    public void ReportsNullWhenNothingCanBeRead()
    {
        string? current = "game";
        var watcher = new GameProcessWatcher(() => current);
        var seen = new List<string?>();
        watcher.ForegroundProcessChanged += seen.Add;
        watcher.Poll();

        current = null;
        watcher.Poll();

        seen.Should().Equal("game", null);
    }

    [Fact]
    public void ALookupThatThrows_CountsAsUnknown()
    {
        var watcher = new GameProcessWatcher(() => throw new InvalidOperationException("denied"));
        var seen = new List<string?>();
        watcher.ForegroundProcessChanged += seen.Add;

        watcher.Poll();

        seen.Should().BeEmpty("null to null is no change");
    }

    [Fact]
    public async Task TheTimerPollsOnItsOwn_AndStopsAfterStop()
    {
        var calls = 0;
        var watcher = new GameProcessWatcher(() => $"p{Interlocked.Increment(ref calls)}", TimeSpan.FromMilliseconds(20));
        var seen = new List<string?>();
        watcher.ForegroundProcessChanged += name => { lock (seen) { seen.Add(name); } };

        watcher.Start();
        await Task.Delay(300);
        watcher.Stop();
        var afterStop = Volatile.Read(ref calls);
        await Task.Delay(100);

        afterStop.Should().BeGreaterThan(2);
        Volatile.Read(ref calls).Should().Be(afterStop);
        watcher.Dispose();
    }
}
