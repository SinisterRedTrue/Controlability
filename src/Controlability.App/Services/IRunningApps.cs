using System.Diagnostics;

namespace Controlability.App.Services;

/// <summary>Names of the programs that have a window open, for picking the game in the settings.</summary>
public interface IRunningApps
{
    IReadOnlyList<string> GetNames();

    /// <summary>Whether a process with this name runs, with or without a window (Steam often sits in the tray).</summary>
    bool IsRunning(string processName);
}

public sealed class RunningApps : IRunningApps
{
    public bool IsRunning(string processName)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return processes.Length > 0;
    }

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
