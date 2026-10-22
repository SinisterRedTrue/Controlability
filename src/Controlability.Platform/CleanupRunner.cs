using Controlability.Core.Abstractions;

namespace Controlability.Platform;

/// <summary>
/// What <c>Controlability.exe --cleanup</c> does for the uninstaller: gives back every controller we hid and removes
/// the start-with-Windows entry. No UI; never throws.
/// </summary>
public static class CleanupRunner
{
    public const string Argument = "--cleanup";

    /// <returns>0 if everything was undone, 1 if something could not be.</returns>
    public static int Run(IDeviceHider hider, IAutostart autostart, Action<string>? log = null)
    {
        var failed = false;

        try
        {
            hider.UnhideAll();
        }
        catch (Exception ex)
        {
            log?.Invoke($"Cleanup: unhide failed: {ex.Message}");
            failed = true;
        }

        if (!autostart.Set(false))
        {
            log?.Invoke("Cleanup: could not remove the autostart entry.");
            failed = true;
        }

        return failed ? 1 : 0;
    }
}
