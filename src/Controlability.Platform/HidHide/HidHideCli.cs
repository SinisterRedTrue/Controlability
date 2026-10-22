using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace Controlability.Platform.HidHide;

public sealed class HidHideCliException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>The <c>HidHideCLI.exe</c> commands this app uses.</summary>
public interface IHidHideCli
{
    string ExePath { get; }

    string Version();

    bool IsCloakOn();

    void SetCloak(bool on);

    bool IsInverseOn();

    IReadOnlyList<string> GetApps();

    void RegisterApp(string exePath);

    /// <summary>Instance paths of the devices HidHide hides right now (all of them, not only ours).</summary>
    IReadOnlyList<string> GetHiddenDevices();

    void Hide(string instancePath);

    void Unhide(string instancePath);

    IReadOnlyList<HidDevice> GetGamingDevices();
}

/// <summary>Runs <c>HidHideCLI.exe</c>: no shell, arguments via <see cref="ProcessStartInfo.ArgumentList"/>, 5 s timeout.</summary>
public sealed class HidHideCli(string exePath) : IHidHideCli
{
    private const string DefaultRelativePath = @"Nefarius Software Solutions\HidHide\x64\HidHideCLI.exe";
    private const string ExeName = "HidHideCLI.exe";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public string ExePath { get; } = exePath;

    /// <summary>Finds <c>HidHideCLI.exe</c>: Program Files default, then the uninstall registry entry, then PATH.</summary>
    public static string? Locate()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            var candidate = Path.Combine(programFiles, DefaultRelativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        if (FindInRegistry() is { } fromRegistry)
        {
            return fromRegistry;
        }

        return FindOnPath(Environment.GetEnvironmentVariable("PATH"), File.Exists);
    }

    public static string? FindOnPath(string? pathVariable, Func<string, bool> fileExists)
    {
        if (string.IsNullOrEmpty(pathVariable))
        {
            return null;
        }

        foreach (var folder in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(folder.Trim('"'), ExeName);
                if (fileExists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // A malformed PATH entry is not our problem.
            }
        }

        return null;
    }

    public string Version() => Run("--version").Trim();

    public bool IsCloakOn() => Run("--cloak-state").Contains("--cloak-on", StringComparison.OrdinalIgnoreCase);

    public void SetCloak(bool on) => Run(on ? "--cloak-on" : "--cloak-off");

    public bool IsInverseOn() => Run("--inv-state").Contains("--inv-on", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetApps() => ParseQuotedValues(Run("--app-list"));

    public void RegisterApp(string exePath) => Run("--app-reg", exePath);

    public IReadOnlyList<string> GetHiddenDevices() => ParseQuotedValues(Run("--dev-list"));

    public void Hide(string instancePath) => Run("--dev-hide", instancePath);

    public void Unhide(string instancePath) => Run("--dev-unhide", instancePath);

    public IReadOnlyList<HidDevice> GetGamingDevices()
    {
        try
        {
            return HidDevice.ParseList(Run("--dev-gaming"));
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new HidHideCliException($"Could not read the output of --dev-gaming: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// The list commands print one command per line, e.g. <c>--dev-hide "HID\VID_..."</c> or <c>--app-reg "C:\...\x.exe"</c>.
    /// Returns the quoted part of each line.
    /// </summary>
    public static IReadOnlyList<string> ParseQuotedValues(string output)
    {
        var values = new List<string>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var first = line.IndexOf('"');
            var last = line.LastIndexOf('"');
            if (first >= 0 && last > first)
            {
                values.Add(line[(first + 1)..last]);
            }
        }

        return values;
    }

    private string Run(params string[] arguments)
    {
        var info = new ProcessStartInfo(ExePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        var description = $"{ExeName} {string.Join(' ', arguments)}";
        try
        {
            using var process = Process.Start(info) ?? throw new HidHideCliException($"Could not start {description}.");
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(Timeout))
            {
                process.Kill(entireProcessTree: true);
                throw new HidHideCliException($"{description} did not finish within {Timeout.TotalSeconds:0} s.");
            }

            // Closing the pipes ends the reads once the process is gone.
            Task.WaitAll(output, error);
            if (process.ExitCode != 0)
            {
                var detail = string.IsNullOrWhiteSpace(error.Result) ? output.Result : error.Result;
                throw new HidHideCliException($"{description} failed with exit code {process.ExitCode}: {detail.Trim()}");
            }

            return output.Result;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            throw new HidHideCliException($"Could not run {description}: {ex.Message}", ex);
        }
    }

    private static string? FindInRegistry()
    {
        const string uninstall = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                using var root = baseKey.OpenSubKey(uninstall);
                if (root is null)
                {
                    continue;
                }

                foreach (var name in root.GetSubKeyNames())
                {
                    using var entry = root.OpenSubKey(name);
                    if (entry?.GetValue("DisplayName") is not string displayName
                        || !displayName.Contains("HidHide", StringComparison.OrdinalIgnoreCase)
                        || entry.GetValue("InstallLocation") is not string location
                        || string.IsNullOrWhiteSpace(location))
                    {
                        continue;
                    }

                    foreach (var relative in new[] { Path.Combine("x64", ExeName), ExeName })
                    {
                        var candidate = Path.Combine(location, relative);
                        if (File.Exists(candidate))
                        {
                            return candidate;
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException or ArgumentException)
            {
                // Registry not readable: fall through to PATH.
            }
        }

        return null;
    }
}
