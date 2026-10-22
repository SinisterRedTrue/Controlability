using Controlability.Core.Settings;
using Controlability.Platform.Logging;

namespace Controlability.Engine.Tests.Hardening;

public sealed class RollingFileLogTests : IDisposable
{
    private readonly string _folder = Path.Combine(Directory.CreateTempSubdirectory("controlability-log-tests").FullName, "logs");
    private DateTime _now = new(2026, 10, 22, 12, 0, 0);

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(_folder)!, recursive: true);

    private RollingFileLog Create(AppLogLevel level = AppLogLevel.Information) => new(_folder, level, () => _now);

    [Fact]
    public void Writes_one_file_per_day_named_after_the_date()
    {
        var log = Create();

        log.Write(AppLogLevel.Information, "Test", "first");
        _now = _now.AddDays(1);
        log.Write(AppLogLevel.Information, "Test", "second");

        Directory.GetFiles(_folder).Select(Path.GetFileName).Order()
            .Should().Equal("app-20261022.log", "app-20261023.log");
        File.ReadAllText(Path.Combine(_folder, "app-20261022.log")).Should().Contain("[INF] Test: first");
    }

    [Fact]
    public void Drops_entries_below_the_level_and_follows_a_level_change()
    {
        var log = Create(AppLogLevel.Warning);

        log.Write(AppLogLevel.Information, "Test", "hidden");
        log.Level = AppLogLevel.Debug;
        log.Write(AppLogLevel.Debug, "Test", "shown");

        var text = File.ReadAllText(Path.Combine(_folder, "app-20261022.log"));
        text.Should().NotContain("hidden").And.Contain("[DBG] Test: shown");
    }

    [Fact]
    public void Deletes_files_older_than_seven_days_but_keeps_the_last_seven()
    {
        Directory.CreateDirectory(_folder);
        foreach (var day in new[] { "20261014", "20261015", "20261016", "20261022" })
        {
            File.WriteAllText(Path.Combine(_folder, $"app-{day}.log"), "old");
        }

        File.WriteAllText(Path.Combine(_folder, "notes.txt"), "keep me");

        Create().Write(AppLogLevel.Information, "Test", "today");

        // Today is the 22nd: the 16th is the oldest of the last seven days.
        Directory.GetFiles(_folder).Select(Path.GetFileName).Order()
            .Should().Equal("app-20261016.log", "app-20261022.log", "notes.txt");
    }

    [Fact]
    public void ReadTail_returns_the_newest_lines_across_files_oldest_first()
    {
        var log = Create();
        log.Write(AppLogLevel.Information, "T", "one");
        log.Write(AppLogLevel.Information, "T", "two");
        _now = _now.AddDays(1);
        log.Write(AppLogLevel.Information, "T", "three");

        var tail = log.ReadTail(2);

        tail.Should().HaveCount(2);
        tail[0].Should().EndWith("two");
        tail[1].Should().EndWith("three");
    }

    [Fact]
    public void ReadTail_of_a_missing_folder_is_empty()
        => Create().ReadTail(10).Should().BeEmpty();

    [Fact]
    public void Writing_to_an_unusable_folder_does_not_throw()
    {
        var blocker = Path.Combine(Path.GetDirectoryName(_folder)!, "file");
        File.WriteAllText(blocker, "x");
        var log = new RollingFileLog(Path.Combine(blocker, "logs"), AppLogLevel.Information, () => _now);

        var act = () => log.Write(AppLogLevel.Error, "T", "boom");

        act.Should().NotThrow();
    }
}
