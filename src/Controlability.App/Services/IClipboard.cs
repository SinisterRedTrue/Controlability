using System.Windows;

namespace Controlability.App.Services;

public interface IClipboard
{
    /// <summary>Puts the text on the clipboard. Returns false (and does not throw) if another app holds the clipboard.</summary>
    bool SetText(string text);
}

public sealed class WpfClipboard : IClipboard
{
    public bool SetText(string text)
    {
        try
        {
            Clipboard.SetText(text);
            return true;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return false;
        }
    }
}
