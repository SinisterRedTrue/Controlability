using System.IO;
using Controlability.Core.Profiles;

namespace Controlability.App.Services;

public sealed record ProfileChoice(string Id, string Name);

/// <summary>The profiles the user can pick for a slot.</summary>
public interface IProfileCatalog
{
    IReadOnlyList<ProfileChoice> GetProfiles();
}

/// <summary>Reads and writes single profiles, and reaches the shipped copy a "reset" restores.</summary>
public interface IProfileEditor
{
    Profile? Get(string id);

    void Save(Profile profile);

    /// <summary>The profile as it ships with the app, or null if there is none with that id.</summary>
    Profile? GetShipped(string id);
}

public sealed class ProfileStoreCatalog(ProfileStore store, string shippedFolder) : IProfileCatalog, IProfileEditor
{
    public IReadOnlyList<ProfileChoice> GetProfiles() =>
        [.. store.LoadAll().Select(profile => new ProfileChoice(profile.Id, profile.Name)).OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase)];

    public Profile? Get(string id) => store.Get(id);

    public void Save(Profile profile) => store.Save(profile);

    public Profile? GetShipped(string id) => Directory.Exists(shippedFolder) ? new ProfileStore(shippedFolder).Get(id) : null;
}
