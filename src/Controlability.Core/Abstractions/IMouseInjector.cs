using Controlability.Core.Keyboard;

namespace Controlability.Core.Abstractions;

/// <summary>Puts mouse input into Windows (<c>SendInput</c>). Used by the touchpad-as-mouse extra.</summary>
public interface IMouseInjector
{
    /// <summary>Moves the cursor relative to where it is, in pixels.</summary>
    void Move(int dx, int dy);

    /// <summary>Presses or releases one button.</summary>
    void SetButton(MouseButtons button, bool down);
}
