using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Controlability.App.Services;

/// <summary>Calls <paramref name="onResume"/> when the PC wakes from sleep. The event arrives on a system thread.</summary>
public sealed class PowerResumeHandler : IDisposable
{
    private readonly ILogger _logger;
    private readonly Action _onResume;

    public PowerResumeHandler(ILogger logger, Action onResume)
    {
        _logger = logger;
        _onResume = onResume;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
    }

    public void Dispose() => SystemEvents.PowerModeChanged -= OnPowerModeChanged;

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e) => Handle(e.Mode);

    internal void Handle(PowerModes mode)
    {
        if (mode != PowerModes.Resume)
        {
            return;
        }

        _logger.LogInformation("Resumed from sleep: restarting the controller input.");
        try
        {
            _onResume();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restarting the input after sleep failed.");
        }
    }
}
