using Controlability.Platform;

namespace Controlability.Engine.Tests;

public class AppPathsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "controlability-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void Paths_follow_the_plan_layout()
    {
        var paths = new AppPaths(@"C:\Roaming", @"C:\Local");

        paths.ProfilesFolder.Should().Be(@"C:\Roaming\Controlability\profiles");
        paths.SettingsFile.Should().Be(@"C:\Roaming\Controlability\settings.json");
        paths.LogsFolder.Should().Be(@"C:\Local\Controlability\logs");
    }

    [Fact]
    public void First_run_copies_the_defaults_and_later_runs_leave_them_alone()
    {
        var defaults = Path.Combine(_root, "defaults");
        Directory.CreateDirectory(defaults);
        File.WriteAllText(Path.Combine(defaults, "a.json"), "{}");
        File.WriteAllText(Path.Combine(defaults, "b.json"), "{}");
        var paths = new AppPaths(Path.Combine(_root, "roaming"), Path.Combine(_root, "local"));

        paths.EnsureCreated(defaults).Should().BeTrue();
        File.Delete(Path.Combine(paths.ProfilesFolder, "a.json"));
        paths.EnsureCreated(defaults).Should().BeFalse();

        Directory.GetFiles(paths.ProfilesFolder).Select(Path.GetFileName).Should().Equal("b.json");
        Directory.Exists(paths.LogsFolder).Should().BeTrue();
    }
}
