using System.Text;
using Controlability.Core.Abstractions;
using Controlability.Core.Models;
using Controlability.Input.Sdl;

namespace Controlability.Cli;

internal static partial class Commands
{
    private const double PollIntervalMs = 1.0;
    private const int PrintIntervalMs = 33;
    private static readonly TimeSpan SettleTime = TimeSpan.FromMilliseconds(500);

    public static int List()
    {
        using var source = CreateSource();
        var devices = new List<DeviceInfo>();
        source.DeviceAdded += devices.Add;
        PollFor(source, SettleTime);

        if (devices.Count == 0)
        {
            Console.WriteLine("No controllers found.");
            return 0;
        }

        for (var i = 0; i < devices.Count; i++)
        {
            var d = devices[i];
            Console.WriteLine($"[{i}] {d.Name}");
            Console.WriteLine($"    Id:           {d.Id}");
            Console.WriteLine($"    VID:PID:      {d.Vid:X4}:{d.Pid:X4}");
            Console.WriteLine($"    Serial:       {d.Serial ?? "(none)"}");
            Console.WriteLine($"    HID path:     {d.HidPath ?? "(none)"}");
            Console.WriteLine($"    Connection:   {d.Connection}");
            Console.WriteLine($"    Type:         {d.Type}");
            Console.WriteLine($"    Capabilities: {d.Capabilities}");
        }

        return 0;
    }

    public static int Watch(int index, CancellationToken cancel)
    {
        using var source = CreateSource();
        var devices = new List<DeviceInfo>();
        source.DeviceAdded += devices.Add;
        source.DeviceRemoved += id => devices.RemoveAll(d => d.Id == id);

        using var timer = new HighResTimer();
        var width = SafeWindowWidth();
        var nextPrint = Environment.TickCount64;
        while (!cancel.IsCancellationRequested)
        {
            source.Poll();
            if (Environment.TickCount64 >= nextPrint)
            {
                nextPrint = Environment.TickCount64 + PrintIntervalMs;
                var line = index < devices.Count
                    ? FormatState(devices[index], source)
                    : $"Waiting for controller [{index}]... ({devices.Count} connected)";
                Console.Write('\r' + Fit(line, width));
            }

            timer.Sleep(PollIntervalMs);
        }

        Console.WriteLine();
        return 0;
    }

    public static int Rumble(int index)
    {
        using var source = CreateSource();
        var devices = new List<DeviceInfo>();
        source.DeviceAdded += devices.Add;
        PollFor(source, SettleTime);

        if (index >= devices.Count)
        {
            Console.Error.WriteLine(devices.Count == 0
                ? "No controllers found."
                : $"No controller at index {index} ({devices.Count} connected).");
            return 1;
        }

        var device = devices[index];
        Console.WriteLine($"[{index}] {device.Name} ({device.Capabilities})");
        if (!device.Capabilities.HasFlag(DeviceCapabilities.Rumble))
        {
            Console.WriteLine("Rumble is not supported on this device.");
        }

        if (!device.Capabilities.HasFlag(DeviceCapabilities.Led))
        {
            Console.WriteLine("LED is not supported on this device.");
        }

        source.SetRumble(device.Id, 255, 255, 1000);
        (byte R, byte G, byte B)[] colors = [(255, 0, 0), (0, 255, 0), (0, 0, 255)];
        foreach (var (r, g, b) in colors)
        {
            source.SetLed(device.Id, r, g, b);
            PollFor(source, TimeSpan.FromMilliseconds(1000.0 / colors.Length));
        }

        source.SetRumble(device.Id, 0, 0, 0);
        PollFor(source, TimeSpan.FromMilliseconds(50));
        return 0;
    }

    private static SdlControllerSource CreateSource()
    {
        var source = new SdlControllerSource(new StopwatchClock());
        source.Diagnostic += message => Console.Error.WriteLine($"[sdl] {message}");
        return source;
    }

    private static void PollFor(IInputSource source, TimeSpan duration)
    {
        using var timer = new HighResTimer();
        var end = Environment.TickCount64 + (long)duration.TotalMilliseconds;
        do
        {
            source.Poll();
            timer.Sleep(PollIntervalMs);
        }
        while (Environment.TickCount64 < end);
    }

    private static string FormatState(DeviceInfo device, IInputSource source)
    {
        if (!source.TryGetState(device.Id, out var s))
        {
            return $"{device.Name}: no state";
        }

        var buttons = s.Buttons == ButtonId.None ? "-" : s.Buttons.ToString().Replace(", ", "+");
        var sb = new StringBuilder();
        sb.Append(FormattableString.Invariant(
            $"L {s.LeftX,5:+0.00;-0.00;0.00},{s.LeftY,5:+0.00;-0.00;0.00} | R {s.RightX,5:+0.00;-0.00;0.00},{s.RightY,5:+0.00;-0.00;0.00} | LT {s.LeftTrigger:0.00} RT {s.RightTrigger:0.00} | {buttons} | Bat {s.Battery.State} {s.Battery.Percent}%"));
        return sb.ToString();
    }

    private static string Fit(string line, int width)
        => line.Length >= width ? line[..(width - 1)] : line.PadRight(width - 1);

    private static int SafeWindowWidth()
    {
        try
        {
            return Math.Max(40, Console.WindowWidth);
        }
        catch (IOException)
        {
            return 120;
        }
    }
}
