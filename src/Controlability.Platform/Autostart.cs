using Microsoft.Win32;

namespace Controlability.Platform;

/// <summary>Starts the app with Windows through a value in <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.</summary>
public interface IAutostart
{
    bool IsEnabled { get; }

    /// <summary>Adds or removes the Run value. Returns false (and does not throw) if the registry refused.</summary>
    bool Set(bool enabled);
}

public sealed class RegistryAutostart : IAutostart
{
    public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "Controlability";
    public const string MinimizedArgument = "--minimized";

    private readonly string _keyPath;
    private readonly string _valueName;
    private readonly Func<string?> _exePath;

    public RegistryAutostart(string? keyPath = null, string? valueName = null, Func<string?>? exePath = null)
    {
        _keyPath = keyPath ?? RunKeyPath;
        _valueName = valueName ?? ValueName;
        _exePath = exePath ?? (() => Environment.ProcessPath);
    }

    public bool IsEnabled
    {
        get
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(_keyPath);
                return key?.GetValue(_valueName) is string;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
            {
                return false;
            }
        }
    }

    public bool Set(bool enabled)
    {
        try
        {
            if (enabled)
            {
                if (_exePath() is not { Length: > 0 } exe)
                {
                    return false;
                }

                using var key = Registry.CurrentUser.CreateSubKey(_keyPath);
                key.SetValue(_valueName, CommandLine(exe), RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(_keyPath, writable: true);
                key?.DeleteValue(_valueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            return false;
        }
    }

    /// <summary>The quoted exe path plus <c>--minimized</c>, so a login start goes straight to the tray.</summary>
    public static string CommandLine(string exePath) => $"\"{exePath}\" {MinimizedArgument}";
}
