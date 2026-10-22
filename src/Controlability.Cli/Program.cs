using Controlability.Cli;
using Controlability.Core.Settings;

const string Usage = """
    Controlability CLI

    Usage: Controlability.Cli <command> [index]

      list             Print every connected controller with all device fields, then exit.
      watch [index]    Live view of sticks, triggers, buttons and battery (Ctrl+C to quit).
      rumble [index]   Rumble for 1 s and cycle the LED red, green, blue.
      forward [index]  Forward the controller to virtual pad 1 through the engine, with rumble (Ctrl+C to quit).
      run [seconds] [--mode a|b]
                       Full engine with auto-assign: every controller gets the next free slot. Prints slot
                       changes and the CPU used; stops after [seconds] (default: until Ctrl+C).
                       --mode b = keyboard-as-controller: keyboard + mouse become pad 1 (panic key
                       Ctrl+Alt+Pause), controllers start at pad 2. Not saved to the settings.
                       --game <process[,process]> = capture only while that game has the focus. Use the
                       name Task Manager > Details shows for the running game, with or without .exe.
                       Without it Mode B swallows no keys, so the PC stays usable.
      keys             Print raw keyboard events with the id of the keyboard they came from, to pick the
                       keyboard for Settings.KeyboardDevice (Ctrl+C to quit). Captures nothing.
      latency [seconds] [index]
                       Like forward for N seconds (default 10), then print read -> submit latency.
      driver           Print the ViGEmBus driver status.
      hidhide          Print the HidHide status, whitelist, hidden devices and our journal.
      unhide-all       Unhide every device Controlability hid (crash recovery).

    [index] is the zero-based position shown by 'list' (default 0).
    """;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 1 : 0;
}

args = StripMode(args, out var mode, out var games, out var modeError);
if (modeError is not null)
{
    Console.Error.WriteLine(modeError);
    return 1;
}

if (args.Length == 0)
{
    Console.WriteLine(Usage);
    return 1;
}

var isLatency = args[0].Equals("latency", StringComparison.OrdinalIgnoreCase);
var isRun = args[0].Equals("run", StringComparison.OrdinalIgnoreCase);
var seconds = isRun ? 0 : 10;
if ((isLatency || isRun) && args.Length > 1 && (!int.TryParse(args[1], out seconds) || seconds < 1))
{
    Console.Error.WriteLine($"Invalid seconds '{args[1]}'.");
    return 1;
}

var indexArg = isLatency ? 2 : isRun ? int.MaxValue : 1;
var index = 0;
if (args.Length > indexArg && (!int.TryParse(args[indexArg], out index) || index < 0))
{
    Console.Error.WriteLine($"Invalid index '{args[indexArg]}'.");
    return 1;
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

switch (args[0].ToLowerInvariant())
{
    case "list":
        return Commands.List();
    case "watch":
        return Commands.Watch(index, cts.Token);
    case "rumble":
        return Commands.Rumble(index);
    case "forward":
        return Commands.Forward(index, cts.Token);
    case "run":
        return Commands.Run(seconds, mode, games, cts.Token);
    case "keys":
        return Commands.Keys(cts.Token);
    case "latency":
        return Commands.Latency(seconds, index, cts.Token);
    case "driver":
        return Commands.Driver();
    case "hidhide":
        return Commands.HidHide();
    case "unhide-all":
        return Commands.UnhideAll();
    default:
        Console.Error.WriteLine($"Unknown command '{args[0]}'.");
        Console.WriteLine(Usage);
        return 1;
}

static string[] StripMode(string[] arguments, out KeyboardMode? mode, out List<string>? games, out string? error)
{
    mode = null;
    games = null;
    error = null;
    var rest = new List<string>();
    for (var i = 0; i < arguments.Length; i++)
    {
        if (arguments[i].Equals("--game", StringComparison.OrdinalIgnoreCase))
        {
            var names = i + 1 < arguments.Length ? arguments[++i] : "";
            games = [.. names.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];
            continue;
        }

        if (!arguments[i].Equals("--mode", StringComparison.OrdinalIgnoreCase))
        {
            rest.Add(arguments[i]);
            continue;
        }

        var value = i + 1 < arguments.Length ? arguments[++i] : "";
        switch (value.ToLowerInvariant())
        {
            case "a":
                mode = KeyboardMode.KeyboardFirst;
                break;
            case "b":
                mode = KeyboardMode.KeyboardAsController;
                break;
            default:
                error = $"Invalid mode '{value}': use a or b.";
                break;
        }
    }

    return [.. rest];
}
