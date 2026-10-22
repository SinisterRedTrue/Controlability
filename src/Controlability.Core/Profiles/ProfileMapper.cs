using Controlability.Core.Extras;
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

    /// <summary>
    /// Builds the extras (gyro, touchpad mouse, turbo, toggle). Throws <see cref="ProfileFormatException"/> on an unknown
    /// button name or an out-of-range value, like <see cref="ToMappingSettings"/>.
    /// </summary>
    public static ExtrasSettings ToExtrasSettings(Profile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var extras = profile.Extras;

        try
        {
            var gyroButton = ButtonId.None;
            if (extras.GyroButton.Length > 0
                && (!Enum.TryParse(extras.GyroButton, ignoreCase: true, out gyroButton) || !IsSingleBit((uint)gyroButton)))
            {
                throw new ArgumentException($"Unknown gyro button \"{extras.GyroButton}\".");
            }

            var turbo = new Dictionary<XboxButton, int>();
            foreach (var (name, hz) in extras.Turbo)
            {
                turbo[ParseXboxButton(name)] = hz;
            }

            var toggle = extras.Toggle.Select(ParseXboxButton).Distinct().ToList();

            var settings = new ExtrasSettings(
                new GyroSettings
                {
                    Enabled = extras.GyroToRightStick,
                    SensitivityX = extras.GyroSensitivityX,
                    SensitivityY = extras.GyroSensitivityY,
                    InvertX = extras.GyroInvertX,
                    InvertY = extras.GyroInvertY,
                    Smoothing = extras.GyroSmoothing,
                    Activation = extras.GyroActivation,
                    Button = gyroButton,
                },
                new TouchpadMouseSettings
                {
                    Enabled = extras.TouchpadMouse,
                    Sensitivity = extras.TouchpadSensitivity,
                    Acceleration = extras.TouchpadAcceleration,
                },
                turbo,
                toggle);

            // The processors validate the numbers; building them here reports a bad value when the profile loads.
            _ = new GyroProcessor(settings.Gyro);
            _ = new TouchpadMouseProcessor(settings.Touchpad);
            _ = new TurboProcessor(settings.Turbo);
            return settings;
        }
        catch (ArgumentException ex)
        {
            throw new ProfileFormatException($"Profile \"{profile.Id}\" has an invalid extra: {ex.Message}", ex);
        }
    }

    private static XboxButton ParseXboxButton(string name)
        => Enum.TryParse<XboxButton>(name, ignoreCase: true, out var button) && button != XboxButton.None
            ? button
            : throw new ArgumentException($"Unknown Xbox button \"{name}\".");

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
