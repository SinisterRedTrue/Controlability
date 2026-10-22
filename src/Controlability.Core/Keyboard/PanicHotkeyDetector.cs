namespace Controlability.Core.Keyboard;

/// <summary>
/// Detects the hard-coded panic hotkey <b>Ctrl+Alt+Pause</b> from a stream of key events. Tracks Ctrl and Alt itself, so
/// it works even when those keys are bound (and swallowed) in the profile. Ctrl+Pause is reported by many keyboards as
/// Ctrl+Break, so <see cref="KeyCodes.Cancel"/> counts as Pause. Not thread-safe: it belongs to the hook thread.
/// </summary>
public sealed class PanicHotkeyDetector
{
    private bool _leftCtrl, _rightCtrl, _leftAlt, _rightAlt;

    /// <returns>True when this event is the Pause key going down while Ctrl and Alt are held.</returns>
    public bool OnKey(int virtualKey, bool isDown)
    {
        switch (virtualKey)
        {
            case KeyCodes.Control or KeyCodes.LeftControl:
                _leftCtrl = isDown;
                return false;
            case KeyCodes.RightControl:
                _rightCtrl = isDown;
                return false;
            case KeyCodes.Alt or KeyCodes.LeftAlt:
                _leftAlt = isDown;
                return false;
            case KeyCodes.RightAlt:
                _rightAlt = isDown;
                return false;
            case KeyCodes.Pause or KeyCodes.Cancel:
                return isDown && (_leftCtrl || _rightCtrl) && (_leftAlt || _rightAlt);
            default:
                return false;
        }
    }

    public void Reset() => _leftCtrl = _rightCtrl = _leftAlt = _rightAlt = false;
}
