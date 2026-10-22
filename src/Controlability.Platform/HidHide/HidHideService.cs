using Controlability.Core.Abstractions;
using Controlability.Core.Models;

namespace Controlability.Platform.HidHide;

/// <param name="InstancePathsHiddenByUs">Instance paths from the journal.</param>
public sealed record HidHideStatus(
    string ExePath,
    string Version,
    bool CloakOn,
    bool InverseOn,
    IReadOnlyList<string> Applications,
    IReadOnlyList<string> HiddenDevices,
    IReadOnlyList<HiddenEntry> Journal,
    IReadOnlyList<HidDevice> GamingDevices);

/// <summary>
/// <see cref="IDeviceHider"/> on top of <c>HidHideCLI.exe</c>. Only touches devices it hid itself: each hide is journaled
/// first, each unhide removes the entry. None of the public methods throws; problems go to the log.
/// </summary>
public sealed class HidHideService : IDeviceHider
{
    private readonly IHidHideCli? _cli;
    private readonly HiddenJournal _journal;
    private readonly string? _selfPath;
    private readonly Action<string>? _log;
    private readonly object _gate = new();
    private bool _whitelisted;
    private bool _cloakChecked;

    public HidHideService(IHidHideCli? cli, HiddenJournal journal, string? selfPath = null, Action<string>? log = null)
    {
        _cli = cli;
        _journal = journal;
        _selfPath = selfPath ?? Environment.ProcessPath;
        _log = log;
    }

    public bool IsAvailable => _cli is not null;

    /// <summary>The journal entries: devices that are hidden on our behalf right now.</summary>
    public IReadOnlyList<HiddenEntry> HiddenByUs => _journal.Load();

    public static HidHideService CreateDefault(string journalFile, Action<string>? log = null)
    {
        var exe = HidHideCli.Locate();
        return new HidHideService(exe is null ? null : new HidHideCli(exe), new HiddenJournal(journalFile), log: log);
    }

    public void WhitelistSelf()
    {
        lock (_gate)
        {
            if (_cli is null || _whitelisted || string.IsNullOrEmpty(_selfPath))
            {
                return;
            }

            try
            {
                if (!_cli.GetApps().Any(app => string.Equals(app, _selfPath, StringComparison.OrdinalIgnoreCase)))
                {
                    _cli.RegisterApp(_selfPath);
                }

                _whitelisted = true;
            }
            catch (HidHideCliException ex)
            {
                _log?.Invoke($"HidHide: could not whitelist {_selfPath}: {ex.Message}");
            }
        }
    }

    public void Hide(DeviceInfo device)
    {
        lock (_gate)
        {
            if (_cli is null)
            {
                return;
            }

            try
            {
                // Our own exe must be allowed to see the device before it disappears for everybody else.
                WhitelistSelf();
                if (!_whitelisted)
                {
                    _log?.Invoke($"HidHide: not hiding \"{device.Name}\" because this app could not be whitelisted.");
                    return;
                }

                var instancePath = ResolveInstancePath(_cli, device);
                if (instancePath is null)
                {
                    _log?.Invoke($"HidHide: could not find the HID instance path of \"{device.Name}\"; it stays visible.");
                    return;
                }

                if (_journal.Load().Any(e => string.Equals(e.InstancePath, instancePath, StringComparison.OrdinalIgnoreCase)))
                {
                    return;
                }

                if (_cli.GetHiddenDevices().Any(p => string.Equals(p, instancePath, StringComparison.OrdinalIgnoreCase)))
                {
                    _log?.Invoke($"HidHide: \"{device.Name}\" was already hidden before we started; leaving it that way.");
                    return;
                }

                _journal.Add(new HiddenEntry(device.Id.Value, instancePath));
                try
                {
                    _cli.Hide(instancePath);
                }
                catch (HidHideCliException)
                {
                    _journal.Remove(instancePath);
                    throw;
                }

                if (!_cloakChecked)
                {
                    if (!_cli.IsCloakOn())
                    {
                        _cli.SetCloak(true);
                    }

                    _cloakChecked = true;
                }
            }
            catch (Exception ex) when (ex is HidHideCliException or IOException or UnauthorizedAccessException)
            {
                _log?.Invoke($"HidHide: could not hide \"{device.Name}\": {ex.Message}");
            }
        }
    }

    public void Unhide(DeviceInfo device)
    {
        lock (_gate)
        {
            UnhideEntries(_journal.Load().Where(e => e.DeviceId == device.Id.Value).ToList());
        }
    }

    public void UnhideAll()
    {
        lock (_gate)
        {
            UnhideEntries(_journal.Load());
        }
    }

    /// <summary>Gives back what an earlier run left hidden (crash, kill). Returns how many devices were journaled.</summary>
    public int RecoverFromJournal()
    {
        lock (_gate)
        {
            var entries = _journal.Load();
            if (entries.Count == 0)
            {
                return 0;
            }

            _log?.Invoke($"HidHide: unhiding {entries.Count} device(s) left over from an earlier run.");
            UnhideEntries(entries);
            return entries.Count;
        }
    }

    /// <summary>Everything the <c>hidhide</c> debug command prints. Throws <see cref="HidHideCliException"/> when HidHide fails.</summary>
    public HidHideStatus? GetStatus()
    {
        if (_cli is null)
        {
            return null;
        }

        lock (_gate)
        {
            return new HidHideStatus(
                _cli.ExePath,
                _cli.Version(),
                _cli.IsCloakOn(),
                _cli.IsInverseOn(),
                _cli.GetApps(),
                _cli.GetHiddenDevices(),
                _journal.Load(),
                _cli.GetGamingDevices());
        }
    }

    private void UnhideEntries(IReadOnlyList<HiddenEntry> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        if (_cli is null)
        {
            _log?.Invoke($"HidHide is not installed; {entries.Count} journaled device(s) cannot be unhidden.");
            return;
        }

        foreach (var entry in entries)
        {
            try
            {
                _cli.Unhide(entry.InstancePath);
                _journal.Remove(entry.InstancePath);
            }
            catch (Exception ex) when (ex is HidHideCliException or IOException or UnauthorizedAccessException)
            {
                // Kept in the journal: the next start tries again.
                _log?.Invoke($"HidHide: could not unhide {entry.InstancePath}: {ex.Message}");
            }
        }
    }

    private static string? ResolveInstancePath(IHidHideCli cli, DeviceInfo device)
    {
        var match = DevicePathConverter.Resolve(device, cli.GetGamingDevices());
        return match?.InstancePath ?? DevicePathConverter.ToInstancePath(device.HidPath);
    }
}
