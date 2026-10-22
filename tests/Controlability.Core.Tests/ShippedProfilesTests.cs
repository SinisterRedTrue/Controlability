using Controlability.Core.Models;
using Controlability.Core.Profiles;

namespace Controlability.Core.Tests;

public class ShippedProfilesTests
{
    [Fact]
    public void Every_shipped_profile_loads_without_being_quarantined()
    {
        using var temp = new TempFolder();
        var files = Directory.GetFiles(Samples.RepoProfilesFolder(), "*.json");
        foreach (var file in files)
        {
            File.Copy(file, temp.Combine(Path.GetFileName(file)));
        }

        var store = new ProfileStore(temp.Path);
        var profiles = store.LoadAll();

        files.Select(Path.GetFileName).Should().BeEquivalentTo("playstation.json", "nintendo.json", "generic.json", "keyboard.json");
        profiles.Should().HaveCount(files.Length);
        Directory.Exists(store.BrokenFolder).Should().BeFalse();
        profiles.Select(p => p.Id).Should().BeEquivalentTo("default-playstation", "default-nintendo", "default-generic", "default-keyboard");
        profiles.Should().OnlyContain(p => p.SchemaVersion == Profile.CurrentSchemaVersion);
    }

    [Fact]
    public void Each_device_type_finds_its_shipped_profile()
    {
        using var temp = new TempFolder();
        foreach (var file in Directory.GetFiles(Samples.RepoProfilesFolder(), "*.json"))
        {
            File.Copy(file, temp.Combine(Path.GetFileName(file)));
        }

        var store = new ProfileStore(temp.Path);

        store.FindFor(Samples.Device(ControllerType.PS4), null).Id.Should().Be("default-playstation");
        store.FindFor(Samples.Device(ControllerType.PS5), null).Id.Should().Be("default-playstation");
        store.FindFor(Samples.Device(ControllerType.SwitchPro), null).Id.Should().Be("default-nintendo");
        store.FindFor(Samples.Device(ControllerType.Keyboard), null).Id.Should().Be("default-keyboard");
        store.FindFor(Samples.Device(ControllerType.Xbox), null).Id.Should().Be("default-generic");
        store.FindFor(Samples.Device(ControllerType.Generic), null).Id.Should().Be("default-generic");
    }

    [Fact]
    public void Keyboard_profile_has_the_plan_layout()
    {
        using var temp = new TempFolder();
        File.Copy(Path.Combine(Samples.RepoProfilesFolder(), "keyboard.json"), temp.Combine("keyboard.json"));

        var keyboard = new ProfileStore(temp.Path).Get("default-keyboard")!.Keyboard;

        keyboard.DecayMs.Should().Be(60);
        keyboard.Keys.Should().Contain("W", "LeftStickUp").And.Contain("Space", "A").And.Contain("Shift", "B")
            .And.Contain("MouseLeft", "RightTrigger").And.Contain("MouseRight", "LeftTrigger")
            .And.Contain("Tab", "Back").And.Contain("Escape", "Start").And.Contain("Ctrl", "LeftThumb");
    }
}
