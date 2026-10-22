using Controlability.Core.Models;

namespace Controlability.Core.Extras;

/// <summary>
/// Turbo: while a configured Xbox button is held, it is pressed and released <c>Hz</c> times a second (50 % duty).
/// The first press starts "on", so a quick tap still reaches the game.
/// </summary>
public sealed class TurboProcessor
{
    public const int MinHz = 1;
    public const int MaxHz = 30;
    public const int DefaultHz = 10;

    private readonly (XboxButton Button, double PeriodMs)[] _turbo;
    private readonly double[] _heldSinceMs;

    public TurboProcessor(IReadOnlyDictionary<XboxButton, int> hz)
    {
        ArgumentNullException.ThrowIfNull(hz);
        var list = new List<(XboxButton, double)>();
        foreach (var (button, rate) in hz)
        {
            if (rate is < MinHz or > MaxHz)
            {
                throw new ArgumentException($"Turbo rate for {button} must be {MinHz} to {MaxHz} Hz.");
            }

            list.Add((button, 1000.0 / rate));
        }

        _turbo = [.. list];
        _heldSinceMs = new double[_turbo.Length];
        Array.Fill(_heldSinceMs, double.NaN);
    }

    public bool IsEmpty => _turbo.Length == 0;

    public ushort Process(ushort buttons, double nowMs)
    {
        var result = buttons;
        for (var i = 0; i < _turbo.Length; i++)
        {
            var (button, period) = _turbo[i];
            if ((buttons & (ushort)button) == 0)
            {
                _heldSinceMs[i] = double.NaN;
                continue;
            }

            if (double.IsNaN(_heldSinceMs[i]))
            {
                _heldSinceMs[i] = nowMs;
            }

            var phase = (nowMs - _heldSinceMs[i]) % period;
            if (phase >= period / 2)
            {
                result &= (ushort)~(ushort)button;
            }
        }

        return result;
    }
}

/// <summary>Toggle: pressing a configured Xbox button once holds it down, pressing it again lets go.</summary>
public sealed class ToggleProcessor
{
    private readonly XboxButton[] _toggles;
    private readonly bool[] _wasDown;
    private readonly bool[] _latched;

    public ToggleProcessor(IReadOnlyCollection<XboxButton> toggles)
    {
        ArgumentNullException.ThrowIfNull(toggles);
        _toggles = [.. toggles];
        _wasDown = new bool[_toggles.Length];
        _latched = new bool[_toggles.Length];
    }

    public bool IsEmpty => _toggles.Length == 0;

    public ushort Process(ushort buttons)
    {
        var result = buttons;
        for (var i = 0; i < _toggles.Length; i++)
        {
            var bit = (ushort)_toggles[i];
            var down = (buttons & bit) != 0;
            if (down && !_wasDown[i])
            {
                _latched[i] = !_latched[i];
            }

            _wasDown[i] = down;
            result = _latched[i] ? (ushort)(result | bit) : (ushort)(result & ~bit);
        }

        return result;
    }
}
