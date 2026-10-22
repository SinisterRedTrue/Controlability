using System.Diagnostics;
using Controlability.Core.Settings;
using Controlability.Engine;
using Microsoft.Extensions.DependencyInjection;

namespace Controlability.Cli;

internal static partial class Commands
{
    /// <summary>Forwards one controller to virtual pad 1 through <see cref="IEngine"/> (Manual policy, nothing saved).</summary>
    public static int Forward(int index, CancellationToken cancel)
    {
        using var provider = BuildEngine(settings => settings.SlotPolicy = SlotPolicy.Manual, persist: false);
        var engine = provider.GetRequiredService<IEngine>();
        if (!engine.DriverStatus.ViGEmAvailable)
        {
            Console.Error.WriteLine("ViGEmBus is not installed or not reachable. Run 'driver' for details.");
            return 1;
        }

        engine.Start();
        var width = SafeWindowWidth();
        while (!cancel.IsCancellationRequested)
        {
            var devices = engine.Devices;
            var device = index < devices.Count ? devices[index] : null;
            string line;
            if (device is null)
            {
                line = $"Waiting for controller [{index}]... ({devices.Count} connected)";
            }
            else
            {
                if (engine.Slots[0].Device != device.Id)
                {
                    engine.Assign(device.Id, 0);
                }

                var slot = engine.Slots[0];
                line = $"[{index}] {device.Name} -> {slot.State}, XInput {(slot.XInputIndex?.ToString() ?? "?")}";
                if (engine.TryGetLiveState(device.Id, out _, out var mapped))
                {
                    var pressed = mapped.Buttons == 0 ? "-" : $"0x{mapped.Buttons:X4}";
                    line += FormattableString.Invariant(
                        $" | L {mapped.LeftX,6},{mapped.LeftY,6} R {mapped.RightX,6},{mapped.RightY,6} | LT {mapped.LeftTrigger,3} RT {mapped.RightTrigger,3} | {pressed}");
                }
            }

            Console.Write('\r' + Fit(line, width));
            cancel.WaitHandle.WaitOne(PrintIntervalMs);
        }

        Console.WriteLine();
        return 0;
    }

    /// <summary>Full engine with the saved settings (auto-assign by default). Prints slot changes and the CPU used.</summary>
    public static int Run(int seconds, CancellationToken cancel)
    {
        using var provider = BuildEngine(null, persist: true);
        var engine = provider.GetRequiredService<IEngine>();
        var drivers = engine.DriverStatus;
        Console.WriteLine($"ViGEmBus: {(drivers.ViGEmAvailable ? "available" : "NOT available (no virtual pads)")} | HidHide: {(drivers.HidHideAvailable ? "available" : "not available")}");

        var changed = 0;
        engine.Changed += () => Interlocked.Exchange(ref changed, 1);

        using var process = Process.GetCurrentProcess();
        var cpuStart = process.TotalProcessorTime;
        var wallStart = Stopwatch.GetTimestamp();
        engine.Start();
        Console.WriteLine(seconds > 0 ? $"Running for {seconds} s. Ctrl+C to stop early." : "Running. Ctrl+C to stop.");

        var end = seconds > 0 ? wallStart + (long)(seconds * (double)Stopwatch.Frequency) : long.MaxValue;
        while (!cancel.IsCancellationRequested && Stopwatch.GetTimestamp() < end)
        {
            if (Interlocked.Exchange(ref changed, 0) == 1)
            {
                PrintSlots(engine);
            }

            cancel.WaitHandle.WaitOne(100);
        }

        var cpu = TotalCpu(process) - cpuStart;
        var wall = (Stopwatch.GetTimestamp() - wallStart) / (double)Stopwatch.Frequency;
        engine.Stop();
        Console.WriteLine(FormattableString.Invariant(
            $"CPU: {cpu.TotalSeconds:0.00} s over {wall:0.0} s = {cpu.TotalSeconds / wall * 100:0.00}% of one core, {cpu.TotalSeconds / wall / Environment.ProcessorCount * 100:0.00}% of the machine ({Environment.ProcessorCount} logical cores). Tick errors: {engine.TickErrorCount}."));
        return 0;
    }

    private static TimeSpan TotalCpu(Process process)
    {
        process.Refresh();
        return process.TotalProcessorTime;
    }

    private static void PrintSlots(IEngine engine)
    {
        Console.WriteLine($"-- {engine.Devices.Count} device(s) --");
        foreach (var slot in engine.Slots)
        {
            var detail = slot.State == SlotState.Empty
                ? "(empty)"
                : $"{slot.DeviceName ?? slot.Device?.ToString()} | profile {slot.ProfileId ?? "?"} | XInput {(slot.XInputIndex?.ToString() ?? "?")}";
            Console.WriteLine($"  P{slot.Index + 1} {slot.State,-7} {detail}");
        }
    }

    private static ServiceProvider BuildEngine(Action<Settings>? configure, bool persist)
    {
        var services = new ServiceCollection();
        services.AddControlabilityEngine(options =>
        {
            options.ConfigureSettings = configure;
            options.PersistSettings = persist;
            options.Log = message => Console.Error.WriteLine($"[engine] {message}");
        });
        return services.BuildServiceProvider();
    }
}
