using Controlability.Core.Models;
using Controlability.Core.Profiles;

namespace Controlability.Core.Tests;

internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "controlability-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

internal static class Samples
{
    public static Profile Profile(string id, ControllerType[]? types = null, string[]? processes = null) => new()
    {
        Id = id,
        Name = id,
        AppliesTo = new ProfileTargets
        {
            ControllerTypes = [.. types ?? []],
            ProcessNames = [.. processes ?? []],
        },
    };

    public static DeviceInfo Device(ControllerType type) => new(
        new DeviceId("test"), "Test pad", 0x054C, 0x09CC, null, null, ConnectionType.Usb, type, DeviceCapabilities.None);

    public static string RepoProfilesFolder()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "Controlability.sln")))
        {
            directory = directory.Parent;
        }

        return System.IO.Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Repo root not found."), "profiles");
    }
}
