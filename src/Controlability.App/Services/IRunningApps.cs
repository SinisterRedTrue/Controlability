using System.Diagnostics;

namespace Controlability.App.Services;

/// <summary>Names of the programs that have a window open, for picking the game in the settings.</summary>
public interface IRunningApps
{
    IReadOnlyList<string> GetNames();
}

public sealed class RunningApps : IRunningApps
{
    public IReadOnlyList<string> GetNames()
    {
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle != 0 && process.Id != Environment.ProcessId)
                {
                    names.Add(process.ProcessName);
                }
            }
            catch (Exception)
            {
                // Exited or access denied: not interesting.
            }
            finally
            {
                process.Dispose();
            }
        }

        return [.. names];
    }
}
