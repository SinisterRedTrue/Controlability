using System.Collections.Frozen;
using Controlability.Core.Profiles;

namespace Controlability.Core.Keyboard;

/// <summary>
/// The compiled Mode B bindings of a profile: key codes (normalized) and mouse buttons to Xbox controls. Immutable and
/// built for lookups on the low-level hook thread (frozen collections, no allocation).
/// </summary>
public sealed class KeyBindings
{
    public static readonly KeyBindings Empty = new(
        FrozenDictionary<int, KeyboardControl>.Empty, FrozenDictionary<MouseButtons, KeyboardControl>.Empty, 1f, 60);

    private readonly FrozenDictionary<int, KeyboardControl> _keys;
    private readonly FrozenDictionary<MouseButtons, KeyboardControl> _mouse;

    private KeyBindings(
        FrozenDictionary<int, KeyboardControl> keys,
        FrozenDictionary<MouseButtons, KeyboardControl> mouse,
        float mouseSensitivity,
        int decayMs)
    {
        _keys = keys;
        _mouse = mouse;
        MouseSensitivity = mouseSensitivity;
        DecayMs = decayMs;
        BoundMouseButtons = mouse.Keys.Aggregate(MouseButtons.None, (all, button) => all | button);
    }

    public float MouseSensitivity { get; }

    public int DecayMs { get; }

    /// <summary>Every mouse button that has a binding; the mouse hook swallows exactly these.</summary>
    public MouseButtons BoundMouseButtons { get; }

    public int KeyCount => _keys.Count;

    /// <param name="warn">Receives one line per entry that was skipped (unknown key or control name).</param>
    public static KeyBindings From(ProfileKeyboard keyboard, Action<string>? warn = null)
    {
        ArgumentNullException.ThrowIfNull(keyboard);

        var keys = new Dictionary<int, KeyboardControl>();
        var mouse = new Dictionary<MouseButtons, KeyboardControl>();
        foreach (var (keyName, controlName) in keyboard.Keys)
        {
            if (!KeyboardControls.TryParse(controlName, out var control))
            {
                warn?.Invoke($"Keyboard binding \"{keyName}\" skipped: unknown control \"{controlName}\".");
            }
            else if (MouseButtonNames.TryParse(keyName, out var button))
            {
                mouse[button] = control;
            }
            else if (KeyCodes.TryParse(keyName, out var code))
            {
                keys[code] = control;
            }
            else
            {
                warn?.Invoke($"Keyboard binding \"{keyName}\" skipped: unknown key.");
            }
        }

        return new KeyBindings(
            keys.ToFrozenDictionary(),
            mouse.ToFrozenDictionary(),
            keyboard.MouseSensitivity,
            Math.Max(0, keyboard.DecayMs));
    }

    /// <summary>The control of a key (any left/right variant), or null if it is not bound.</summary>
    public KeyboardControl? ControlOf(int virtualKey)
        => _keys.TryGetValue(KeyCodes.Normalize(virtualKey), out var control) ? control : null;

    public KeyboardControl? ControlOf(MouseButtons button)
        => _mouse.TryGetValue(button, out var control) ? control : null;

    public bool IsBound(int virtualKey) => _keys.ContainsKey(KeyCodes.Normalize(virtualKey));

    public bool IsBound(MouseButtons button) => (BoundMouseButtons & button) != MouseButtons.None;
}
