using System.Text.Json;
using Controlability.Core.Mapping;
using Controlability.Core.Models;
using Controlability.Core.Profiles;

namespace Controlability.Core.Tests;

public class ProfileStoreTests
{
    [Fact]
    public void Save_then_LoadAll_round_trips_every_field()
    {
        using var temp = new TempFolder();
        var store = new ProfileStore(temp.Path);
        var profile = new Profile
        {
            Id = "mine",
            Name = "Mine",
            AppliesTo = new ProfileTargets { ControllerTypes = [ControllerType.PS5], ProcessNames = ["game.exe"] },
            Sticks = new ProfileSticks
            {
                Left = new ProfileStick { Mode = DeadZoneMode.Axial, DeadZone = 0.1f, Outer = 0.9f, Curve = CurveType.Quadratic },
                Right = new ProfileStick { Curve = CurveType.Custom, Points = [new CurvePoint(0.5f, 0.25f)] },
            },
            Triggers = new ProfileTriggers { DeadZone = 0.05f, Digital = true, DigitalThreshold = 0.6f },
            Buttons = new Dictionary<string, string> { ["South"] = "B" },
            Extras = new ProfileExtras { GyroToRightStick = true, Turbo = new Dictionary<string, int> { ["A"] = 10 } },
            Led = new ProfileLed { Color = "#FF0000" },
            Keyboard = new ProfileKeyboard { Keys = new Dictionary<string, string> { ["W"] = "LeftStickUp" }, MouseSensitivity = 2f, DecayMs = 80 },
        };

        store.Save(profile);
        var loaded = store.LoadAll();

        loaded.Should().ContainSingle().Which.Should().BeEquivalentTo(profile);
        store.Get("mine").Should().BeEquivalentTo(profile);
    }

    [Fact]
    public void Saved_json_uses_camelCase_and_string_enums()
    {
        using var temp = new TempFolder();
        new ProfileStore(temp.Path).Save(Samples.Profile("p", [ControllerType.PS4]));

        var json = File.ReadAllText(temp.Combine("p.json"));

        json.Should().Contain("\"schemaVersion\": 1").And.Contain("\"controllerTypes\"").And.Contain("\"PS4\"");
        Directory.GetFiles(temp.Path, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void Save_replaces_the_existing_file_for_the_same_id_even_if_named_differently()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.Combine("custom-name.json"), JsonSerializer.Serialize(Samples.Profile("x"), ProfilesJsonContext.Default.Profile));
        var store = new ProfileStore(temp.Path);

        store.Save(Samples.Profile("x") with { Name = "Renamed" });

        Directory.GetFiles(temp.Path, "*.json").Should().ContainSingle().Which.Should().EndWith("custom-name.json");
        store.Get("x")!.Name.Should().Be("Renamed");
    }

    [Fact]
    public void Reads_comments_and_trailing_commas()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.Combine("c.json"), """
            {
              // a comment
              "schemaVersion": 1,
              "id": "c",
              "name": "C",
            }
            """);

        new ProfileStore(temp.Path).Get("c").Should().NotBeNull();
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("[]")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"../evil\", \"name\": \"X\" }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"ok\", \"name\": \"\" }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"ok\", \"name\": \"X\", \"sticks\": { \"left\": { \"deadZone\": 5 } } }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"ok\", \"name\": \"X\", \"buttons\": { \"Nope\": \"A\" } }")]
    [InlineData("{ \"schemaVersion\": 1, \"id\": \"ok\", \"name\": \"X\", \"appliesTo\": { \"controllerTypes\": [\"Toaster\"] } }")]
    [InlineData("{ \"schemaVersion\": 99, \"id\": \"ok\", \"name\": \"X\" }")]
    [InlineData("{ \"id\": \"ok\", \"name\": \"X\" }")]
    public void Invalid_file_is_quarantined_with_a_reason_and_does_not_throw(string content)
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.Combine("bad.json"), content);
        File.WriteAllText(temp.Combine("good.json"), JsonSerializer.Serialize(Samples.Profile("good"), ProfilesJsonContext.Default.Profile));
        var log = new List<string>();
        var store = new ProfileStore(temp.Path, log: log.Add);

        var loaded = store.LoadAll();

        loaded.Should().ContainSingle().Which.Id.Should().Be("good");
        File.Exists(temp.Combine("bad.json")).Should().BeFalse();
        File.Exists(temp.Combine("broken", "bad.json")).Should().BeTrue();
        File.ReadAllText(temp.Combine("broken", "bad.json.reason.txt")).Should().NotBeNullOrWhiteSpace();
        log.Should().ContainSingle();
    }

    [Fact]
    public void Second_file_with_a_duplicate_id_is_quarantined()
    {
        using var temp = new TempFolder();
        var json = JsonSerializer.Serialize(Samples.Profile("same"), ProfilesJsonContext.Default.Profile);
        File.WriteAllText(temp.Combine("a.json"), json);
        File.WriteAllText(temp.Combine("b.json"), json);

        new ProfileStore(temp.Path).LoadAll().Should().ContainSingle();

        File.Exists(temp.Combine("broken", "b.json")).Should().BeTrue();
    }

    [Fact]
    public void Missing_folder_gives_an_empty_list()
    {
        using var temp = new TempFolder();

        new ProfileStore(temp.Combine("nope")).LoadAll().Should().BeEmpty();
    }

    [Fact]
    public void Save_rejects_an_unsafe_id()
    {
        using var temp = new TempFolder();

        var act = () => new ProfileStore(temp.Path).Save(Samples.Profile("..\\evil"));

        act.Should().Throw<ProfileFormatException>();
    }

    [Fact]
    public void Duplicate_saves_a_copy_with_a_unique_id()
    {
        using var temp = new TempFolder();
        var store = new ProfileStore(temp.Path);
        store.Save(Samples.Profile("default-a") with { Name = "A" });

        var first = store.Duplicate("default-a")!;
        var second = store.Duplicate("default-a")!;

        first.Id.Should().Be("a-copy");
        first.Name.Should().Be("A (copy)");
        second.Id.Should().Be("a-copy-2");
        store.LoadAll().Should().HaveCount(3);
        store.Duplicate("missing").Should().BeNull();
    }

    [Fact]
    public void Delete_removes_the_file()
    {
        using var temp = new TempFolder();
        var store = new ProfileStore(temp.Path);
        store.Save(Samples.Profile("gone"));

        store.Delete("gone").Should().BeTrue();
        store.Delete("gone").Should().BeFalse();
        store.LoadAll().Should().BeEmpty();
    }

    [Fact]
    public void FindFor_prefers_process_over_controller_type_over_generic()
    {
        using var temp = new TempFolder();
        var store = new ProfileStore(temp.Path);
        store.Save(Samples.Profile("default-generic"));
        store.Save(Samples.Profile("default-ps", [ControllerType.PS4, ControllerType.PS5]));
        store.Save(Samples.Profile("elden", processes: ["EldenRing.exe"]));
        var ps4 = Samples.Device(ControllerType.PS4);

        store.FindFor(ps4, "eldenring").Id.Should().Be("elden");
        store.FindFor(ps4, "ELDENRING.EXE").Id.Should().Be("elden");
        store.FindFor(ps4, "other").Id.Should().Be("default-ps");
        store.FindFor(ps4, null).Id.Should().Be("default-ps");
        store.FindFor(Samples.Device(ControllerType.Xbox), "other").Id.Should().Be("default-generic");
    }

    [Fact]
    public void FindFor_process_profile_with_controller_types_needs_both_to_match()
    {
        using var temp = new TempFolder();
        var store = new ProfileStore(temp.Path);
        store.Save(Samples.Profile("default-generic"));
        store.Save(Samples.Profile("game-ps", [ControllerType.PS5], ["game"]));

        store.FindFor(Samples.Device(ControllerType.PS5), "game").Id.Should().Be("game-ps");
        store.FindFor(Samples.Device(ControllerType.Xbox), "game").Id.Should().Be("default-generic");
    }

    [Fact]
    public void FindFor_user_profile_beats_a_shipped_default_with_the_same_targets()
    {
        using var temp = new TempFolder();
        var store = new ProfileStore(temp.Path);
        store.Save(Samples.Profile("default-ps", [ControllerType.PS4]));
        store.Save(Samples.Profile("a-mine", [ControllerType.PS4]));
        store.Save(Samples.Profile("z-mine", [ControllerType.PS4]));

        store.FindFor(Samples.Device(ControllerType.PS4), null).Id.Should().Be("a-mine");
    }

    [Fact]
    public void FindFor_falls_back_to_the_built_in_generic_profile_when_nothing_is_stored()
    {
        using var temp = new TempFolder();

        new ProfileStore(temp.Path).FindFor(Samples.Device(ControllerType.PS4), null).Should().BeSameAs(Profile.Generic);
    }
}
