using System.Diagnostics;
using Controlability.Core.Abstractions;
using Controlability.Core.Mapping;
using Controlability.Core.Models;
using Controlability.Engine;
using Controlability.Output.ViGEm;

namespace Controlability.Cli;

internal static partial class Commands
{
    /// <summary>Game rumble is refreshed by the game; this only bounds a motor that is never switched off.</summary>
    private const int RumbleDurationMs = 60_000;

    private static readonly MappingSettings ForwardMapping = MappingSettings.Default with { Buttons = DefaultMappings.PlayStation };

    public static int Driver()
    {
        var status = ViGEmDriverCheck.Check();
        if (status.IsInstalled)
        {
            Console.WriteLine($"ViGEmBus: installed, version {status.Version}");
            return 0;
        }

        Console.WriteLine("ViGEmBus: NOT installed or not reachable.");
        Console.WriteLine($"Reason: {status.Error}");
        Console.WriteLine("Install it from https://github.com/nefarius/ViGEmBus/releases and reboot.");
        return 1;
    }

    public static int Latency(int seconds, int index, CancellationToken cancel)
    {
        var samples = new List<double>(seconds * 1000);
        Console.WriteLine($"Measuring for {seconds} s. Move the sticks and press buttons the whole time.");
        var result = RunForward(index, (TimeSpan.FromSeconds(seconds), samples), cancel);
        Console.WriteLine();
        if (result != 0)
        {
            return result;
        }

        if (samples.Count == 0)
        {
            Console.WriteLine("No state changes were seen, so there is nothing to measure.");
            return 1;
        }

        samples.Sort();
        Console.WriteLine(FormattableString.Invariant(
            $"Poll() read -> SubmitReport() returned, {samples.Count} submits: p50 {Percentile(samples, 0.50):0.000} ms | p95 {Percentile(samples, 0.95):0.000} ms | p99 {Percentile(samples, 0.99):0.000} ms | max {samples[^1]:0.000} ms"));
        return 0;
    }

    private static int RunForward(int index, (TimeSpan Duration, List<double> Samples)? latency, CancellationToken cancel)
    {
        using var sink = new ViGEmSink();
        if (!sink.IsAvailable)
        {
            Console.Error.WriteLine("ViGEmBus is not installed or not reachable. Run 'driver' for details.");
            return 1;
        }

        using var source = CreateSource();
        var devices = new List<DeviceInfo>();
        source.DeviceAdded += devices.Add;
        source.DeviceRemoved += id => devices.RemoveAll(d => d.Id == id);

        // The game's rumble arrives on a ViGEm thread; SetRumble only enqueues, so this is safe.
        DeviceInfo? active = null;
        sink.RumbleRequested += (_, large, small) =>
        {
            var device = Volatile.Read(ref active);
            if (device is not null)
            {
                source.SetRumble(device.Id, large, small, large == 0 && small == 0 ? 0 : RumbleDurationMs);
            }
        };

        var slot = sink.Connect(0);
        try
        {
            using var timer = new HighResTimer();
            var width = SafeWindowWidth();
            var nextPrint = Environment.TickCount64;
            var end = latency is { } l ? Stopwatch.GetTimestamp() + (long)(l.Duration.TotalSeconds * Stopwatch.Frequency) : long.MaxValue;
            var submits = 0;
            int? userIndex = null;
            var announced = false;

            while (!cancel.IsCancellationRequested && Stopwatch.GetTimestamp() < end)
            {
                // The poll that discovers a controller includes SDL enumeration, so it is not a latency sample.
                var measurable = Volatile.Read(ref active) is not null;
                var start = Stopwatch.GetTimestamp();
                source.Poll();

                var device = index < devices.Count ? devices[index] : null;
                Volatile.Write(ref active, device);

                var xbox = XboxState.Neutral;
                if (device is not null && source.TryGetState(device.Id, out var input))
                {
                    xbox = Mapper.Map(in input, ForwardMapping);
                }

                if (sink.SubmitIfChanged(slot, in xbox))
                {
                    submits++;
                    if (measurable)
                    {
                        latency?.Samples.Add((Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency);
                    }
                }

                userIndex ??= sink.GetXInputUserIndex(slot);
                if (userIndex is not null && !announced)
                {
                    announced = true;
                    Console.WriteLine($"Virtual Xbox 360 pad connected as XInput user index {userIndex} (check joy.cpl).");
                }

                if (Environment.TickCount64 >= nextPrint)
                {
                    nextPrint = Environment.TickCount64 + PrintIntervalMs;
                    var line = device is null
                        ? $"Waiting for controller [{index}]... ({devices.Count} connected)"
                        : $"[{index}] {device.Name} -> XInput {(userIndex?.ToString() ?? "?")} | {submits} reports sent | Ctrl+C to stop";
                    Console.Write('\r' + Fit(line, width));
                }

                timer.Sleep(PollIntervalMs);
            }

            Console.WriteLine();
            return 0;
        }
        finally
        {
            // Stop any rumble still running on the real pad, flush that command, then remove the virtual pad.
            var device = Volatile.Read(ref active);
            if (device is not null)
            {
                source.SetRumble(device.Id, 0, 0, 0);
                source.Poll();
            }

            sink.Disconnect(slot);
        }
    }

    private static double Percentile(List<double> sorted, double fraction)
        => sorted[Math.Clamp((int)Math.Ceiling(fraction * sorted.Count) - 1, 0, sorted.Count - 1)];
}
