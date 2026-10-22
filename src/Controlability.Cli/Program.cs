using Controlability.Cli;

const string Usage = """
    Controlability CLI

    Usage: Controlability.Cli <command> [index]

      list             Print every connected controller with all device fields, then exit.
      watch [index]    Live view of sticks, triggers, buttons and battery (Ctrl+C to quit).
      rumble [index]   Rumble for 1 s and cycle the LED red, green, blue.
      forward [index]  Forward the controller to virtual pad 1 through the engine, with rumble (Ctrl+C to quit).
      run [seconds]    Full engine with auto-assign: every controller gets the next free slot. Prints slot
                       changes and the CPU used; stops after [seconds] (default: until Ctrl+C).
      latency [seconds] [index]
                       Like forward for N seconds (default 10), then print read -> submit latency.
      driver           Print the ViGEmBus driver status.

    [index] is the zero-based position shown by 'list' (default 0).
    """;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 1 : 0;
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
        return Commands.Run(seconds, cts.Token);
    case "latency":
        return Commands.Latency(seconds, index, cts.Token);
    case "driver":
        return Commands.Driver();
    default:
        Console.Error.WriteLine($"Unknown command '{args[0]}'.");
        Console.WriteLine(Usage);
        return 1;
}
