using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Abstractions;
using Controlability.Core.Profiles;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

public sealed record ProfileListItem(string Id, string Name, bool IsShipped)
{
    public override string ToString() => Name;
}

/// <summary>The profiles page: the list on the left, the editor of the selected profile on the right.</summary>
public sealed partial class ProfilesViewModel : ObservableObject, IDisposable
{
    private readonly IEngine _engine;
    private readonly IProfileEditor _profiles;
    private readonly IRunningApps _apps;
    private readonly IDialogs _dialogs;
    private readonly IUiThread _ui;
    private readonly IClock _clock;
    private readonly ThrottledAction _throttle;
    private bool _switching;
    private bool _disposed;

    [ObservableProperty]
    private ProfileListItem? _selectedProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditor))]
    [NotifyPropertyChangedFor(nameof(HasNoEditor))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    private ProfileEditorViewModel? _editor;

    [ObservableProperty]
    private string _statusText = "";

    public ProfilesViewModel(
        IEngine engine,
        IProfileEditor profiles,
        IRunningApps apps,
        IDialogs dialogs,
        IUiThread ui,
        IClock clock)
    {
        _engine = engine;
        _profiles = profiles;
        _apps = apps;
        _dialogs = dialogs;
        _ui = ui;
        _clock = clock;
        _throttle = new ThrottledAction(ui, clock, DashboardViewModel.RefreshInterval, RefreshDevices);
        Reload();
        _engine.Changed += _throttle.Request;
    }

    public ObservableCollection<ProfileListItem> Profiles { get; } = [];

    public bool HasEditor => Editor is not null;

    public bool HasNoEditor => Editor is null;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _engine.Changed -= _throttle.Request;
        Editor?.Dispose();
    }

    /// <summary>Re-reads the list from disk and keeps the selection. UI thread only.</summary>
    public void Reload()
    {
        RebuildList(SelectedProfile?.Id);
        if (Editor is not { IsDirty: true })
        {
            LoadEditor(SelectedProfile);
        }
    }

    partial void OnSelectedProfileChanged(ProfileListItem? oldValue, ProfileListItem? newValue)
    {
        if (_switching)
        {
            return;
        }

        if (Editor is { IsDirty: true } && !_dialogs.Confirm(Strings.DiscardTitle, Strings.DiscardMessage))
        {
            // Put the old selection back once the list has finished handling this change.
            _ui.Post(() =>
            {
                _switching = true;
                SelectedProfile = oldValue;
                _switching = false;
            });
            return;
        }

        LoadEditor(newValue);
    }

    [RelayCommand(CanExecute = nameof(HasEditor))]
    private void Duplicate()
    {
        if (Editor is not { } editor || _profiles.Get(editor.Id) is not { } source)
        {
            return;
        }

        var all = _profiles.GetAll();
        var copy = ProfileJson.Clone(source);
        copy.Name = ProfileNaming.UniqueName(Strings.Format(Strings.CopyName, source.Name), all.Select(p => p.Name));
        copy.Id = ProfileNaming.UniqueId(copy.Name, all.Select(p => p.Id));
        AddAndSelect(copy, Strings.StatusDuplicated);
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (Editor is not { IsShipped: false } editor
            || !_dialogs.Confirm(Strings.DeleteTitle, Strings.Format(Strings.DeleteMessage, editor.Name)))
        {
            return;
        }

        var name = editor.Name;
        _profiles.Delete(editor.Id);
        RebuildList(null);
        LoadEditor(SelectedProfile);
        StatusText = Strings.Format(Strings.StatusDeleted, name);
    }

    [RelayCommand]
    private void Import()
    {
        if (_dialogs.PickOpenFile(Strings.ImportTitle, Strings.FileFilterProfiles) is not { } path)
        {
            return;
        }

        if (!_profiles.TryImport(path, out var profile, out var error))
        {
            StatusText = Strings.Format(Strings.StatusImportFailed, error);
            return;
        }

        var all = _profiles.GetAll();
        profile.Name = ProfileNaming.UniqueName(profile.Name, all.Select(p => p.Name));
        profile.Id = ProfileNaming.UniqueId(profile.Id.Length > 0 ? profile.Id : profile.Name, all.Select(p => p.Id));
        AddAndSelect(profile, Strings.StatusImported);
    }

    [RelayCommand(CanExecute = nameof(HasEditor))]
    private void Export()
    {
        if (Editor is not { } editor)
        {
            return;
        }

        var profile = editor.SavedProfile;
        if (_dialogs.PickSaveFile(Strings.ExportTitle, Strings.FileFilterProfiles, profile.Id + ".json") is { } path)
        {
            _profiles.Export(profile, path);
            StatusText = Strings.Format(Strings.StatusExported, path);
        }
    }

    private bool CanDelete() => Editor is { IsShipped: false };

    private void AddAndSelect(Profile profile, string statusFormat)
    {
        try
        {
            _profiles.Save(profile);
        }
        catch (ProfileFormatException ex)
        {
            StatusText = Strings.Format(Strings.StatusImportFailed, ex.Message);
            return;
        }

        RebuildList(profile.Id);
        LoadEditor(SelectedProfile);
        StatusText = Strings.Format(statusFormat, profile.Name);
    }

    private void RebuildList(string? selectId)
    {
        _switching = true;
        try
        {
            Profiles.Clear();
            foreach (var profile in _profiles.GetAll().OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                Profiles.Add(new ProfileListItem(profile.Id, profile.Name, profile.Id.StartsWith("default-", StringComparison.Ordinal)));
            }

            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == selectId) ?? Profiles.FirstOrDefault();
        }
        finally
        {
            _switching = false;
        }
    }

    private void LoadEditor(ProfileListItem? item)
    {
        if (Editor is { } old)
        {
            old.Saved -= OnEditorSaved;
            old.Dispose();
        }

        if (item is null || _profiles.Get(item.Id) is not { } profile)
        {
            Editor = null;
            return;
        }

        var editor = new ProfileEditorViewModel(profile, _profiles, _engine, _apps, _dialogs, _ui, _clock);
        editor.Saved += OnEditorSaved;
        Editor = editor;
    }

    private void OnEditorSaved(ProfileEditorViewModel editor) => RebuildList(editor.Id);

    private void RefreshDevices()
    {
        if (!_disposed)
        {
            Editor?.RefreshDevice();
        }
    }
}
