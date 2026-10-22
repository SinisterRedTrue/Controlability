using Controlability.App.Services;
using Controlability.App.ViewModels;
using Controlability.Core.Abstractions;
using Controlability.Core.Keyboard;
using Controlability.Core.Models;
using Controlability.Core.Profiles;
using Controlability.Core.Settings;
using Controlability.Engine;

namespace Controlability.App.Tests;

public sealed class SettingsViewModelTests : IDisposable
{
    private readonly FakeEngine _engine = new();
    private readonly FakeUiThread _ui = new();
    private readonly FakeClock _clock = new();
    private readonly Settings _settings = new();
    private readonly List<Settings> _saved = [];
    private readonly FakeProfileEditor _profiles = new();
    private readonly FakeRunningApps _apps = new("Dungeons", "notepad");
    private SettingsViewModel? _viewModel;

    private SettingsViewModel ViewModel => _viewModel ??= new SettingsViewModel(_engine, _settings, _saved.Add, _profiles, _apps, _ui, _clock);

    public void Dispose() => _viewModel?.Dispose();

    private static Profile KeyboardProfile() => new()
    {
        Id = "default-keyboard",
        Name = "Keyboard and mouse default",
        Keyboard = new ProfileKeyboard
        {
            Keys = new Dictionary<string, string> { ["W"] = "LeftStickUp", ["Space"] = "A", ["MouseLeft"] = "RightTrigger" },
        },
    };

    private void KeyboardInSlotOne()
    {
        _profiles.Profiles["default-keyboard"] = KeyboardProfile();
        _engine.Slots = [new SlotStatus(0, KeyboardPad.Id, "Keyboard + Mouse", "default-keyboard", SlotState.Active, 0), .. _engine.Slots.Skip(1)];
    }

    private KeyBindingRow Row(KeyboardControl control) => ViewModel.KeyRows.Single(row => row.Control == control);

    [Fact]
    public void Close_to_tray_follows_the_settings_and_is_saved_when_changed()
    {
        _settings.CloseToTray = false;

        ViewModel.CloseToTray.Should().BeFalse();
        _saved.Should().BeEmpty("loading the value is not a change");

        ViewModel.CloseToTray = true;

        _settings.CloseToTray.Should().BeTrue();
        _saved.Should().ContainSingle();
    }

    [Fact]
    public void The_game_list_starts_from_the_settings()
    {
        _settings.KeyboardTargetProcesses = ["Dungeons"];

        ViewModel.TargetProcesses.Should().Equal("Dungeons");
        ViewModel.HasTargets.Should().BeTrue();
        ViewModel.HasNoTargets.Should().BeFalse();
    }

    [Fact]
    public void Adding_a_game_trims_it_drops_exe_and_tells_the_engine()
    {
        ViewModel.NewProcessName = "  Dungeons.EXE ";

        ViewModel.AddProcessCommand.Execute(null);

        ViewModel.TargetProcesses.Should().Equal("Dungeons");
        ViewModel.NewProcessName.Should().BeEmpty();
        _engine.Calls.Should().Equal("SetKeyboardTargets:Dungeons");
    }

    [Fact]
    public void Blank_and_duplicate_games_are_ignored()
    {
        ViewModel.NewProcessName = "  ";
        ViewModel.AddProcessCommand.Execute(null);
        ViewModel.NewProcessName = "Dungeons";
        ViewModel.AddProcessCommand.Execute(null);
        ViewModel.NewProcessName = "dungeons";
        ViewModel.AddProcessCommand.Execute(null);

        ViewModel.TargetProcesses.Should().Equal("Dungeons");
        _engine.Calls.Should().ContainSingle();
    }

    [Fact]
    public void A_running_program_can_be_picked_and_removed_again()
    {
        ViewModel.RunningApps.Should().Equal("Dungeons", "notepad");
        ViewModel.SelectedRunningApp = "notepad";

        ViewModel.AddSelectedProcessCommand.Execute(null);
        ViewModel.RemoveProcessCommand.Execute("notepad");

        ViewModel.TargetProcesses.Should().BeEmpty();
        _engine.Calls.Should().Equal("SetKeyboardTargets:notepad", "SetKeyboardTargets:");
    }

    [Fact]
    public void The_keyboard_table_shows_the_keys_of_the_keyboard_profile()
    {
        KeyboardInSlotOne();

        ViewModel.Refresh();

        Row(KeyboardControl.LeftStickUp).KeyText.Should().Be("W");
        Row(KeyboardControl.A).KeyText.Should().Be("Space");
        Row(KeyboardControl.RightTrigger).KeyText.Should().Be("MouseLeft");
        Row(KeyboardControl.B).KeyText.Should().Be("not set");
        ViewModel.KeyboardProfileText.Should().Be("Profile: Keyboard and mouse default");
        ViewModel.HasKeyboardProfile.Should().BeTrue();
    }

    [Fact]
    public void Without_a_keyboard_profile_the_table_says_so_and_nothing_is_bound()
    {
        ViewModel.KeyboardProfileText.Should().Be("No keyboard profile was found.");
        ViewModel.HasKeyboardProfile.Should().BeFalse();
        ViewModel.KeyRows.Should().OnlyContain(row => row.KeyText == "not set");
    }

    [Fact]
    public void Changing_a_key_replaces_the_old_one_saves_the_profile_and_reapplies_it_to_the_engine()
    {
        KeyboardInSlotOne();
        ViewModel.Refresh();

        Row(KeyboardControl.A).ChangeCommand.Execute(null);
        ViewModel.IsCapturing.Should().BeTrue();
        Row(KeyboardControl.A).IsCapturing.Should().BeTrue();
        ViewModel.CapturePrompt.Should().Be("Press a key or mouse button for A...");

        ViewModel.ApplyCapture("F").Should().BeTrue();

        var saved = _profiles.Saved.Should().ContainSingle().Subject;
        saved.Keyboard.Keys.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["W"] = "LeftStickUp",
            ["MouseLeft"] = "RightTrigger",
            ["F"] = "A",
        });
        _engine.Calls.Should().Equal("SetProfile:0:default-keyboard");
        Row(KeyboardControl.A).KeyText.Should().Be("F");
        ViewModel.IsCapturing.Should().BeFalse();
    }

    [Fact]
    public void A_key_that_is_already_used_moves_to_the_new_control()
    {
        KeyboardInSlotOne();
        ViewModel.Refresh();

        Row(KeyboardControl.B).ChangeCommand.Execute(null);
        ViewModel.ApplyCapture("Space");

        var keys = _profiles.Saved.Single().Keyboard.Keys;
        keys["Space"].Should().Be("B");
        Row(KeyboardControl.A).KeyText.Should().Be("not set");
    }

    [Fact]
    public void A_mouse_button_can_be_bound_and_steals_the_old_binding()
    {
        KeyboardInSlotOne();
        ViewModel.Refresh();

        Row(KeyboardControl.X).ChangeCommand.Execute(null);
        ViewModel.ApplyCapture("MouseLeft");

        var keys = _profiles.Saved.Single().Keyboard.Keys;
        keys["MouseLeft"].Should().Be("X");
        Row(KeyboardControl.RightTrigger).KeyText.Should().Be("not set");
    }

    [Fact]
    public void An_alias_of_an_existing_key_is_not_bound_twice()
    {
        KeyboardInSlotOne();
        ViewModel.Refresh();

        Row(KeyboardControl.B).ChangeCommand.Execute(null);
        ViewModel.ApplyCapture("Spacebar");

        _profiles.Saved.Single().Keyboard.Keys.Keys.Should().NotContain("Space");
    }

    [Fact]
    public void Cancelling_a_capture_changes_nothing()
    {
        KeyboardInSlotOne();
        ViewModel.Refresh();
        Row(KeyboardControl.A).ChangeCommand.Execute(null);

        ViewModel.CancelCaptureCommand.Execute(null);

        ViewModel.IsCapturing.Should().BeFalse();
        ViewModel.ApplyCapture("F").Should().BeFalse("nothing is being captured");
        _profiles.Saved.Should().BeEmpty();
    }

    [Fact]
    public void Clearing_a_key_removes_it_from_the_profile()
    {
        KeyboardInSlotOne();
        ViewModel.Refresh();

        Row(KeyboardControl.A).ClearCommand.Execute(null);

        _profiles.Saved.Single().Keyboard.Keys.Should().NotContainKey("Space");
        Row(KeyboardControl.A).KeyText.Should().Be("not set");
    }

    [Fact]
    public void Resetting_brings_back_the_shipped_keys()
    {
        KeyboardInSlotOne();
        _profiles.Shipped["default-keyboard"] = KeyboardProfile() with
        {
            Keyboard = new ProfileKeyboard { Keys = new Dictionary<string, string> { ["E"] = "X" } },
        };
        ViewModel.Refresh();

        ViewModel.ResetKeysCommand.Execute(null);

        Row(KeyboardControl.X).KeyText.Should().Be("E");
        Row(KeyboardControl.A).KeyText.Should().Be("not set");
        _engine.Calls.Should().Equal("SetProfile:0:default-keyboard");
    }

    [Fact]
    public void Editing_without_the_keyboard_in_a_slot_still_saves_but_does_not_touch_the_engine()
    {
        _profiles.Profiles["default-keyboard"] = KeyboardProfile();
        ViewModel.Refresh();

        Row(KeyboardControl.A).ChangeCommand.Execute(null);
        ViewModel.ApplyCapture("F");

        _profiles.Saved.Should().ContainSingle();
        _engine.Calls.Should().BeEmpty();
    }

    [Fact]
    public void The_controller_table_shows_the_physical_buttons_of_the_connected_pad()
    {
        var pad = FakeEngine.MakeDevice("DS4");
        _engine.Devices = [pad];
        _engine.Slots = [FakeEngine.Active(0, pad, "default-playstation"), .. _engine.Slots.Skip(1)];
        _profiles.Profiles["default-playstation"] = new Profile { Id = "default-playstation", Name = "PlayStation default" };

        ViewModel.Refresh();

        ViewModel.ControllerSummary.Should().Be("DS4 - profile: PlayStation default");
        ViewModel.ControllerRows.Single(r => r.Label == "A").Button.Should().Be("Cross");
        ViewModel.ControllerRows.Single(r => r.Label == "Left bumper (LB)").Button.Should().Be("L1");
        ViewModel.ControllerRows.Single(r => r.Label == "Right trigger (RT)").Button.Should().Be("R2");
    }

    [Fact]
    public void A_remapped_button_shows_up_in_the_controller_table()
    {
        var pad = FakeEngine.MakeDevice("DS4");
        _engine.Devices = [pad];
        _engine.Slots = [FakeEngine.Active(0, pad, "custom"), .. _engine.Slots.Skip(1)];
        _profiles.Profiles["custom"] = new Profile
        {
            Id = "custom",
            Name = "Custom",
            Buttons = new Dictionary<string, string> { ["South"] = "B", ["East"] = "A" },
        };

        ViewModel.Refresh();

        ViewModel.ControllerRows.Single(r => r.Label == "B").Button.Should().Contain("Cross");
        ViewModel.ControllerRows.Single(r => r.Label == "A").Button.Should().Contain("Circle");
    }

    [Fact]
    public void Without_a_controller_the_table_falls_back_to_the_playstation_layout()
    {
        ViewModel.ControllerSummary.Should().Be("No controller is connected. Showing the PlayStation layout.");
        ViewModel.ControllerRows.Should().NotBeEmpty();
    }
}

public sealed class FakeProfileEditor : IProfileEditor
{
    public Dictionary<string, Profile> Profiles { get; } = [];

    public Dictionary<string, Profile> Shipped { get; } = [];

    public List<Profile> Saved { get; } = [];

    public Profile? Get(string id) => Profiles.TryGetValue(id, out var profile) ? Clone(profile) : null;

    public void Save(Profile profile)
    {
        Saved.Add(Clone(profile));
        Profiles[profile.Id] = Clone(profile);
    }

    public Profile? GetShipped(string id) => Shipped.TryGetValue(id, out var profile) ? Clone(profile) : null;

    public IReadOnlyList<Profile> GetAll() => [.. Profiles.Values.Select(Clone)];

    public void Delete(string id) => Profiles.Remove(id);

    /// <summary>What <see cref="TryImport"/> returns for a path, by path.</summary>
    public Dictionary<string, Profile> Files { get; } = [];

    public Dictionary<string, Profile> Exported { get; } = [];

    public bool TryImport(string path, out Profile profile, out string error)
    {
        if (Files.TryGetValue(path, out var found))
        {
            profile = Clone(found);
            error = "";
            return true;
        }

        profile = Profile.Generic;
        error = "Not a profile file.";
        return false;
    }

    public void Export(Profile profile, string path) => Exported[path] = Clone(profile);

    private static Profile Clone(Profile profile) => ProfileJson.Clone(profile);
}

public sealed class FakeRunningApps(params string[] names) : IRunningApps
{
    public IReadOnlyList<string> GetNames() => names;

    public bool IsRunning(string processName) => names.Contains(processName, StringComparer.OrdinalIgnoreCase);
}
