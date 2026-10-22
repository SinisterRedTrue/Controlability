using Controlability.Core.Abstractions;
using Controlability.Core.Models;

namespace Controlability.Core.Extras;

/// <summary>Everything a profile's Extras tab turns on. <see cref="None"/> changes nothing.</summary>
public sealed record ExtrasSettings(
    GyroSettings Gyro,
    TouchpadMouseSettings Touchpad,
    IReadOnlyDictionary<XboxButton, int> Turbo,
    IReadOnlyList<XboxButton> Toggle)
{
    public static readonly ExtrasSettings None = new(
        GyroSettings.Off,
        TouchpadMouseSettings.Off,
        new Dictionary<XboxButton, int>(),
        []);

    public bool IsEmpty => !Gyro.Enabled && !Touchpad.Enabled && Turbo.Count == 0 && Toggle.Count == 0;
}

/// <summary>
/// Runs a profile's extras for one device, on the input thread: gyro offset on the right stick, toggle and turbo on the
/// Xbox buttons, and the touchpad's mouse frame. Holds the per-device state (smoothing, toggle latches, turbo phase),
/// so a profile change builds a new instance.
/// </summary>
public sealed class ExtrasProcessor
{
    /// <summary>A stall (for example a debugger break) must not turn into one huge gyro step.</summary>
    private const float MaxDeltaSeconds = 0.1f;

    private readonly IClock _clock;
    private readonly GyroProcessor? _gyro;
    private readonly TouchpadMouseProcessor? _touchpad;
    private readonly TurboProcessor _turbo;
    private readonly ToggleProcessor _toggle;
    private long _lastTicks;
    private bool _hasLast;

    public ExtrasProcessor(ExtrasSettings settings, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
        _gyro = settings.Gyro.Enabled ? new GyroProcessor(settings.Gyro) : null;
        _touchpad = settings.Touchpad.Enabled ? new TouchpadMouseProcessor(settings.Touchpad) : null;
        _turbo = new TurboProcessor(settings.Turbo);
        _toggle = new ToggleProcessor(settings.Toggle);
    }

    public bool UsesGyro => _gyro is not null;

    public bool UsesTouchpad => _touchpad is not null;

    /// <summary>The input as the mapper should see it: with the touchpad as a mouse, its click is not also a button.</summary>
    public InputState Prepare(in InputState raw)
    {
        var state = raw;
        if (_touchpad is not null)
        {
            state.Buttons &= ~ButtonId.Touchpad;
        }

        return state;
    }

    /// <summary>The mouse movement and buttons the touchpad asks for. Zero when the touchpad extra is off.</summary>
    public MouseFrame ReadMouse(in InputState raw)
        => _touchpad?.Process(in raw, _clock.ToMs(_clock.Ticks)) ?? default;

    /// <summary>Adds the gyro to the right stick and applies toggle, then turbo, to the buttons.</summary>
    public void Apply(in InputState raw, ref XboxState mapped)
    {
        var now = _clock.Ticks;
        var delta = _hasLast ? Math.Min((float)(_clock.ToMs(now - _lastTicks) / 1000.0), MaxDeltaSeconds) : 0f;
        _lastTicks = now;
        _hasLast = true;

        if (_gyro is not null)
        {
            _gyro.Update(in raw, delta, out var x, out var y);
            mapped.RightX = GyroProcessor.Add(mapped.RightX, x);
            mapped.RightY = GyroProcessor.Add(mapped.RightY, y);
        }

        if (!_toggle.IsEmpty)
        {
            mapped.Buttons = _toggle.Process(mapped.Buttons);
        }

        if (!_turbo.IsEmpty)
        {
            mapped.Buttons = _turbo.Process(mapped.Buttons, _clock.ToMs(now));
        }
    }
}
