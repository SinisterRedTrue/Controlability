using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Controlability.Core.IO;
using Controlability.Core.Models;

namespace Controlability.Core.Profiles;

/// <summary>
/// Profiles as JSON files in one folder. Unreadable or invalid files are moved to <c>broken/</c> next to a
/// <c>.reason.txt</c> and never surface as exceptions from the read methods.
/// </summary>
public sealed partial class ProfileStore
{
    public const string BrokenFolderName = "broken";
    private const string DefaultIdPrefix = "default-";

    private static readonly JsonDocumentOptions DocumentOptions = new()
    {
        CommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _folder;
    private readonly ProfileMigrator _migrator;
    private readonly Action<string>? _log;

    /// <param name="folder">Folder holding the profile files; created on first save.</param>
    /// <param name="migrator">Defaults to <see cref="ProfileMigrator.Default"/>.</param>
    /// <param name="log">Receives a line for every file that was quarantined or skipped.</param>
    public ProfileStore(string folder, ProfileMigrator? migrator = null, Action<string>? log = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        _folder = folder;
        _migrator = migrator ?? ProfileMigrator.Default;
        _log = log;
    }

    public string Folder => _folder;

    public string BrokenFolder => Path.Combine(_folder, BrokenFolderName);

    public IReadOnlyList<Profile> LoadAll() => Scan().Select(entry => entry.Profile).ToList();

    public Profile? Get(string id) => Scan().FirstOrDefault(entry => entry.Profile.Id == id).Profile;

    /// <summary>
    /// Reads one profile from JSON text (for importing a file): migrates and validates it like a stored file.
    /// Returns false with a readable reason instead of throwing.
    /// </summary>
    public bool TryParse(string text, out Profile profile, out string error)
    {
        try
        {
            profile = Parse(text);
            error = "";
            return true;
        }
        catch (ProfileFormatException ex)
        {
            profile = Profile.Generic;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Writes the profile atomically; an existing file for the same id is replaced wherever it is named.</summary>
    public void Save(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile = profile with { SchemaVersion = _migrator.CurrentVersion };
        Validate(profile);

        var path = Scan().Where(entry => entry.Profile.Id == profile.Id).Select(entry => entry.Path).FirstOrDefault()
            ?? Path.Combine(_folder, profile.Id + ".json");
        AtomicFile.WriteAllText(path, JsonSerializer.Serialize(profile, ProfilesJsonContext.Default.Profile));
    }

    /// <summary>Saves a copy with a new id and "(copy)" name, or returns null if <paramref name="id"/> is unknown.</summary>
    public Profile? Duplicate(string id)
    {
        var entries = Scan();
        var source = entries.FirstOrDefault(entry => entry.Profile.Id == id).Profile;
        if (source is null)
        {
            return null;
        }

        var taken = entries.Select(entry => entry.Profile.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var baseId = source.Id.StartsWith(DefaultIdPrefix, StringComparison.Ordinal)
            ? source.Id[DefaultIdPrefix.Length..]
            : source.Id;
        var newId = $"{baseId}-copy";
        for (var n = 2; taken.Contains(newId); n++)
        {
            newId = $"{baseId}-copy-{n}";
        }

        var copy = source with { Id = newId, Name = $"{source.Name} (copy)" };
        Save(copy);
        return copy;
    }

    /// <summary>Returns false if there was no such profile.</summary>
    public bool Delete(string id)
    {
        var path = Scan().Where(entry => entry.Profile.Id == id).Select(entry => entry.Path).FirstOrDefault();
        if (path is null)
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    /// <summary>
    /// Picks the profile for a device and the foreground game. A process match beats a controller type match,
    /// which beats the generic default (empty <c>appliesTo</c>). On a tie a user profile beats a shipped
    /// <c>default-*</c> one, then file name order decides. Falls back to <see cref="Profile.Generic"/>.
    /// </summary>
    public Profile FindFor(DeviceInfo device, string? processName)
    {
        ArgumentNullException.ThrowIfNull(device);

        var process = NormalizeProcess(processName);
        Profile? best = null;
        var bestRank = 0;
        foreach (var profile in LoadAll())
        {
            var rank = RankFor(profile.AppliesTo, device.Type, process);
            if (rank > bestRank || (rank == bestRank && best is not null && IsUserProfile(profile) && !IsUserProfile(best)))
            {
                best = profile;
                bestRank = rank;
            }
        }

        return best ?? Profile.Generic;
    }

    private static int RankFor(ProfileTargets targets, ControllerType type, string? process)
    {
        var typeMatches = targets.ControllerTypes.Contains(type);
        if (targets.ProcessNames.Count > 0)
        {
            var processMatches = process is not null
                && targets.ProcessNames.Any(name => string.Equals(NormalizeProcess(name), process, StringComparison.OrdinalIgnoreCase));
            return processMatches && (targets.ControllerTypes.Count == 0 || typeMatches) ? 3 : 0;
        }

        if (targets.ControllerTypes.Count > 0)
        {
            return typeMatches ? 2 : 0;
        }

        return 1;
    }

    private static string? NormalizeProcess(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        name = name.Trim();
        return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
    }

    private static bool IsUserProfile(Profile profile) => !profile.Id.StartsWith(DefaultIdPrefix, StringComparison.Ordinal);

    private List<(Profile Profile, string Path)> Scan()
    {
        var result = new List<(Profile Profile, string Path)>();
        if (!Directory.Exists(_folder))
        {
            return result;
        }

        string[] files;
        try
        {
            files = Directory.GetFiles(_folder, "*.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Invoke($"Cannot list profiles in \"{_folder}\": {ex.Message}");
            return result;
        }

        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        foreach (var path in files)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log?.Invoke($"Skipped profile \"{path}\": {ex.Message}");
                continue;
            }

            try
            {
                var profile = Parse(text);
                if (result.Any(entry => string.Equals(entry.Profile.Id, profile.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ProfileFormatException($"Duplicate profile id \"{profile.Id}\".");
                }

                result.Add((profile, path));
            }
            catch (ProfileFormatException ex)
            {
                Quarantine(path, ex.Message);
            }
        }

        return result;
    }

    private Profile Parse(string text)
    {
        try
        {
            if (JsonNode.Parse(text, null, DocumentOptions) is not JsonObject root)
            {
                throw new ProfileFormatException("The file does not contain a JSON object.");
            }

            var profile = _migrator.Migrate(root).Deserialize(ProfilesJsonContext.Default.Profile)
                ?? throw new ProfileFormatException("The file is empty.");
            Validate(profile);
            return profile;
        }
        catch (ProfileFormatException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ProfileFormatException(ex.Message, ex);
        }
    }

    private static void Validate(Profile profile)
    {
        if (!IdPattern().IsMatch(profile.Id ?? ""))
        {
            throw new ProfileFormatException("The profile id must be 1-64 characters: letters, digits, '.', '_' or '-'.");
        }

        if (string.IsNullOrWhiteSpace(profile.Name))
        {
            throw new ProfileFormatException("The profile needs a name.");
        }

        _ = ProfileMapper.ToMappingSettings(profile);
    }

    private void Quarantine(string path, string reason)
    {
        _log?.Invoke($"Invalid profile \"{path}\" moved to broken/: {reason}");
        try
        {
            Directory.CreateDirectory(BrokenFolder);
            var target = Path.Combine(BrokenFolder, Path.GetFileName(path));
            if (File.Exists(target))
            {
                target = Path.Combine(BrokenFolder, $"{Path.GetFileNameWithoutExtension(path)}.{DateTime.UtcNow:yyyyMMddHHmmssfff}.json");
            }

            File.Move(path, target);
            File.WriteAllText(target + ".reason.txt", reason);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log?.Invoke($"Could not quarantine \"{path}\": {ex.Message}");
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex IdPattern();
}
