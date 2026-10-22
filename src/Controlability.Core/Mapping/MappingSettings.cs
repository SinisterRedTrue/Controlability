namespace Controlability.Core.Mapping;

public sealed record StickSettings(DeadZone DeadZone, ResponseCurve Curve)
{
    public static readonly StickSettings Default = new(DeadZone.Default, ResponseCurve.Linear);
}

/// <summary>Everything <see cref="Mapper"/> needs; profiles are turned into this.</summary>
public sealed record MappingSettings(
    StickSettings LeftStick,
    StickSettings RightStick,
    TriggerSettings Triggers,
    ButtonRemapper Buttons)
{
    public static readonly MappingSettings Default = new(
        StickSettings.Default,
        StickSettings.Default,
        TriggerSettings.Default,
        DefaultMappings.Generic);
}
