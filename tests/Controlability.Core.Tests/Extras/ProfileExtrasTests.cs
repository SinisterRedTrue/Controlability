using Controlability.Core.Extras;
using Controlability.Core.Models;
using Controlability.Core.Profiles;

namespace Controlability.Core.Tests.Extras;

public sealed class ProfileExtrasTests
{
    [Fact]
    public void Defaults_MeanNoExtras()
    {
        var settings = ProfileMapper.ToExtrasSettings(new Profile { Id = "p", Name = "p" });

        settings.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void AllFields_ReachTheSettings()
    {
        var profile = new Profile
        {
            Id = "p",
            Name = "p",
            Extras = new ProfileExtras
            {
                GyroToRightStick = true,
                GyroSensitivityX = 2f,
                GyroSensitivityY = 3f,
                GyroInvertY = true,
                GyroSmoothing = 0.5f,
                GyroActivation = GyroActivation.WhileHeld,
                GyroButton = "leftshoulder",
                TouchpadMouse = true,
                TouchpadSensitivity = 1.5f,
                TouchpadAcceleration = 1f,
                Turbo = new() { ["A"] = 12, ["x"] = 5 },
                Toggle = ["LeftThumb", "LeftThumb"],
            },
        };

        var settings = ProfileMapper.ToExtrasSettings(profile);

        settings.Gyro.Should().BeEquivalentTo(new GyroSettings
        {
            Enabled = true,
            SensitivityX = 2f,
            SensitivityY = 3f,
            InvertY = true,
            Smoothing = 0.5f,
            Activation = GyroActivation.WhileHeld,
            Button = ButtonId.LeftShoulder,
        });
        settings.Touchpad.Should().BeEquivalentTo(new TouchpadMouseSettings { Enabled = true, Sensitivity = 1.5f, Acceleration = 1f });
        settings.Turbo.Should().BeEquivalentTo(new Dictionary<XboxButton, int> { [XboxButton.A] = 12, [XboxButton.X] = 5 });
        settings.Toggle.Should().Equal(XboxButton.LeftThumb);
    }

    [Theory]
    [InlineData("turbo-name")]
    [InlineData("turbo-rate")]
    [InlineData("toggle-name")]
    [InlineData("gyro-button")]
    [InlineData("gyro-smoothing")]
    [InlineData("touchpad-speed")]
    public void BadValues_ThrowProfileFormatException(string problem)
    {
        var extras = new ProfileExtras();
        switch (problem)
        {
            case "turbo-name":
                extras.Turbo["Banana"] = 10;
                break;
            case "turbo-rate":
                extras.Turbo["A"] = 500;
                break;
            case "toggle-name":
                extras.Toggle.Add("None");
                break;
            case "gyro-button":
                extras.GyroButton = "South|East";
                break;
            case "gyro-smoothing":
                extras.GyroToRightStick = true;
                extras.GyroSmoothing = 2f;
                break;
            case "touchpad-speed":
                extras.TouchpadSensitivity = 0f;
                break;
        }

        FluentActions.Invoking(() => ProfileMapper.ToExtrasSettings(new Profile { Id = "p", Extras = extras }))
            .Should().Throw<ProfileFormatException>();
    }

    [Fact]
    public void ExtrasRoundTripThroughJson_AndAnOldFileLoadsWithDefaults()
    {
        var profile = new Profile { Id = "p", Name = "p" };
        profile.Extras.GyroToRightStick = true;
        profile.Extras.GyroActivation = GyroActivation.Toggle;
        profile.Extras.Turbo["B"] = 8;
        profile.Extras.Toggle.Add("Y");

        var copy = ProfileJson.Clone(profile);

        copy.Extras.Should().BeEquivalentTo(profile.Extras);
        ProfileJson.Serialize(profile).Should().Contain("\"gyroActivation\": \"Toggle\"");

        // A phase 1-10 file only has the three original keys; the new ones fall back to their defaults.
        var old = System.Text.Json.JsonSerializer.Deserialize(
            "{\"schemaVersion\":1,\"id\":\"old\",\"extras\":{\"gyroToRightStick\":true,\"touchpadMouse\":false,\"turbo\":{\"A\":10}}}",
            ProfilesJsonContext.Default.Profile)!;
        old.Extras.GyroToRightStick.Should().BeTrue();
        old.Extras.GyroSensitivityX.Should().Be(1f);
        old.Extras.GyroActivation.Should().Be(GyroActivation.Always);
        old.Extras.Toggle.Should().BeEmpty();
        old.SchemaVersion.Should().Be(Profile.CurrentSchemaVersion, "the format only gained optional keys");
    }
}

public sealed class GameProfileLookupTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly ProfileStore _store;

    public GameProfileLookupTests()
    {
        _store = new ProfileStore(_folder.Path);
    }

    public void Dispose() => _folder.Dispose();

    private static readonly DeviceInfo Ps4 = Samples.Device(ControllerType.PS4);

    [Fact]
    public void ProcessAndControllerType_BeatsProcessOnly_WhichBeatsTheTypeProfile()
    {
        _store.Save(Samples.Profile("type", types: [ControllerType.PS4]));
        _store.Save(Samples.Profile("any-pad", processes: ["game.exe"]));
        _store.Save(Samples.Profile("ps4-pad", types: [ControllerType.PS4], processes: ["game"]));

        _store.FindGameProfile(Ps4, "GAME")!.Id.Should().Be("ps4-pad");
        _store.FindGameProfile(Samples.Device(ControllerType.Xbox), "game")!.Id.Should().Be("any-pad");
        _store.FindFor(Ps4, null).Id.Should().Be("type");
    }

    [Fact]
    public void WithoutAGameProfile_ThereIsNoGameMatch_EvenThoughFindForHasAFallback()
    {
        _store.Save(Samples.Profile("type", types: [ControllerType.PS4]));
        _store.Save(Samples.Profile("other-game", processes: ["other"]));

        _store.FindGameProfile(Ps4, "game").Should().BeNull();
        _store.FindGameProfile(Ps4, null).Should().BeNull();
        _store.FindFor(Ps4, "game").Id.Should().Be("type");
    }

    [Fact]
    public void AGameProfileForAnotherControllerType_DoesNotMatch()
    {
        _store.Save(Samples.Profile("xbox-only", types: [ControllerType.Xbox], processes: ["game"]));

        _store.FindGameProfile(Ps4, "game").Should().BeNull();
    }

    [Fact]
    public void OnATie_AUserProfileBeatsAShippedOne()
    {
        _store.Save(Samples.Profile("default-game", processes: ["game"]));
        _store.Save(Samples.Profile("mine", processes: ["game"]));

        _store.FindGameProfile(Ps4, "game")!.Id.Should().Be("mine");
    }
}
