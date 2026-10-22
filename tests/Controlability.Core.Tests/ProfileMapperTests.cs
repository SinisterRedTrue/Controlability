using Controlability.Core.Mapping;
using Controlability.Core.Models;
using Controlability.Core.Profiles;

namespace Controlability.Core.Tests;

public class ProfileMapperTests
{
    [Fact]
    public void Default_profile_maps_to_the_default_settings()
    {
        var settings = ProfileMapper.ToMappingSettings(new Profile());

        settings.LeftStick.DeadZone.Should().Be(DeadZone.Default);
        settings.RightStick.Curve.Type.Should().Be(CurveType.Linear);
        settings.Triggers.Should().Be(TriggerSettings.Default);
        settings.Buttons[ButtonId.South].Should().Be(XboxButton.A);
    }

    [Fact]
    public void Sticks_and_triggers_are_carried_over()
    {
        var profile = new Profile
        {
            Sticks = new ProfileSticks
            {
                Left = new ProfileStick { Mode = DeadZoneMode.Axial, DeadZone = 0.2f, Outer = 0.9f, Curve = CurveType.Cubic },
                Right = new ProfileStick { Curve = CurveType.Custom, Points = [new CurvePoint(0.5f, 0.25f)] },
            },
            Triggers = new ProfileTriggers { DeadZone = 0.1f, Digital = true, DigitalThreshold = 0.7f },
        };

        var settings = ProfileMapper.ToMappingSettings(profile);

        settings.LeftStick.DeadZone.Should().Be(new DeadZone(DeadZoneMode.Axial, 0.2f, 0.9f));
        settings.LeftStick.Curve.Type.Should().Be(CurveType.Cubic);
        settings.RightStick.Curve.Apply(0.5f).Should().BeApproximately(0.25f, 1e-6f);
        settings.Triggers.Should().Be(new TriggerSettings(0.1f, true, 0.7f));
    }

    [Fact]
    public void Button_entries_override_the_generic_layout()
    {
        var profile = new Profile
        {
            Buttons = new Dictionary<string, string> { ["south"] = "B", ["Touchpad"] = "Back", ["East"] = "None" },
        };

        var buttons = ProfileMapper.ToMappingSettings(profile).Buttons;

        buttons[ButtonId.South].Should().Be(XboxButton.B);
        buttons[ButtonId.Touchpad].Should().Be(XboxButton.Back);
        buttons[ButtonId.East].Should().Be(XboxButton.None);
        buttons[ButtonId.North].Should().Be(XboxButton.Y);
    }

    [Fact]
    public void Shipped_playstation_and_nintendo_layouts_match_the_built_in_ones()
    {
        using var temp = new TempFolder();
        foreach (var file in Directory.GetFiles(Samples.RepoProfilesFolder(), "*.json"))
        {
            File.Copy(file, temp.Combine(Path.GetFileName(file)));
        }

        var store = new ProfileStore(temp.Path);
        var ps = ProfileMapper.ToMappingSettings(store.Get("default-playstation")!).Buttons;
        var nintendo = ProfileMapper.ToMappingSettings(store.Get("default-nintendo")!).Buttons;

        foreach (var source in Enum.GetValues<ButtonId>().Where(b => b != ButtonId.None))
        {
            ps[source].Should().Be(DefaultMappings.PlayStation[source], source.ToString());
            nintendo[source].Should().Be(DefaultMappings.Nintendo[source], source.ToString());
        }
    }

    [Theory]
    [InlineData("Nope", "A")]
    [InlineData("South", "Nope")]
    [InlineData("South, East", "A")]
    [InlineData("None", "A")]
    public void Unknown_button_names_throw(string source, string target)
    {
        var profile = new Profile { Buttons = new Dictionary<string, string> { [source] = target } };

        var act = () => ProfileMapper.ToMappingSettings(profile);

        act.Should().Throw<ProfileFormatException>();
    }

    [Fact]
    public void Out_of_range_values_throw()
    {
        var profile = new Profile { Sticks = new ProfileSticks { Left = new ProfileStick { DeadZone = 1.5f } } };

        var act = () => ProfileMapper.ToMappingSettings(profile);

        act.Should().Throw<ProfileFormatException>();
    }

    [Fact]
    public void Custom_curve_without_points_throws()
    {
        var profile = new Profile { Sticks = new ProfileSticks { Left = new ProfileStick { Curve = CurveType.Custom } } };

        var act = () => ProfileMapper.ToMappingSettings(profile);

        act.Should().Throw<ProfileFormatException>();
    }
}
