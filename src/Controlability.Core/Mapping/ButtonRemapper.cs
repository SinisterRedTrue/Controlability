using System.Numerics;
using Controlability.Core.Models;

namespace Controlability.Core.Mapping;

/// <summary>
/// Immutable table from source button to Xbox button(s). Several sources may map to one target;
/// a source without an entry is dropped.
/// </summary>
public sealed class ButtonRemapper
{
    public static readonly ButtonRemapper Empty = new(new XboxButton[32]);

    private readonly XboxButton[] _table;

    private ButtonRemapper(XboxButton[] table)
    {
        _table = table;
    }

    public static ButtonRemapper Create(IEnumerable<KeyValuePair<ButtonId, XboxButton>> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var table = new XboxButton[32];
        foreach (var (source, target) in entries)
        {
            table[IndexOf(source)] = target;
        }

        return new ButtonRemapper(table);
    }

    public XboxButton this[ButtonId source] => _table[IndexOf(source)];

    /// <summary>Returns a copy with <paramref name="source"/> mapped to <paramref name="target"/>.</summary>
    public ButtonRemapper With(ButtonId source, XboxButton target)
    {
        var table = (XboxButton[])_table.Clone();
        table[IndexOf(source)] = target;
        return new ButtonRemapper(table);
    }

    /// <summary>Converts the pressed source buttons to Xbox buttons. Does not allocate.</summary>
    public XboxButton Remap(ButtonId pressed)
    {
        var bits = (uint)pressed;
        var result = XboxButton.None;
        while (bits != 0)
        {
            result |= _table[BitOperations.TrailingZeroCount(bits)];
            bits &= bits - 1;
        }

        return result;
    }

    private static int IndexOf(ButtonId source)
    {
        var bits = (uint)source;
        if (BitOperations.PopCount(bits) != 1)
        {
            throw new ArgumentException("Exactly one source button is required.", nameof(source));
        }

        return BitOperations.TrailingZeroCount(bits);
    }
}
