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

    IReadOnlyList<Profile> GetAll();

    void Delete(string id);

    /// <summary>Reads a profile file the user picked. Returns false with a readable reason if it is not a valid profile.</summary>
    bool TryImport(string path, out Profile profile, out string error);

    void Export(Profile profile, string path);
}

public sealed class ProfileStoreCatalog(ProfileStore store, string shippedFolder) : IProfileCatalog, IProfileEditor
{
    public IReadOnlyList<ProfileChoice> GetProfiles() =>
        [.. store.LoadAll().Select(profile => new ProfileChoice(profile.Id, profile.Name)).OrderBy(choice => choice.Name, StringComparer.CurrentCultureIgnoreCase)];

    public Profile? Get(string id) => store.Get(id);

    public void Save(Profile profile) => store.Save(profile);

    public Profile? GetShipped(string id) => Directory.Exists(shippedFolder) ? new ProfileStore(shippedFolder).Get(id) : null;

    public IReadOnlyList<Profile> GetAll() => store.LoadAll();

    public void Delete(string id) => store.Delete(id);

    public bool TryImport(string path, out Profile profile, out string error)
    {
        try
        {
            return store.TryParse(File.ReadAllText(path), out profile, out error);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            profile = Profile.Generic;
            error = ex.Message;
            return false;
        }
    }

    public void Export(Profile profile, string path) => File.WriteAllText(path, ProfileJson.Serialize(profile));
}
