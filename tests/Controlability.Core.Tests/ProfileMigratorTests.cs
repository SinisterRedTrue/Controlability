using System.Text.Json.Nodes;
using Controlability.Core.Profiles;

namespace Controlability.Core.Tests;

public class ProfileMigratorTests
{
    // A fake v0 profile called its display name "title"; v1 renamed it to "name".
    private static ProfileMigrator FakeV0ToV1() => new(1, new Dictionary<int, Action<JsonObject>>
    {
        [0] = root =>
        {
            root["name"] = root["title"]!.DeepClone();
            root.Remove("title");
        },
    });

    [Fact]
    public void Migrates_a_v0_document_to_the_current_version()
    {
        var root = JsonNode.Parse("""{ "schemaVersion": 0, "id": "old", "title": "Old" }""")!.AsObject();

        FakeV0ToV1().Migrate(root);

        root["schemaVersion"]!.GetValue<int>().Should().Be(1);
        root["name"]!.GetValue<string>().Should().Be("Old");
        root.ContainsKey("title").Should().BeFalse();
    }

    [Fact]
    public void Runs_steps_in_order()
    {
        var order = new List<int>();
        var migrator = new ProfileMigrator(3, new Dictionary<int, Action<JsonObject>>
        {
            [1] = _ => order.Add(1),
            [2] = _ => order.Add(2),
        });

        migrator.Migrate(JsonNode.Parse("""{ "schemaVersion": 1 }""")!.AsObject());

        order.Should().Equal(1, 2);
    }

    [Fact]
    public void Current_version_is_left_alone()
    {
        var root = JsonNode.Parse("""{ "schemaVersion": 1, "id": "x" }""")!.AsObject();

        ProfileMigrator.Default.Migrate(root).ToJsonString().Should().Be(JsonNode.Parse("""{ "schemaVersion": 1, "id": "x" }""")!.ToJsonString());
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 2 }""")]
    [InlineData("""{ "schemaVersion": 0 }""")]
    [InlineData("""{ "id": "x" }""")]
    [InlineData("""{ "schemaVersion": "one" }""")]
    public void Unsupported_versions_throw(string json)
    {
        var act = () => ProfileMigrator.Default.Migrate(JsonNode.Parse(json)!.AsObject());

        act.Should().Throw<ProfileFormatException>();
    }

    [Fact]
    public void Store_loads_a_v0_file_through_the_migrator()
    {
        using var temp = new TempFolder();
        File.WriteAllText(temp.Combine("old.json"), """{ "schemaVersion": 0, "id": "old", "title": "Old" }""");

        var profile = new ProfileStore(temp.Path, FakeV0ToV1()).Get("old");

        profile.Should().NotBeNull();
        profile!.Name.Should().Be("Old");
        profile.SchemaVersion.Should().Be(1);
    }
}
