using Controlability.App.ViewModels;
using Controlability.Core.Models;
using Controlability.Core.Profiles;

namespace Controlability.App.Tests;

public sealed class ProfilesViewModelTests
{
    private readonly FakeEngine _engine = new();
    private readonly FakeProfileEditor _store = new();
    private readonly FakeUiThread _ui = new();
    private readonly FakeClock _clock = new();
    private readonly FakeDialogs _dialogs = new();

    public ProfilesViewModelTests()
    {
        _store.Profiles["default-generic"] = new Profile { Id = "default-generic", Name = "Generic default" };
        _store.Profiles["racing"] = new Profile { Id = "racing", Name = "Racing" };
    }

    private ProfilesViewModel Create() => new(_engine, _store, new FakeRunningApps(), _dialogs, _ui, _clock);

    [Fact]
    public void ListsTheProfilesByName_AndEditsTheFirst()
    {
        using var vm = Create();

        vm.Profiles.Select(p => p.Name).Should().Equal("Generic default", "Racing");
        vm.SelectedProfile!.Id.Should().Be("default-generic");
        vm.Editor!.Id.Should().Be("default-generic");
        vm.Editor.IsShipped.Should().BeTrue();
    }

    [Fact]
    public void Duplicate_SavesACopyWithAFreshNameAndId_AndSelectsIt()
    {
        using var vm = Create();
        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == "racing");

        vm.DuplicateCommand.Execute(null);
        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == "racing");
        vm.DuplicateCommand.Execute(null);

        _store.Profiles.Keys.Should().Contain(["racing-copy", "racing-copy-2"]);
        _store.Profiles["racing-copy"].Name.Should().Be("Racing (copy)");
        _store.Profiles["racing-copy-2"].Name.Should().Be("Racing (copy) (2)");
        vm.Editor!.Id.Should().Be("racing-copy-2");
    }

    [Fact]
    public void Delete_AsksFirst_AndShippedProfilesCannotBeDeleted()
    {
        using var vm = Create();
        vm.DeleteCommand.CanExecute(null).Should().BeFalse("default-generic ships with the app");

        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == "racing");
        vm.DeleteCommand.CanExecute(null).Should().BeTrue();

        _dialogs.ConfirmAnswer = false;
        vm.DeleteCommand.Execute(null);
        _store.Profiles.Should().ContainKey("racing");

        _dialogs.ConfirmAnswer = true;
        vm.DeleteCommand.Execute(null);
        _store.Profiles.Should().NotContainKey("racing");
        vm.Profiles.Select(p => p.Id).Should().Equal("default-generic");
        vm.Editor!.Id.Should().Be("default-generic");
    }

    [Fact]
    public void Import_GivesTheProfileAUniqueNameAndId()
    {
        _store.Files["x.json"] = new Profile { Id = "racing", Name = "Racing" };
        _dialogs.OpenPath = "x.json";
        using var vm = Create();

        vm.ImportCommand.Execute(null);

        _store.Profiles["racing"].Name.Should().Be("Racing", "the existing profile is untouched");
        _store.Profiles["racing-2"].Name.Should().Be("Racing (2)");
        vm.Editor!.Id.Should().Be("racing-2");
    }

    [Fact]
    public void Import_OfABadFile_ShowsTheReason()
    {
        _dialogs.OpenPath = "bad.json";
        using var vm = Create();

        vm.ImportCommand.Execute(null);

        vm.StatusText.Should().Contain("Could not import");
        _store.Profiles.Should().HaveCount(2);
    }

    [Fact]
    public void Import_CancelledDialog_DoesNothing()
    {
        _dialogs.OpenPath = null;
        using var vm = Create();

        vm.ImportCommand.Execute(null);

        _store.Profiles.Should().HaveCount(2);
    }

    [Fact]
    public void Export_WritesTheSavedProfile()
    {
        _dialogs.SavePath = "out.json";
        using var vm = Create();
        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == "racing");
        vm.Editor!.Name = "Unsaved edit";

        vm.ExportCommand.Execute(null);

        _store.Exported["out.json"].Name.Should().Be("Racing");
    }

    [Fact]
    public void SwitchingAwayFromUnsavedChanges_AsksAndCanStay()
    {
        using var vm = Create();
        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == "racing");
        vm.Editor!.Name = "Unsaved edit";
        var editor = vm.Editor;

        _dialogs.ConfirmAnswer = false;
        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == "default-generic");
        _ui.RunAll();

        _dialogs.Confirmations.Should().ContainSingle();
        vm.Editor.Should().BeSameAs(editor);
        vm.SelectedProfile!.Id.Should().Be("racing");

        _dialogs.ConfirmAnswer = true;
        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == "default-generic");
        vm.Editor!.Id.Should().Be("default-generic");
    }

    [Fact]
    public void SavingRenamesTheEntryInTheList_AndKeepsTheSelection()
    {
        using var vm = Create();
        vm.SelectedProfile = vm.Profiles.Single(p => p.Id == "racing");

        vm.Editor!.Name = "Zebra";
        vm.Editor.Save();

        vm.Profiles.Select(p => p.Name).Should().Equal("Generic default", "Zebra");
        vm.SelectedProfile!.Id.Should().Be("racing");
        vm.Editor.Id.Should().Be("racing");
    }
}

public sealed class DevicesViewModelTests : IDisposable
{
    private static readonly DeviceInfo Pad = FakeEngine.MakeDevice("pad");
    private static readonly DeviceInfo Other = FakeEngine.MakeDevice("other", ControllerType.SwitchPro, ConnectionType.Bluetooth);

    private readonly FakeEngine _engine = new();
    private readonly FakeProfileEditor _store = new();
    private readonly FakeUiThread _ui = new();
    private readonly FakeClock _clock = new();
    private readonly DevicesViewModel _vm;

    public DevicesViewModelTests()
    {
        _engine.Devices = [Pad, Other];
        _engine.Slots = [FakeEngine.Active(0, Pad, "tuned"), .. _engine.Slots.Skip(1)];
        var tuned = new Profile { Id = "tuned", Name = "Tuned" };
        tuned.Sticks.Left.DeadZone = 0.2f;
        _store.Profiles["tuned"] = tuned;
        _vm = new DevicesViewModel(_engine, _store, _ui, _clock);
    }

    public void Dispose() => _vm.Dispose();

    [Fact]
    public void ListsEveryDeviceWithItsDetails()
    {
        _vm.Devices.Select(d => d.Name).Should().Equal("pad", "other");
        var pad = _vm.Selected!;
        pad.Name.Should().Be("pad");
        pad.VidPidText.Should().Be("054C:09CC");
        pad.CapabilitiesText.Should().Be("Rumble, Light bar");
        pad.AssignmentText.Should().Be("On Player 1");
        _vm.Devices[1].AssignmentText.Should().Be("Not assigned");
        _vm.HasDevices.Should().BeTrue();
    }

    [Fact]
    public void TheSelectedDevicesProfileSetsTheDeadZoneRings()
    {
        _vm.LeftInner.Should().BeApproximately(0.2, 1e-6);

        _vm.Selected = _vm.Devices[1];

        _vm.LeftInner.Should().BeApproximately(0.08, 1e-6);
        _vm.Live.Device.Should().Be(Other.Id);
    }

    [Fact]
    public void TheHideToggle_CallsTheEngine_AndFollowsItsState()
    {
        var pad = _vm.Selected!;
        pad.CanHide.Should().BeTrue();

        pad.IsHidden = true;
        _engine.Calls.Should().Contain("SetDeviceHidden:pad:True");

        _engine.Calls.Clear();
        _engine.Hidden.Clear();
        _engine.RaiseChanged();
        _ui.RunAll();

        pad.IsHidden.Should().BeFalse("the engine says it is not hidden");
        _engine.Calls.Should().BeEmpty("a refresh must not send the toggle back");
    }

    [Fact]
    public void WithoutHidHide_TheToggleIsDisabled()
    {
        _engine.DriverStatus = new Controlability.Engine.DriverStatus(true, false);

        _vm.Refresh();

        _vm.Selected!.CanHide.Should().BeFalse();
        _vm.HidHideMissing.Should().BeTrue();
    }

    [Fact]
    public void RumbleTestAndLed_GoThroughTheEngine()
    {
        var pad = _vm.Selected!;

        pad.RumbleTestCommand.Execute(null);
        pad.SetLedCommand.Execute("#ff8800");

        _engine.Calls.Should().Contain("TestRumble:pad").And.Contain("SetLedColor:pad:255,136,0");
        pad.LedText.Should().Be("#FF8800");
        pad.LedError.Should().BeEmpty();
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GG0000")]
    public void ABadLedColorIsRejected(string text)
    {
        var pad = _vm.Selected!;

        pad.SetLedCommand.Execute(text);

        pad.LedError.Should().NotBeEmpty();
        _engine.Calls.Should().NotContain(c => c.StartsWith("SetLedColor"));
    }

    [Fact]
    public void RumbleAndLedAreOnlyOfferedWhereTheDeviceHasThem()
    {
        _engine.Devices = [new DeviceInfo(new DeviceId("plain"), "plain", 1, 2, null, null, ConnectionType.Usb, ControllerType.Generic, DeviceCapabilities.None)];
        _vm.Refresh();

        var plain = _vm.Selected!;
        plain.RumbleTestCommand.CanExecute(null).Should().BeFalse();
        plain.SetLedCommand.CanExecute(null).Should().BeFalse();
        plain.CapabilitiesText.Should().Be("None");
    }

    [Fact]
    public void UnplugReselectsTheFirstDevice()
    {
        _vm.Selected = _vm.Devices[1];

        _engine.Devices = [Pad];
        _vm.Refresh();

        _vm.Devices.Should().ContainSingle();
        _vm.Selected!.Id.Should().Be(Pad.Id);
    }
}

public sealed class PadPoseTests
{
    [Fact]
    public void RawButtonsAreShownByPosition()
    {
        var pose = PadPose.FromRaw(new InputState { Buttons = ButtonId.South | ButtonId.DpadLeft | ButtonId.Touchpad, LeftX = 2f });

        pose.Buttons.Should().Be(XboxButton.A | XboxButton.DpadLeft);
        pose.LeftX.Should().Be(1f, "out-of-range values are clamped for drawing");
    }

    [Fact]
    public void XboxStateIsScaledToUnits()
    {
        var pose = PadPose.FromXbox(new XboxState { LeftX = short.MaxValue, RightY = short.MinValue, LeftTrigger = 255, Buttons = (ushort)XboxButton.Y });

        pose.LeftX.Should().Be(1f);
        pose.RightY.Should().Be(-1f);
        pose.LeftTrigger.Should().Be(1f);
        pose.Buttons.Should().Be(XboxButton.Y);
    }

    [Fact]
    public void LivePad_ReportsNoStateForAnUnknownDevice_AndChangesOnlyWhenTheStateDid()
    {
        var engine = new FakeEngine();
        var live = new LivePadViewModel(engine) { Device = new DeviceId("pad") };
        live.Poll().Should().BeFalse();
        live.HasState.Should().BeFalse();

        engine.Live[new DeviceId("pad")] = new InputState { LeftTrigger = 0.5f };
        live.Poll().Should().BeTrue();
        live.HasState.Should().BeTrue();
        live.Raw.LeftTrigger.Should().Be(0.5f);
        live.Poll().Should().BeFalse("nothing changed");
    }
}
