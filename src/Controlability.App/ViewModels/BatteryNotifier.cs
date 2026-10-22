using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Abstractions;
using Controlability.Core.Extras;
using Controlability.Core.Models;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

/// <summary>
/// Shows a toast once when a controller drops below 20 % and once more below 10 %. The engine raises
/// <see cref="IEngine.Changed"/> when a battery reading changes; <see cref="BatteryAlertTracker"/> decides what counts.
/// </summary>
public sealed class BatteryNotifier : IDisposable
{
    private readonly IEngine _engine;
    private readonly INotifier _notifier;
    private readonly ThrottledAction _throttle;
    private readonly BatteryAlertTracker _tracker = new();
    private readonly HashSet<DeviceId> _known = [];
    private bool _disposed;

    public BatteryNotifier(IEngine engine, INotifier notifier, IUiThread ui, IClock clock)
    {
        _engine = engine;
        _notifier = notifier;
        _throttle = new ThrottledAction(ui, clock, DashboardViewModel.RefreshInterval, Check);
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

    internal void Check()
    {
        if (_disposed)
        {
            return;
        }

        var present = new HashSet<DeviceId>();
        foreach (var device in _engine.Devices.Where(d => d.Type != ControllerType.Keyboard))
        {
            present.Add(device.Id);
            if (!_engine.TryGetLiveState(device.Id, out var raw, out _))
            {
                continue;
            }

            var alert = _tracker.Update(device.Id, raw.Battery);
            if (alert is { } level)
            {
                _notifier.Notify(
                    Strings.NotifyTitle,
                    Strings.Format(
                        level == BatteryAlertLevel.Critical ? Strings.NotifyBatteryCritical : Strings.NotifyBatteryLow,
                        device.Name,
                        raw.Battery.Percent));
            }
        }

        foreach (var id in _known.Where(known => !present.Contains(known)).ToList())
        {
            _tracker.Forget(id);
            _known.Remove(id);
        }

        _known.UnionWith(present);
    }
}
