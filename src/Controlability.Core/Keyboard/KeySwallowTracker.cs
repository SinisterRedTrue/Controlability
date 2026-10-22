namespace Controlability.Core.Keyboard;

/// <summary>
/// The blocker's decision, free of Windows: should the low-level hook swallow this key or mouse button event?
/// <list type="bullet">
/// <item>Only keys bound in the active profile are swallowed.</item>
/// <item>Injected events (other tools, on-screen keyboards) always pass.</item>
/// <item>A key-up is swallowed only if its key-down was, so a key held from before blocking began is not left stuck in
/// the game.</item>
/// </list>
/// Not thread-safe: it belongs to the hook thread.
/// </summary>
public sealed class KeySwallowTracker
{
    private readonly bool[] _keyDownSwallowed = new bool[256];
    private MouseButtons _mouseDownSwallowed;

    /// <summary>The pure rule without key-up bookkeeping: is this key bound and the event not injected?</summary>
    public static bool ShouldSwallowKey(KeyBindings bindings, int virtualKey, bool injected)
        => !injected && bindings.IsBound(virtualKey);

    public bool DecideKey(KeyBindings bindings, int virtualKey, bool isDown, bool injected)
    {
        var key = KeyCodes.Normalize(virtualKey) & 0xFF;
        if (isDown)
        {
            var swallow = ShouldSwallowKey(bindings, virtualKey, injected);
            _keyDownSwallowed[key] = swallow;
            return swallow;
        }

        var wasSwallowed = _keyDownSwallowed[key];
        _keyDownSwallowed[key] = false;
        return wasSwallowed && !injected;
    }

    public bool DecideMouse(KeyBindings bindings, MouseButtons button, bool isDown, bool injected)
    {
        if (isDown)
        {
            var swallow = !injected && bindings.IsBound(button);
            _mouseDownSwallowed = swallow ? _mouseDownSwallowed | button : _mouseDownSwallowed & ~button;
            return swallow;
        }

        var wasSwallowed = (_mouseDownSwallowed & button) != MouseButtons.None;
        _mouseDownSwallowed &= ~button;
        return wasSwallowed && !injected;
    }

    /// <summary>Forgets what was swallowed (blocking switched off).</summary>
    public void Reset()
    {
        Array.Clear(_keyDownSwallowed);
        _mouseDownSwallowed = MouseButtons.None;
    }
}
