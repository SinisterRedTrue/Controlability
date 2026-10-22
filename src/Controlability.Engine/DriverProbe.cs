using Controlability.Output.ViGEm;
using Controlability.Platform.HidHide;

namespace Controlability.Engine;

/// <summary>Installed state and versions of the two drivers; the version is "unknown" when it cannot be read.</summary>
public readonly record struct DriverInfo(bool ViGEmInstalled, string ViGEmVersion, bool HidHideInstalled, string HidHideVersion);

/// <summary>Looks at the drivers on demand (the wizard re-checks every 2 s, the diagnostics read it once).</summary>
public interface IDriverProbe
{
    DriverInfo Read();
}

public sealed class DriverProbe : IDriverProbe
{
    private const string Unknown = "unknown";

    public DriverInfo Read()
    {
        var vigem = ViGEmDriverCheck.Check();
        var hidHide = HidHideCli.Locate();
        return new DriverInfo(vigem.IsInstalled, vigem.Version, hidHide is not null, hidHide is null ? Unknown : ReadHidHideVersion(hidHide));
    }

    private static string ReadHidHideVersion(string exePath)
    {
        try
        {
            var version = new HidHideCli(exePath).Version();
            return version.Length == 0 ? Unknown : version;
        }
        catch (HidHideCliException)
        {
            return Unknown;
        }
    }
}
