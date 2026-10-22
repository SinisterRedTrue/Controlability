namespace Controlability.Core.Abstractions;

/// <summary>Identifies a virtual pad owned by an <see cref="IOutputSink"/>.</summary>
public readonly record struct SlotHandle(int SlotIndex)
{
    public static readonly SlotHandle Invalid = new(-1);

    public bool IsValid => SlotIndex >= 0;
}
