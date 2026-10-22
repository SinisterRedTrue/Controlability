namespace Controlability.Core.Models;

/// <summary>Stable identity of an input device: serial if available, otherwise VID:PID:path.</summary>
public readonly record struct DeviceId(string Value)
{
    public override string ToString() => Value;
}
