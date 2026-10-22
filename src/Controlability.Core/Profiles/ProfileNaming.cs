using System.Text;

namespace Controlability.Core.Profiles;

/// <summary>Unique names and ids for profiles the user creates, duplicates or imports.</summary>
public static class ProfileNaming
{
    public const int MaxIdLength = 64;

    /// <summary>
    /// Returns <paramref name="name"/> trimmed, or with " (2)", " (3)" ... added until no existing name
    /// (case-insensitive) is the same.
    /// </summary>
    public static string UniqueName(string name, IEnumerable<string> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var trimmed = (name ?? "").Trim();
        var taken = existing.ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!taken.Contains(trimmed))
        {
            return trimmed;
        }

        for (var n = 2; ; n++)
        {
            var candidate = $"{trimmed} ({n})";
            if (!taken.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    /// <summary>A file-safe id (letters, digits, '.', '_', '-') made from the name that no existing id uses.</summary>
    public static string UniqueId(string name, IEnumerable<string> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var slug = new StringBuilder();
        foreach (var c in (name ?? "").Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9' or '.' or '_')
            {
                slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                slug.Append('-');
            }
        }

        var baseId = slug.ToString().Trim('-');
        if (baseId.Length == 0)
        {
            baseId = "profile";
        }

        baseId = baseId[..Math.Min(baseId.Length, MaxIdLength - 5)];
        var taken = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var id = baseId;
        for (var n = 2; taken.Contains(id); n++)
        {
            id = $"{baseId}-{n}";
        }

        return id;
    }
}
