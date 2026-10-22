using Controlability.Cli;

const string Usage = """
    Controlability CLI

    Usage: Controlability.Cli <command> [index]

      list             Print every connected controller with all device fields, then exit.
      watch [index]    Live view of sticks, triggers, buttons and battery (Ctrl+C to quit).
      rumble [index]   Rumble for 1 s and cycle the LED red, green, blue.

    [index] is the zero-based position shown by 'list' (default 0).
    """;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 1 : 0;
}

var index = 0;
if (args.Length > 1 && (!int.TryParse(args[1], out index) || index < 0))
{
    Console.Error.WriteLine($"Invalid index '{args[1]}'.");
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
    default:
        Console.Error.WriteLine($"Unknown command '{args[0]}'.");
        Console.WriteLine(Usage);
        return 1;
}
