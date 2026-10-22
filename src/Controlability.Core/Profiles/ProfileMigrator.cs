using System.Text.Json.Nodes;

namespace Controlability.Core.Profiles;

/// <summary>
/// Upgrades a profile's JSON from its stored <c>schemaVersion</c> to the current one, one version at a time.
/// A step registered under N turns a version N document into version N+1.
/// </summary>
public sealed class ProfileMigrator
{
    public const string VersionProperty = "schemaVersion";

    /// <summary>Only v1 exists, so there are no steps yet.</summary>
    public static readonly ProfileMigrator Default = new(Profile.CurrentSchemaVersion, new Dictionary<int, Action<JsonObject>>());

    private readonly IReadOnlyDictionary<int, Action<JsonObject>> _steps;

    public ProfileMigrator(int currentVersion, IReadOnlyDictionary<int, Action<JsonObject>> steps)
    {
        ArgumentNullException.ThrowIfNull(steps);
        CurrentVersion = currentVersion;
        _steps = steps;
    }

    public int CurrentVersion { get; }

    /// <summary>Migrates <paramref name="root"/> in place and returns it. Throws <see cref="ProfileFormatException"/> if it cannot.</summary>
    public JsonObject Migrate(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root[VersionProperty] is not JsonValue value || !value.TryGetValue<int>(out var version))
        {
            throw new ProfileFormatException($"Missing or invalid \"{VersionProperty}\".");
        }

        if (version > CurrentVersion)
        {
            throw new ProfileFormatException($"Schema version {version} is newer than this app supports ({CurrentVersion}).");
        }

        while (version < CurrentVersion)
        {
            if (!_steps.TryGetValue(version, out var step))
            {
                throw new ProfileFormatException($"No migration from schema version {version}.");
            }

            step(root);
            version++;
            root[VersionProperty] = version;
        }

        return root;
    }
}
