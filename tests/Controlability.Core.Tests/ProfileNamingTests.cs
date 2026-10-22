using Controlability.Core.Profiles;

namespace Controlability.Core.Tests;

public sealed class ProfileNamingTests
{
    [Fact]
    public void UniqueName_KeepsAFreeName()
    {
        ProfileNaming.UniqueName("  Racing ", ["Shooter"]).Should().Be("Racing");
    }

    [Fact]
    public void UniqueName_AddsTwoForTheFirstDuplicate_IgnoringCase()
    {
        ProfileNaming.UniqueName("racing", ["Racing"]).Should().Be("racing (2)");
    }

    [Fact]
    public void UniqueName_CountsUpPastTakenNumbers()
    {
        ProfileNaming.UniqueName("Racing", ["Racing", "Racing (2)", "Racing (3)"]).Should().Be("Racing (4)");
    }

    [Theory]
    [InlineData("My Game Profile!", "my-game-profile")]
    [InlineData("  ", "profile")]
    [InlineData("Ünïcode ✓", "n-code")]
    public void UniqueId_MakesAFileSafeSlug(string name, string expected)
    {
        ProfileNaming.UniqueId(name, []).Should().Be(expected);
    }

    [Fact]
    public void UniqueId_AvoidsTakenIds_AndStaysWithinTheStoreLimit()
    {
        ProfileNaming.UniqueId("racing", ["racing", "Racing-2"]).Should().Be("racing-3");

        var id = ProfileNaming.UniqueId(new string('a', 200), []);
        id.Length.Should().BeLessThanOrEqualTo(ProfileNaming.MaxIdLength);
        new ProfileStore(Path.GetTempPath()).TryParse(
            $$"""{ "schemaVersion": 1, "id": "{{id}}", "name": "x" }""", out _, out _).Should().BeTrue();
    }
}

public sealed class ProfileJsonTests
{
    [Fact]
    public void Clone_SharesNoCollectionWithTheOriginal()
    {
        var original = new Profile { Id = "a", Name = "A" };
        original.Buttons["South"] = "B";
        original.AppliesTo.ProcessNames.Add("game.exe");

        var clone = ProfileJson.Clone(original);
        clone.Buttons["East"] = "A";
        clone.AppliesTo.ProcessNames.Clear();
        clone.Sticks.Left.DeadZone = 0.3f;

        original.Buttons.Should().ContainSingle();
        original.AppliesTo.ProcessNames.Should().ContainSingle();
        original.Sticks.Left.DeadZone.Should().Be(0.08f);
    }

    [Fact]
    public void Serialize_IsEqualForEqualProfiles()
    {
        var a = new Profile { Id = "a", Name = "A" };
        ProfileJson.Serialize(ProfileJson.Clone(a)).Should().Be(ProfileJson.Serialize(a));
    }
}

public sealed class ProfileStoreTryParseTests
{
    private readonly ProfileStore _store = new(Path.Combine(Path.GetTempPath(), "controlability-tryparse-unused"));

    [Fact]
    public void ReturnsTheProfileForValidJson()
    {
        _store.TryParse("""{ "schemaVersion": 1, "id": "ok", "name": "Okay", "buttons": { "South": "B" } }""", out var profile, out var error)
            .Should().BeTrue();
        error.Should().BeEmpty();
        profile.Name.Should().Be("Okay");
        profile.Buttons["South"].Should().Be("B");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{ "schemaVersion": 1, "id": "bad id!", "name": "x" }""")]
    [InlineData("""{ "schemaVersion": 1, "id": "ok", "name": "" }""")]
    [InlineData("""{ "schemaVersion": 1, "id": "ok", "name": "x", "buttons": { "South": "Nope" } }""")]
    [InlineData("""{ "schemaVersion": 1, "id": "ok", "name": "x", "sticks": { "left": { "deadZone": 0.9, "outer": 0.5 } } }""")]
    public void ReturnsFalseWithAReasonForInvalidInput(string json)
    {
        _store.TryParse(json, out _, out var error).Should().BeFalse();
        error.Should().NotBeEmpty();
    }
}
