using System.Text.Json;

namespace Controlability.Core.Profiles;

/// <summary>JSON text and deep copies of profiles (<c>with</c> only copies the top level).</summary>
public static class ProfileJson
{
    public static string Serialize(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return JsonSerializer.Serialize(profile, ProfilesJsonContext.Default.Profile);
    }

    /// <summary>A copy that shares no list or dictionary with <paramref name="profile"/>.</summary>
    public static Profile Clone(Profile profile)
        => JsonSerializer.Deserialize(Serialize(profile), ProfilesJsonContext.Default.Profile)
            ?? throw new InvalidOperationException("A profile always serializes to an object.");
}
