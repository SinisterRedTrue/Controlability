namespace Controlability.Core.Models;

/// <summary>Xbox 360 report: XInput button bits, triggers 0..255, sticks -32768..32767 (up = positive).</summary>
public struct XboxState
{
    public ushort Buttons;
    public byte LeftTrigger;
    public byte RightTrigger;
    public short LeftX;
    public short LeftY;
    public short RightX;
    public short RightY;

    public static readonly XboxState Neutral = default;

    public readonly bool IsPressed(XboxButton button) => (Buttons & (ushort)button) == (ushort)button;
}
