namespace Controlability.Core.Keyboard;

[Flags]
public enum MouseButtons : byte
{
    None = 0,
    Left = 1 << 0,
    Right = 1 << 1,
    Middle = 1 << 2,
    X1 = 1 << 3,
    X2 = 1 << 4,
}

public static class MouseButtonNames
{
    /// <summary>Parses "MouseLeft", "MouseRight", "MouseMiddle", "MouseX1"/"Mouse4", "MouseX2"/"Mouse5" (case-insensitive).</summary>
    public static bool TryParse(string name, out MouseButtons button)
    {
        button = (name ?? "").Trim().ToLowerInvariant() switch
        {
            "mouseleft" => MouseButtons.Left,
            "mouseright" => MouseButtons.Right,
            "mousemiddle" => MouseButtons.Middle,
            "mousex1" or "mouse4" => MouseButtons.X1,
            "mousex2" or "mouse5" => MouseButtons.X2,
            _ => MouseButtons.None,
        };
        return button != MouseButtons.None;
    }

    public static string NameOf(MouseButtons button) => button switch
    {
        MouseButtons.Left => "MouseLeft",
        MouseButtons.Right => "MouseRight",
        MouseButtons.Middle => "MouseMiddle",
        MouseButtons.X1 => "MouseX1",
        MouseButtons.X2 => "MouseX2",
        _ => button.ToString(),
    };
}
