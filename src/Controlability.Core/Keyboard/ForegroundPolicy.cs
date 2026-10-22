namespace Controlability.Core.Keyboard;

/// <summary>
/// Mode B must not take over the whole PC: with target games configured, keys are swallowed and the keyboard pad
/// listens only while one of them is the foreground window. With no target configured nothing is ever swallowed (the
/// pad still listens, but the keys also reach whatever has the focus).
/// </summary>
public static class ForegroundPolicy
{
    /// <summary>Case-insensitive process name compare, with or without ".exe".</summary>
    public static bool IsTarget(IReadOnlyList<string> targets, string? foregroundProcess)
    {
        var current = Normalize(foregroundProcess);
        if (current is null)
        {
            return false;
        }

        foreach (var target in targets)
        {
            if (string.Equals(Normalize(target), current, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Should the keyboard pad produce input right now? Always, unless targets are set and none has the focus.</summary>
    public static bool ShouldListen(IReadOnlyList<string> targets, string? foregroundProcess)
        => targets.Count == 0 || IsTarget(targets, foregroundProcess);

    /// <summary>Should bound keys and mouse buttons be swallowed right now? Only while a target game has the focus.</summary>
    public static bool ShouldBlock(IReadOnlyList<string> targets, string? foregroundProcess)
        => targets.Count > 0 && IsTarget(targets, foregroundProcess);

    private static string? Normalize(string? name)
    {
        var text = name?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        return text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? text[..^4] : text;
    }
}
