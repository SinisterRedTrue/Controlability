using System.Text.Json;
using Controlability.Core.IO;

namespace Controlability.Core.Settings;

/// <summary>
/// Reads and writes <c>settings.json</c>. A missing or unreadable file gives the defaults; an unreadable
/// one is first moved aside to <c>settings.broken.json</c> so the user's data is not overwritten.
/// </summary>
public sealed class SettingsStore
{
    private readonly string _path;
    private readonly Action<string>? _log;

    public SettingsStore(string path, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        _log = log;
    }

    public string Path => _path;

    public string BrokenPath => System.IO.Path.ChangeExtension(_path, ".broken.json");

    public Settings Load()
    {
        string text;
        try
        {
            if (!File.Exists(_path))
            {
                return new Settings();
            }

            text = File.ReadAllText(_path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Invoke($"Cannot read settings \"{_path}\": {ex.Message}");
            return new Settings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize(text, SettingsJsonContext.Default.Settings);
            if (settings is null)
            {
                throw new JsonException("The file is empty.");
            }

            if (settings.SchemaVersion > Settings.CurrentSchemaVersion)
            {
                throw new JsonException($"Schema version {settings.SchemaVersion} is newer than this app supports.");
            }

            return settings;
        }
        catch (JsonException ex)
        {
            _log?.Invoke($"Invalid settings \"{_path}\" moved aside, using defaults: {ex.Message}");
            MoveAside();
            return new Settings();
        }
    }

    public void Save(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings with { SchemaVersion = Settings.CurrentSchemaVersion };
        AtomicFile.WriteAllText(_path, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.Settings));
    }

    private void MoveAside()
    {
        try
        {
            File.Move(_path, BrokenPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Invoke($"Could not move \"{_path}\" aside: {ex.Message}");
        }
    }
}
