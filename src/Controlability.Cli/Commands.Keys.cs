using Controlability.Core.Keyboard;
using Controlability.Input.Keyboard;

namespace Controlability.Cli;

internal static partial class Commands
{
    /// <summary>Prints raw keyboard events with the device id, so the user can tell the keyboards apart. Nothing is blocked.</summary>
    public static int Keys(CancellationToken cancel)
    {
        var keyboards = RawInputWindow.EnumerateKeyboards();
        Console.WriteLine(keyboards.Count == 0 ? "No keyboards found." : $"{keyboards.Count} keyboard device(s):");
        foreach (var keyboard in keyboards)
        {
            Console.WriteLine($"  {keyboard.Name}  {keyboard.Id}");
        }

        Console.WriteLine();
        Console.WriteLine("Press keys on each keyboard. The id on the right goes into Settings.KeyboardDevice (settings.json). Ctrl+C to quit.");

        using var window = new RawInputWindow();
        window.Key += e => Console.WriteLine(
            $"{DateTime.Now:HH:mm:ss.fff}  {(e.Down ? "down" : "up  ")}  {KeyCodes.NameOf(e.VirtualKey),-12} (0x{e.VirtualKey:X2})  {e.Keyboard}");
        try
        {
            window.Start();
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        cancel.WaitHandle.WaitOne();
        return 0;
    }
}
