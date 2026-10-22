using Controlability.Platform;
using Controlability.Platform.HidHide;

namespace Controlability.Cli;

internal static partial class Commands
{
    /// <summary>Prints the HidHide status, the whitelist, the hidden devices and what our journal says we hid.</summary>
    public static int HidHide()
    {
        var service = CreateHidHide();
        if (!service.IsAvailable)
        {
            Console.WriteLine("HidHide: not installed (HidHideCLI.exe not found in Program Files, the registry or PATH).");
            PrintJournal(service);
            return 1;
        }

        HidHideStatus status;
        try
        {
            status = service.GetStatus()!;
        }
        catch (HidHideCliException ex)
        {
            Console.Error.WriteLine($"HidHide: {ex.Message}");
            return 1;
        }

        Console.WriteLine($"HidHide:   {status.Version} ({status.ExePath})");
        Console.WriteLine($"Cloaking:  {(status.CloakOn ? "on" : "off")}");
        Console.WriteLine($"Inverse:   {(status.InverseOn ? "ON (the application list is a blacklist!)" : "off")}");
        Console.WriteLine("Whitelisted applications:");
        PrintList(status.Applications);
        Console.WriteLine("Hidden devices (all, from HidHide):");
        PrintList(status.HiddenDevices);
        PrintJournal(service);
        Console.WriteLine("Gaming devices (what HidHide can hide):");
        PrintList(status.GamingDevices.Select(d => $"{d.InstancePath}{(d.Present ? string.Empty : " (not present)")}"));
        return 0;
    }

    /// <summary>Unhides every device our journal lists (what a crashed or killed run left behind). Other hidden devices stay hidden.</summary>
    public static int UnhideAll()
    {
        var service = CreateHidHide();
        var journaled = service.HiddenByUs;
        if (journaled.Count == 0)
        {
            Console.WriteLine("Nothing to unhide: the journal is empty.");
            return 0;
        }

        service.UnhideAll();
        var remaining = service.HiddenByUs;
        Console.WriteLine($"Unhid {journaled.Count - remaining.Count} of {journaled.Count} device(s).");
        foreach (var entry in remaining)
        {
            Console.Error.WriteLine($"  still hidden: {entry.InstancePath}");
        }

        return remaining.Count == 0 ? 0 : 1;
    }

    private static HidHideService CreateHidHide() =>
        HidHideService.CreateDefault(AppPaths.ForCurrentUser().HiddenJournalFile, message => Console.Error.WriteLine($"[hidhide] {message}"));

    private static void PrintJournal(HidHideService service)
    {
        Console.WriteLine("Hidden by Controlability (journal):");
        PrintList(service.HiddenByUs.Select(e => $"{e.InstancePath}  [{e.DeviceId}]"));
    }

    private static void PrintList(IEnumerable<string> lines)
    {
        var any = false;
        foreach (var line in lines)
        {
            Console.WriteLine($"  {line}");
            any = true;
        }

        if (!any)
        {
            Console.WriteLine("  (none)");
        }
    }
}
