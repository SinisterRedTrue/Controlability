using Controlability.Core.Models;

namespace Controlability.Core.Extras;

public enum BatteryAlertLevel
{
    /// <summary>Below 20 %.</summary>
    Low,

    /// <summary>Below 10 %.</summary>
    Critical,
}

/// <summary>
/// Decides when a battery warrants a toast: once when a device drops below 20 %, once more below 10 %. The warnings
/// come back only after the battery went up again (charging, wired, or at least 20 %), so a reading that wobbles
/// around a threshold does not repeat itself.
/// </summary>
public sealed class BatteryAlertTracker
{
    public const int LowPercent = 20;
    public const int CriticalPercent = 10;

    private readonly Dictionary<DeviceId, BatteryAlertLevel> _reported = [];

    /// <summary>Feeds a new reading; returns the alert to show, if this reading crossed a threshold.</summary>
    public BatteryAlertLevel? Update(DeviceId device, BatteryInfo battery)
    {
        if (battery.State == BatteryState.Unknown)
        {
            return null;
        }

        if (battery.State != BatteryState.Discharging || battery.Percent >= LowPercent)
        {
            _reported.Remove(device);
            return null;
        }

        var hasReported = _reported.TryGetValue(device, out var reported);
        if (battery.Percent < CriticalPercent)
        {
            if (!hasReported || reported != BatteryAlertLevel.Critical)
            {
                _reported[device] = BatteryAlertLevel.Critical;
                return BatteryAlertLevel.Critical;
            }
        }
        else if (!hasReported)
        {
            _reported[device] = BatteryAlertLevel.Low;
            return BatteryAlertLevel.Low;
        }

        return null;
    }

    /// <summary>Forget a device that is gone, so it warns again when it comes back low.</summary>
    public void Forget(DeviceId device) => _reported.Remove(device);
}
