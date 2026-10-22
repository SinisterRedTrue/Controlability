using Controlability.Core.Models;

namespace Controlability.Core.Mapping;

public static class Mapper
{
    /// <summary>Converts a normalized input to an Xbox report. Pure and allocation-free.</summary>
    public static XboxState Map(in InputState input, MappingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var state = new XboxState();
        ProcessStick(input.LeftX, input.LeftY, settings.LeftStick, out state.LeftX, out state.LeftY);
        ProcessStick(input.RightX, input.RightY, settings.RightStick, out state.RightX, out state.RightY);
        state.LeftTrigger = TriggerProcessor.ToByte(TriggerProcessor.Process(input.LeftTrigger, settings.Triggers));
        state.RightTrigger = TriggerProcessor.ToByte(TriggerProcessor.Process(input.RightTrigger, settings.Triggers));
        state.Buttons = (ushort)settings.Buttons.Remap(input.Buttons);
        return state;
    }

    private static void ProcessStick(float x, float y, StickSettings stick, out short outX, out short outY)
    {
        stick.DeadZone.Apply(x, y, out var dx, out var dy);
        if (stick.DeadZone.Mode == DeadZoneMode.Radial)
        {
            stick.Curve.ApplyRadial(dx, dy, out dx, out dy);
        }
        else
        {
            dx = stick.Curve.Apply(dx);
            dy = stick.Curve.Apply(dy);
        }

        outX = StickConverter.ToShort(dx);
        outY = StickConverter.ToShort(dy);
    }
}
