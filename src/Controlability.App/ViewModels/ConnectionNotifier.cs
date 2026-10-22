using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

/// <summary>Raises "X connected as Player 2" / "X disconnected" notifications by comparing engine snapshots.</summary>
public sealed class ConnectionNotifier : IDisposable
{
    private readonly IEngine _engine;
    private readonly INotifier _notifier;
    private readonly ThrottledAction _throttle;
    private readonly Dictionary<DeviceId, string> _known = [];
    private bool _disposed;

    public ConnectionNotifier(IEngine engine, INotifier notifier, IUiThread ui, IClock clock)
    {
        _engine = engine;
        _notifier = notifier;
        _throttle = new ThrottledAction(ui, clock, DashboardViewModel.RefreshInterval, Check);
        foreach (var device in engine.Devices.Where(IsController))
        {
            _known[device.Id] = device.Name;
        }

        _engine.Changed += _throttle.Request;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _engine.Changed -= _throttle.Request;
    }

    /// <summary>The Mode B keyboard pad comes and goes with the mode switch; the user did that, so no toast.</summary>
    private static bool IsController(DeviceInfo device) => device.Type != ControllerType.Keyboard;

    internal void Check()
    {
        if (_disposed)
        {
            return;
        }

        var devices = _engine.Devices.Where(IsController).ToList();
        var slots = _engine.Slots;

        foreach (var device in devices.Where(d => !_known.ContainsKey(d.Id)))
        {
            _known[device.Id] = device.Name;
            var slot = slots.FirstOrDefault(s => s.Device == device.Id);
            _notifier.Notify(
                Strings.NotifyTitle,
                slot is null
                    ? Strings.Format(Strings.NotifyConnectedUnassigned, device.Name)
                    : Strings.Format(Strings.NotifyConnected, device.Name, slot.Index + 1));
        }

        foreach (var (id, name) in _known.Where(pair => devices.All(d => d.Id != pair.Key)).ToList())
        {
            _known.Remove(id);
            _notifier.Notify(Strings.NotifyTitle, Strings.Format(Strings.NotifyDisconnected, name));
        }
    }
}
