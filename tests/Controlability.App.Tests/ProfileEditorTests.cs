using Controlability.App.Services;
using Controlability.App.ViewModels;
using Controlability.Core.Keyboard;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Engine;

namespace Controlability.App.Tests;

public sealed class FakeDialogs : IDialogs
{
    public string? OpenPath { get; set; }

    public string? SavePath { get; set; }

    public bool ConfirmAnswer { get; set; } = true;

    public List<string> Confirmations { get; } = [];

    public string? PickOpenFile(string title, string filter) => OpenPath;

    public string? PickSaveFile(string title, string filter, string fileName) => SavePath;

    public bool Confirm(string title, string message)
    {
        Confirmations.Add(title);
        return ConfirmAnswer;
    }
}

public sealed class ProfileEditorTests
{
    private static readonly DeviceInfo Pad = FakeEngine.MakeDevice("pad");

    private readonly FakeEngine _engine = new();
    private readonly FakeProfileEditor _store = new();
    private readonly FakeUiThread _ui = new();
    private readonly FakeClock _clock = new();
    private readonly FakeDialogs _dialogs = new();

    public ProfileEditorTests()
    {
        _engine.Devices = [Pad];
        _engine.Slots = [FakeEngine.Active(0, Pad, "mine"), .. _engine.Slots.Skip(1)];
        _store.Profiles["mine"] = new Profile { Id = "mine", Name = "Mine" };
    }

    private ProfileEditorViewModel Editor(Profile? profile = null)
        => new(profile ?? _store.Get("mine")!, _store, _engine, new FakeRunningApps("Game", "Other"), _dialogs, _ui, _clock);

    private static InputState Pressed(ButtonId buttons) => new() { Buttons = buttons };

    // ---- Binding flow -------------------------------------------------------------------------------------------

    [Fact]
    public void Binding_StoresThePressedButton_AndSwapsWithTheOneThatHadTheTarget()
    {
        var editor = Editor();
        _engine.Live[Pad.Id] = default;

        editor.BeginBind(KeyboardControl.A);
        editor.IsBinding.Should().BeTrue();
        _ui.Timers.Should().ContainSingle();

        _engine.Live[Pad.Id] = Pressed(ButtonId.West);
        _ui.Timers[0].Action();

        editor.IsBinding.Should().BeFalse();
        _ui.Timers.Should().BeEmpty();
        editor.IsDirty.Should().BeTrue();
        editor.SavedProfile.Buttons.Should().BeEmpty("nothing is stored before Save");

        editor.Save();
        var saved = _store.Profiles["mine"].Buttons;
        saved["West"].Should().Be("A");
        saved["South"].Should().Be("X", "South gave up A, so it takes what West sent before");
    }

    [Fact]
    public void Binding_IgnoresButtonsHeldWhenItStarted_UntilTheyAreReleased()
    {
        var editor = Editor();
        _engine.Live[Pad.Id] = Pressed(ButtonId.South);

        editor.BeginBind(KeyboardControl.B);
        _ui.Timers[0].Action();
        editor.IsBinding.Should().BeTrue("the held button does not count");

        _engine.Live[Pad.Id] = default;
        _ui.Timers[0].Action();
        _engine.Live[Pad.Id] = Pressed(ButtonId.South);
        _ui.Timers[0].Action();

        editor.IsBinding.Should().BeFalse();
        editor.ButtonRows.Single(r => r.Control == KeyboardControl.B).SourceText.Should().Contain("Cross");
    }

    [Fact]
    public void Binding_TimesOutAfterFiveSeconds()
    {
        var editor = Editor();
        _engine.Live[Pad.Id] = default;
        editor.BeginBind(KeyboardControl.A);

        _clock.Advance(TimeSpan.FromSeconds(4.9));
        _ui.Timers[0].Action();
        editor.IsBinding.Should().BeTrue();

        _clock.Advance(TimeSpan.FromSeconds(0.2));
        _ui.Timers[0].Action();

        editor.IsBinding.Should().BeFalse();
        editor.BindPrompt.Should().Contain("No button");
        editor.IsDirty.Should().BeFalse();
        _ui.Timers.Should().BeEmpty();
    }

    [Fact]
    public void Binding_CanBeCancelled_AndChangesNothing()
    {
        var editor = Editor();
        _engine.Live[Pad.Id] = default;
        editor.BeginBind(KeyboardControl.A);

        editor.CancelBind();

        editor.IsBinding.Should().BeFalse();
        editor.BindPrompt.Should().BeEmpty();
        editor.ButtonRows.Should().OnlyContain(r => !r.IsBinding);
        _ui.Timers.Should().BeEmpty();
        _engine.Live[Pad.Id] = Pressed(ButtonId.East);
        editor.PollBind();
        editor.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Binding_WithoutAController_ExplainsAndDoesNotWait()
    {
        _engine.Devices = [];
        _engine.Slots = [.. Enumerable.Range(0, 4).Select(i => new SlotStatus(i, null, null, null, SlotState.Empty, null))];
        var editor = Editor();

        editor.BeginBind(KeyboardControl.A);

        editor.IsBinding.Should().BeFalse();
        editor.BindPrompt.Should().Contain("Connect a controller");
        _ui.Timers.Should().BeEmpty();
    }

    [Fact]
    public void BindingBackToTheDefault_RemovesTheOverride()
    {
        var editor = Editor();
        _engine.Live[Pad.Id] = default;
        editor.Bind(ButtonId.South, XboxButton.B);
        editor.IsDirty.Should().BeTrue();

        editor.Bind(ButtonId.South, XboxButton.A);

        editor.IsDirty.Should().BeFalse("the draft is the same as the saved profile again");
    }

    [Fact]
    public void ClearBinding_DisablesTheButtonsThatSendTheTarget()
    {
        var editor = Editor();

        editor.ClearBinding(KeyboardControl.A);
        editor.Save();

        _store.Profiles["mine"].Buttons["South"].Should().Be("None");
        editor.ButtonRows.Single(r => r.Control == KeyboardControl.A).SourceText.Should().Be("not mapped");
    }

    // ---- Validation ---------------------------------------------------------------------------------------------

    [Fact]
    public void AnInnerDeadZoneAtOrAboveTheOuterOne_IsInvalid_AndCannotBeSaved()
    {
        var editor = Editor();

        editor.LeftOuter = 0.5;
        editor.LeftInner = 0.5;

        editor.IsValid.Should().BeFalse();
        editor.ValidationText.Should().Contain("Left stick");
        editor.CanSave.Should().BeFalse();
        editor.SaveCommand.CanExecute(null).Should().BeFalse();
        editor.Save();
        _store.Saved.Should().BeEmpty();

        editor.LeftInner = 0.2;
        editor.IsValid.Should().BeTrue();
        editor.ValidationText.Should().BeEmpty();
        editor.CanSave.Should().BeTrue();
    }

    [Fact]
    public void AnEmptyName_IsInvalid()
    {
        var editor = Editor();

        editor.Name = "   ";

        editor.IsValid.Should().BeFalse();
        editor.ValidationText.Should().Contain("name");
        editor.Save();
        _store.Saved.Should().BeEmpty();
    }

    [Fact]
    public void ADuplicateName_GetsANumberOnSave()
    {
        _store.Profiles["other"] = new Profile { Id = "other", Name = "Racing" };
        var editor = Editor();

        editor.Name = "racing";
        editor.Save();

        editor.Name.Should().Be("racing (2)");
        _store.Profiles["mine"].Name.Should().Be("racing (2)");
    }

    [Fact]
    public void KeepingYourOwnName_IsNotADuplicate()
    {
        var editor = Editor();
        editor.TriggerDeadZone = 0.1;

        editor.Save();

        _store.Profiles["mine"].Name.Should().Be("Mine");
    }

    // ---- Save, revert, reset ------------------------------------------------------------------------------------

    [Fact]
    public void Save_WritesTheProfile_AndHotReloadsItInTheEngine()
    {
        var editor = Editor();
        editor.RightOuter = 0.9;
        editor.RightCurve = Controlability.Core.Mapping.CurveType.Quadratic;
        var raised = 0;
        editor.Saved += _ => raised++;

        editor.Save();

        _store.Profiles["mine"].Sticks.Right.Outer.Should().Be(0.9f);
        _store.Profiles["mine"].Sticks.Right.Curve.Should().Be(Controlability.Core.Mapping.CurveType.Quadratic);
        _engine.Calls.Should().Contain("ReloadProfile:mine");
        editor.IsDirty.Should().BeFalse();
        raised.Should().Be(1);
    }

    [Fact]
    public void Revert_RestoresTheSavedProfile_IncludingButtonsAndKeys()
    {
        var editor = Editor();
        editor.Name = "Changed";
        editor.LeftInner = 0.3;
        editor.TriggerDigital = true;
        editor.Bind(ButtonId.South, XboxButton.B);
        editor.IsDirty.Should().BeTrue();

        editor.Revert();

        editor.Name.Should().Be("Mine");
        editor.LeftInner.Should().BeApproximately(0.08, 1e-6);
        editor.TriggerDigital.Should().BeFalse();
        editor.IsDirty.Should().BeFalse();
        editor.RevertCommand.CanExecute(null).Should().BeFalse();
        editor.ButtonRows.Single(r => r.Control == KeyboardControl.A).SourceText.Should().Contain("Cross");
        _store.Saved.Should().BeEmpty();
        _engine.Calls.Should().NotContain(c => c.StartsWith("ReloadProfile"));
    }

    [Fact]
    public void ResetToDefault_TakesTheShippedSettings_ButKeepsIdNameAndTargets()
    {
        _store.Shipped["default-generic"] = new Profile { Id = "default-generic", Name = "Generic default", Triggers = new ProfileTriggers { DeadZone = 0.05f } };
        var profile = new Profile { Id = "mine", Name = "Mine" };
        profile.AppliesTo.ProcessNames.Add("game.exe");
        profile.Triggers.Digital = true;
        _store.Profiles["mine"] = profile;
        var editor = Editor();

        editor.ResetToDefault();

        editor.Id.Should().Be("mine");
        editor.Name.Should().Be("Mine");
        editor.ProcessNames.Should().Equal("game.exe");
        editor.TriggerDigital.Should().BeFalse();
        editor.TriggerDeadZone.Should().BeApproximately(0.05, 1e-6);
        editor.IsDirty.Should().BeTrue("a reset is only kept after Save");
        _store.Saved.Should().BeEmpty();
    }

    [Fact]
    public void ResetToDefault_OnAShippedProfile_RestoresItsOwnCopy()
    {
        var shipped = new Profile { Id = "default-playstation", Name = "PlayStation default" };
        shipped.Buttons["South"] = "B";
        _store.Shipped["default-playstation"] = shipped;
        _store.Profiles["default-playstation"] = new Profile { Id = "default-playstation", Name = "Edited" };
        var editor = Editor(_store.Get("default-playstation"));

        editor.ResetToDefault();

        editor.Name.Should().Be("PlayStation default");
        editor.ButtonRows.Single(r => r.Control == KeyboardControl.B).SourceText.Should().Contain("Cross");
    }

    // ---- Preview ------------------------------------------------------------------------------------------------

    [Fact]
    public void ThePreviewUsesTheUnsavedValues()
    {
        var editor = Editor();
        _engine.Live[Pad.Id] = new InputState { RightX = 0.5f };
        editor.Live.Poll();
        var before = editor.Live.Output.RightX;

        editor.RightInner = 0.4;
        editor.Live.Poll();

        editor.Live.Output.RightX.Should().BeLessThan(before);
        editor.RightCurveSamples.Should().HaveCount(ProfileEditorViewModel.CurveSampleCount);
    }

    [Fact]
    public void TheCurveSamplesFollowTheCurve()
    {
        var editor = Editor();
        editor.LeftCurveSamples[32].Should().BeApproximately(0.5f, 1e-4f);

        editor.LeftCurve = Controlability.Core.Mapping.CurveType.Quadratic;

        editor.LeftCurveSamples[32].Should().BeApproximately(0.25f, 1e-4f);
    }

    // ---- Applies to and keyboard --------------------------------------------------------------------------------

    [Fact]
    public void AppliesTo_AddsGamesOnce_AndKeyboardTabFollowsTheKeyboardType()
    {
        var editor = Editor();
        editor.IsKeyboardProfile.Should().BeFalse();

        editor.NewProcessName = " game.exe ";
        editor.AddProcessCommand.Execute(null);
        editor.NewProcessName = "GAME";
        editor.AddProcessCommand.Execute(null);
        editor.ControllerTypes.Single(t => t.Type == ControllerType.Keyboard).IsChecked = true;
        editor.SelectedRunningApp = "Game";
        editor.AddSelectedProcessCommand.Execute(null);
        editor.SelectedRunningApp = "Other";
        editor.AddSelectedProcessCommand.Execute(null);
        editor.Save();

        var saved = _store.Profiles["mine"];
        saved.AppliesTo.ProcessNames.Should().Equal("game.exe", "Other");
        saved.AppliesTo.ControllerTypes.Should().Equal(ControllerType.Keyboard);
        editor.IsKeyboardProfile.Should().BeTrue();
    }

    [Fact]
    public void Browsing_AddsTheFileNameOfTheChosenProgram()
    {
        var editor = Editor();
        _dialogs.OpenPath = @"C:\Games\Cool\cool.exe";

        editor.BrowseProcessCommand.Execute(null);

        editor.ProcessNames.Should().Equal("cool.exe");
    }

    [Fact]
    public void KeyCapture_BindsTheKey_AndTakesItFromTheOldControl()
    {
        var profile = new Profile { Id = "kb", Name = "Keys" };
        profile.AppliesTo.ControllerTypes.Add(ControllerType.Keyboard);
        profile.Keyboard.Keys["W"] = "LeftStickUp";
        profile.Keyboard.Keys["Space"] = "A";
        _store.Profiles["kb"] = profile;
        var editor = Editor(_store.Get("kb"));

        editor.BeginKeyCapture(KeyboardControl.B);
        editor.IsCapturing.Should().BeTrue();
        editor.ApplyKeyCapture("Space").Should().BeTrue();
        editor.Save();

        var keys = _store.Profiles["kb"].Keyboard.Keys;
        keys.Should().ContainKey("Space").WhoseValue.Should().Be("B");
        keys.Should().ContainKey("W");
        editor.KeyRows.Single(r => r.Control == KeyboardControl.A).KeyText.Should().Be("not set");
    }

    [Fact]
    public void MouseSettings_AreEdited()
    {
        var editor = Editor();

        editor.MouseSensitivity = 2.5;
        editor.DecayMs = 120;
        editor.Save();

        _store.Profiles["mine"].Keyboard.MouseSensitivity.Should().Be(2.5f);
        _store.Profiles["mine"].Keyboard.DecayMs.Should().Be(120);
    }
}
