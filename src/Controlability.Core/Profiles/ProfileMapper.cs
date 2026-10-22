using Controlability.Core.Mapping;
using Controlability.Core.Models;

namespace Controlability.Core.Profiles;

public static class ProfileMapper
{
    /// <summary>
    /// Builds the mapping settings for a profile. Throws <see cref="ProfileFormatException"/> if a value is out of range
    /// or a button name is unknown.
    /// </summary>
    public static MappingSettings ToMappingSettings(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        try
        {
            return new MappingSettings(
                ToStick(profile.Sticks.Left),
                ToStick(profile.Sticks.Right),
                new TriggerSettings(profile.Triggers.DeadZone, profile.Triggers.Digital, profile.Triggers.DigitalThreshold),
                ToButtons(profile.Buttons));
        }
        catch (ArgumentException ex)
        {
            throw new ProfileFormatException($"Profile \"{profile.Id}\" has an invalid value: {ex.Message}", ex);
        }
    }

    private static StickSettings ToStick(ProfileStick stick)
    {
        var curve = stick.Curve switch
        {
            CurveType.Quadratic => ResponseCurve.Quadratic,
            CurveType.Cubic => ResponseCurve.Cubic,
            CurveType.Custom => ResponseCurve.Custom(stick.Points.ToArray()),
            _ => ResponseCurve.Linear,
        };

        return new StickSettings(new DeadZone(stick.Mode, stick.DeadZone, stick.Outer), curve);
    }

    private static ButtonRemapper ToButtons(Dictionary<string, string> overrides)
    {
        var buttons = DefaultMappings.Generic;
        foreach (var (sourceName, targetName) in overrides)
        {
            if (!Enum.TryParse<ButtonId>(sourceName, ignoreCase: true, out var source) || !IsSingleBit((uint)source))
            {
                throw new ArgumentException($"Unknown source button \"{sourceName}\".");
            }

            if (!Enum.TryParse<XboxButton>(targetName, ignoreCase: true, out var target))
            {
                throw new ArgumentException($"Unknown Xbox button \"{targetName}\".");
            }

            buttons = buttons.With(source, target);
        }

        return buttons;
    }

    private static bool IsSingleBit(uint bits) => bits != 0 && (bits & (bits - 1)) == 0;
}
