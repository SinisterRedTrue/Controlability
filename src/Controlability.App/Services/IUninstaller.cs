using System.Diagnostics;
using System.IO;

namespace Controlability.App.Services;

/// <summary>Starts the uninstaller that the Windows installer put next to the program.</summary>
public interface IUninstaller
{
    /// <summary>False for a copy that was not installed with the installer (a build from source, a portable folder).</summary>
    bool IsAvailable { get; }

    /// <summary>Starts the uninstaller. False if it is missing or Windows refused to start it (for example the user declined the admin prompt). Never throws.</summary>
    bool Start();
}

/// <summary>Finds the Inno Setup uninstaller (<c>unins000.exe</c>) in the install folder.</summary>
public sealed class InnoUninstaller(string appFolder, Action<string>? log = null) : IUninstaller
{
    public bool IsAvailable => Find() is not null;

    public bool Start()
    {
        var path = Find();
        if (path is null)
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true, WorkingDirectory = appFolder })?.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            log?.Invoke($"Could not start the uninstaller {path}: {ex.Message}");
            return false;
        }
    }

    private string? Find()
        => Directory.Exists(appFolder)
            ? Directory.GetFiles(appFolder, "unins*.exe").OrderBy(file => file, StringComparer.OrdinalIgnoreCase).FirstOrDefault()
            : null;
}
