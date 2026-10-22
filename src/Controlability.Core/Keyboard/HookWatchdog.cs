namespace Controlability.Core.Keyboard;

/// <summary>
/// Windows silently drops a low-level hook whose callback is too slow. Raw Input keeps working in that case, so the
/// two are compared: if Raw Input has seen a key press that the hook did not report for the threshold time, the hook
/// is dead.
/// </summary>
public static class HookWatchdog
{
    public const long DefaultThresholdMs = 2000;

    /// <param name="blocking">Whether blocking is on; nothing needs repairing otherwise.</param>
    /// <param name="lastRawKeyMs">When Raw Input last saw a key press (0 = never).</param>
    /// <param name="lastHookKeyMs">When the hook last saw a key event, or when it was installed (same clock).</param>
    public static bool ShouldReinstall(bool blocking, long lastRawKeyMs, long lastHookKeyMs, long thresholdMs = DefaultThresholdMs)
        => blocking && lastRawKeyMs > 0 && lastRawKeyMs - lastHookKeyMs >= thresholdMs;
}
