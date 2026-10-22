using System.Diagnostics;
using Nefarius.ViGEm.Client;

namespace Controlability.Output.ViGEm;

/// <summary>Result of <see cref="ViGEmDriverCheck.Check"/>. <see cref="Version"/> is "unknown" when it cannot be read.</summary>
public readonly record struct ViGEmDriverStatus(bool IsInstalled, string Version, string? Error);

public static class ViGEmDriverCheck
{
    private const string Unknown = "unknown";

    public static ViGEmDriverStatus Check()
    {
        try
        {
            using var client = new ViGEmClient();
            return new ViGEmDriverStatus(true, ReadDriverVersion(), null);
        }
        catch (Exception ex)
        {
            return new ViGEmDriverStatus(false, Unknown, ex.Message);
        }
    }

    private static string ReadDriverVersion()
    {
        try
        {
            var path = Path.Combine(Environment.SystemDirectory, "drivers", "ViGEmBus.sys");
            var version = FileVersionInfo.GetVersionInfo(path).FileVersion;
            return string.IsNullOrWhiteSpace(version) ? Unknown : version;
        }
        catch (Exception)
        {
            return Unknown;
        }
    }
}
