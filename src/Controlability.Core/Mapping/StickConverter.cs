namespace Controlability.Core.Mapping;

public static class StickConverter
{
    /// <summary>Converts -1..+1 (up = +1) to the XInput range, clamping anything beyond. NaN becomes 0.</summary>
    public static short ToShort(float value)
    {
        if (float.IsNaN(value))
        {
            return 0;
        }

        return (short)Math.Clamp(Math.Round(value * 32767.0), -32768.0, 32767.0);
    }
}
