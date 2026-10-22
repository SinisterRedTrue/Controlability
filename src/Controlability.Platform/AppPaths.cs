namespace Controlability.Platform;

/// <summary>
/// Where Controlability keeps its files: settings and profiles in <c>%APPDATA%\Controlability</c>,
/// logs and the HidHide journal in <c>%LOCALAPPDATA%\Controlability</c>.
/// </summary>
public sealed class AppPaths
{
    private const string AppFolderName = "Controlability";

    public AppPaths(string roamingRoot, string localRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roamingRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(localRoot);

        DataFolder = Path.Combine(roamingRoot, AppFolderName);
        ProfilesFolder = Path.Combine(DataFolder, "profiles");
        SettingsFile = Path.Combine(DataFolder, "settings.json");
        LogsFolder = Path.Combine(localRoot, AppFolderName, "logs");
        HiddenJournalFile = Path.Combine(localRoot, AppFolderName, "hidden.json");
    }

    public string DataFolder { get; }

    public string ProfilesFolder { get; }

    public string SettingsFile { get; }

    public string LogsFolder { get; }

    /// <summary>Devices HidHide currently hides on our behalf; read on the next start to undo a crash.</summary>
    public string HiddenJournalFile { get; }

    public static AppPaths ForCurrentUser() => new(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

    /// <summary>
    /// Creates the folders. On the first run (no profiles folder yet) the shipped profiles are copied from
    /// <paramref name="defaultProfilesFolder"/>; later runs leave the user's files alone, so a deleted default stays deleted.
    /// </summary>
    /// <returns>True if this was the first run.</returns>
    public bool EnsureCreated(string defaultProfilesFolder)
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(LogsFolder);

        var firstRun = !Directory.Exists(ProfilesFolder);
        Directory.CreateDirectory(ProfilesFolder);
        if (firstRun && Directory.Exists(defaultProfilesFolder))
        {
            foreach (var file in Directory.GetFiles(defaultProfilesFolder, "*.json"))
            {
                File.Copy(file, Path.Combine(ProfilesFolder, Path.GetFileName(file)), overwrite: false);
            }
        }

        return firstRun;
    }
}
