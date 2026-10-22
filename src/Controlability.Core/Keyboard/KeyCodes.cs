using System.Collections.Frozen;
using System.Globalization;

namespace Controlability.Core.Keyboard;

/// <summary>
/// Windows virtual-key codes (plain numbers, so Core stays free of Windows references) and the key names used in
/// profiles. Left/right modifier variants are treated as one key: Raw Input only reports the generic code while the
/// low-level hook reports the specific one, and both must meet in the same binding.
/// </summary>
public static class KeyCodes
{
    public const int Cancel = 0x03;
    public const int Backspace = 0x08;
    public const int Tab = 0x09;
    public const int Enter = 0x0D;
    public const int Shift = 0x10;
    public const int Control = 0x11;
    public const int Alt = 0x12;
    public const int Pause = 0x13;
    public const int LeftShift = 0xA0;
    public const int RightShift = 0xA1;
    public const int LeftControl = 0xA2;
    public const int RightControl = 0xA3;
    public const int LeftAlt = 0xA4;
    public const int RightAlt = 0xA5;

    private static readonly FrozenDictionary<string, int> ByName = BuildByName();
    private static readonly FrozenDictionary<int, string> ByCode = BuildByCode();

    /// <summary>Maps left/right Shift, Ctrl and Alt to their generic code; every other code is returned unchanged.</summary>
    public static int Normalize(int virtualKey) => virtualKey switch
    {
        LeftShift or RightShift => Shift,
        LeftControl or RightControl => Control,
        LeftAlt or RightAlt => Alt,
        _ => virtualKey,
    };

    /// <summary>Parses a profile key name (case-insensitive: "W", "Space", "Shift", "NumPad4", ...) to a normalized code.</summary>
    public static bool TryParse(string name, out int virtualKey)
    {
        if (name is not null && ByName.TryGetValue(name.Trim(), out virtualKey))
        {
            return true;
        }

        virtualKey = 0;
        return false;
    }

    /// <summary>The canonical profile name of a key, or "VK_0x.." for keys without a name.</summary>
    public static string NameOf(int virtualKey)
        => ByCode.TryGetValue(Normalize(virtualKey), out var name) ? name : $"VK_0x{virtualKey:X2}";

    private static FrozenDictionary<string, int> BuildByName()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, name) in Canonical())
        {
            map[name] = code;
        }

        (string Alias, int Code)[] aliases =
        [
            ("Esc", 0x1B), ("Return", Enter), ("Spacebar", 0x20), ("Del", 0x2E), ("Ins", 0x2D),
            ("PgUp", 0x21), ("PgDn", 0x22), ("Break", Cancel),
            ("Control", Control), ("LeftControl", Control), ("RightControl", Control),
            ("LeftShift", Shift), ("RightShift", Shift), ("LShift", Shift), ("RShift", Shift),
            ("LeftCtrl", Control), ("RightCtrl", Control), ("LCtrl", Control), ("RCtrl", Control),
            ("LeftAlt", Alt), ("RightAlt", Alt), ("LAlt", Alt), ("RAlt", Alt),
        ];
        foreach (var (alias, code) in aliases)
        {
            map[alias] = code;
        }

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static FrozenDictionary<int, string> BuildByCode()
        => Canonical().ToFrozenDictionary(pair => pair.Code, pair => pair.Name);

    private static IEnumerable<(int Code, string Name)> Canonical()
    {
        for (var c = 'A'; c <= 'Z'; c++)
        {
            yield return (c, c.ToString());
        }

        for (var d = 0; d <= 9; d++)
        {
            yield return (0x30 + d, d.ToString(CultureInfo.InvariantCulture));
            yield return (0x60 + d, $"NumPad{d}");
        }

        for (var f = 1; f <= 24; f++)
        {
            yield return (0x6F + f, $"F{f}");
        }

        yield return (Backspace, "Backspace");
        yield return (Tab, "Tab");
        yield return (Enter, "Enter");
        yield return (Shift, "Shift");
        yield return (Control, "Ctrl");
        yield return (Alt, "Alt");
        yield return (Pause, "Pause");
        yield return (Cancel, "Cancel");
        yield return (0x14, "CapsLock");
        yield return (0x1B, "Escape");
        yield return (0x20, "Space");
        yield return (0x21, "PageUp");
        yield return (0x22, "PageDown");
        yield return (0x23, "End");
        yield return (0x24, "Home");
        yield return (0x25, "Left");
        yield return (0x26, "Up");
        yield return (0x27, "Right");
        yield return (0x28, "Down");
        yield return (0x2D, "Insert");
        yield return (0x2E, "Delete");
        yield return (0xBA, "Semicolon");
        yield return (0xBB, "Equals");
        yield return (0xBC, "Comma");
        yield return (0xBD, "Minus");
        yield return (0xBE, "Period");
        yield return (0xBF, "Slash");
        yield return (0xC0, "Grave");
        yield return (0xDB, "LeftBracket");
        yield return (0xDC, "Backslash");
        yield return (0xDD, "RightBracket");
        yield return (0xDE, "Quote");
    }
}
