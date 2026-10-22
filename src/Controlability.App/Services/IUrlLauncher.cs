using System.Diagnostics;

namespace Controlability.App.Services;

/// <summary>Opens links and folders with the program Windows has chosen for them.</summary>
public interface IUrlLauncher
{
    /// <summary>Opens the URL in the default browser. Never throws.</summary>
    void Open(string url);
}

public sealed class ShellUrlLauncher(Action<string>? log = null) : IUrlLauncher
{
    public void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            log?.Invoke($"Could not open {url}: {ex.Message}");
        }
    }
}
