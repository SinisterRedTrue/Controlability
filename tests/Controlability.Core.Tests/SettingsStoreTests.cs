using Controlability.Core.Settings;

namespace Controlability.Core.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Defaults_match_the_plan()
    {
        var settings = new Settings.Settings();

        settings.StartWithWindows.Should().BeFalse();
        settings.StartMinimized.Should().BeFalse();
        settings.Mode.Should().Be(KeyboardMode.KeyboardFirst);
        settings.SlotPolicy.Should().Be(SlotPolicy.Auto);
        settings.PollingRateHz.Should().Be(1000);
        settings.HidHideEnabled.Should().BeTrue();
        settings.Theme.Should().Be(AppTheme.System);
        settings.Language.Should().Be("system");
        settings.LogLevel.Should().Be(AppLogLevel.Information);
        settings.WindowPlacement.Should().BeNull();
    }

    [Fact]
    public void Missing_file_gives_defaults()
    {
        using var temp = new TempFolder();

        new SettingsStore(temp.Combine("settings.json")).Load().Should().Be(new Settings.Settings());
    }

    [Fact]
    public void Save_then_Load_round_trips()
    {
        using var temp = new TempFolder();
        var store = new SettingsStore(temp.Combine("sub", "settings.json"));
        var settings = new Settings.Settings
        {
            StartWithWindows = true,
            Mode = KeyboardMode.KeyboardAsController,
            SlotPolicy = SlotPolicy.Remember,
            PollingRateHz = 250,
            HidHideEnabled = false,
            Theme = AppTheme.Dark,
            Language = "de",
            LogLevel = AppLogLevel.Debug,
            WindowPlacement = new WindowPlacement(10, 20, 1000, 700, true),
        };

        store.Save(settings);

        store.Load().Should().Be(settings);
        File.ReadAllText(store.Path).Should().Contain("\"pollingRateHz\": 250").And.Contain("\"KeyboardAsController\"");
        Directory.GetFiles(temp.Combine("sub"), "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void Partial_file_fills_the_rest_with_defaults()
    {
        using var temp = new TempFolder();
        var path = temp.Combine("settings.json");
        File.WriteAllText(path, "{ // hi\n \"theme\": \"Dark\", }");

        var settings = new SettingsStore(path).Load();

        settings.Theme.Should().Be(AppTheme.Dark);
        settings.PollingRateHz.Should().Be(1000);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("null")]
    [InlineData("{ \"theme\": \"Neon\" }")]
    [InlineData("{ \"schemaVersion\": 99 }")]
    public void Bad_file_gives_defaults_and_is_moved_aside(string content)
    {
        using var temp = new TempFolder();
        var path = temp.Combine("settings.json");
        File.WriteAllText(path, content);
        var store = new SettingsStore(path);

        store.Load().Should().Be(new Settings.Settings());

        File.Exists(path).Should().BeFalse();
        File.ReadAllText(store.BrokenPath).Should().Be(content);
    }
}
