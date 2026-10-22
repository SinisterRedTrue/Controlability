using CommunityToolkit.Mvvm.Input;
using Controlability.App.Resources;
using Controlability.App.Services;
using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Core.Settings;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

public enum TrayState
{
    Normal,
    Paused,

    /// <summary>ViGEmBus is missing, so no virtual pad can exist. A missing HidHide only shows on the dashboard.</summary>
    Error,
}

/// <summary>What the tray icon and its menu show. The WPF side listens to <see cref="Updated"/> and redraws.</summary>
public sealed class TrayViewModel : IDisposable
{
    private readonly IEngine _engine;
    private readonly ThrottledAction _throttle;
    private bool _disposed;

    public TrayViewModel(IEngine engine, IShell shell, IUiThread ui, IClock clock)
    {
        _engine = engine;
        _throttle = new ThrottledAction(ui, clock, DashboardViewModel.RefreshInterval, Refresh);
        OpenCommand = new RelayCommand(shell.ShowMainWindow);
        ExitCommand = new RelayCommand(shell.Exit);
        TogglePauseCommand = new RelayCommand(() => _engine.Pause(!_engine.IsPaused));
        SetModeCommand = new RelayCommand<KeyboardMode>(mode => _engine.SetMode(mode));
        Refresh();
        _engine.Changed += _throttle.Request;
    }

    public event Action? Updated;

    public IRelayCommand OpenCommand { get; }

    public IRelayCommand ExitCommand { get; }

    public IRelayCommand TogglePauseCommand { get; }

    public IRelayCommand<KeyboardMode> SetModeCommand { get; }

    public TrayState State { get; private set; }

    public string Tooltip { get; private set; } = "";

    public bool IsPaused { get; private set; }

    public KeyboardMode Mode { get; private set; }

    /// <summary>"Player 1: name" or "Player 2: empty", for the four slots.</summary>
    public IReadOnlyList<string> SlotLines { get; private set; } = [];

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _engine.Changed -= _throttle.Request;
    }

    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        IsPaused = _engine.IsPaused;
        Mode = _engine.Mode;
        State = !_engine.DriverStatus.ViGEmAvailable ? TrayState.Error : IsPaused ? TrayState.Paused : TrayState.Normal;
        Tooltip = State switch
        {
            TrayState.Error => Strings.TrayTooltipError,
            TrayState.Paused => Strings.TrayTooltipPaused,
            _ => Strings.TrayTooltipNormal,
        };

        var batteries = BatteryLines();
        if (batteries.Count > 0 && State != TrayState.Error)
        {
            Tooltip += Environment.NewLine + string.Join("  ", batteries);
        }

        SlotLines =
        [
            .. _engine.Slots.Select(slot => slot.DeviceName is { Length: > 0 } name
                ? Strings.Format(Strings.TraySlotFilled, slot.Index + 1, name)
                : Strings.Format(Strings.TraySlotEmpty, slot.Index + 1)),
        ];
        Updated?.Invoke();
    }

    /// <summary>"P1 72%" for each slot whose controller reports a battery level.</summary>
    private List<string> BatteryLines()
    {
        var lines = new List<string>();
        foreach (var slot in _engine.Slots)
        {
            if (slot.Device is { } id
                && _engine.TryGetLiveState(id, out var raw, out _)
                && raw.Battery.State is BatteryState.Discharging or BatteryState.Charging or BatteryState.Full)
            {
                lines.Add(Strings.Format(Strings.TrayTooltipBattery, slot.Index + 1, raw.Battery.Percent));
            }
        }

        return lines;
    }
}
