namespace Controlability.Core.Mapping;

/// <param name="DeadZone">Values at or below this read as 0; the rest is rescaled to 0..1.</param>
/// <param name="Digital">When true the trigger is all or nothing.</param>
/// <param name="DigitalThreshold">In digital mode the trigger is fully pressed at or above this value.</param>
public sealed record TriggerSettings(float DeadZone = 0.02f, bool Digital = false, float DigitalThreshold = 0.5f)
{
    public static readonly TriggerSettings Default = new();
}

public static class TriggerProcessor
{
    /// <summary>Returns the processed trigger value, 0..1.</summary>
    public static float Process(float value, TriggerSettings settings)
    {
        if (!(value > 0f))
        {
            return 0f;
        }

        var clamped = MathF.Min(value, 1f);
        if (settings.Digital)
        {
            return clamped >= settings.DigitalThreshold ? 1f : 0f;
        }

        var deadZone = MathF.Max(settings.DeadZone, 0f);
        if (!(clamped > deadZone))
        {
            return 0f;
        }

        var range = 1f - deadZone;
        return range > 0f ? (clamped - deadZone) / range : 1f;
    }

    public static byte ToByte(float value) => (byte)MathF.Round(Math.Clamp(value, 0f, 1f) * 255f);
}
