using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Controlability.Core.Settings;
using Controlability.Engine;
using Controlability.Platform.Logging;

namespace Controlability.App.Services;

/// <summary>Builds the text of "Copy diagnostics": versions, drivers, devices, settings and the end of the log.</summary>
public sealed class DiagnosticsService(IEngine engine, IDriverProbe drivers, Settings settings, RollingFileLog log, IClipboard clipboard)
{
    public const int LogLines = 200;

    public string Build()
    {
        var text = new StringBuilder();
        text.AppendLine($"Controlability {AppInfo.Version}");
        text.AppendLine($"Windows: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        text.AppendLine($"Runtime: {RuntimeInformation.FrameworkDescription}");

        var info = drivers.Read();
        text.AppendLine($"ViGEmBus: {(info.ViGEmInstalled ? info.ViGEmVersion : "not installed")}");
        text.AppendLine($"HidHide: {(info.HidHideInstalled ? info.HidHideVersion : "not installed")}");
        text.AppendLine($"Engine: {(engine.IsRunning ? "running" : "stopped")}{(engine.IsPaused ? ", paused" : "")}, mode {engine.Mode}, tick errors {engine.TickErrorCount}");

        text.AppendLine();
        text.AppendLine("Devices:");
        var devices = engine.Devices;
        if (devices.Count == 0)
        {
            text.AppendLine("  (none)");
        }

        foreach (var device in devices)
        {
            text.AppendLine($"  {device.Name} [{device.Type}, {device.Connection}] {device.Vid:X4}:{device.Pid:X4} id={device.Id}");
        }

        text.AppendLine();
        text.AppendLine("Settings:");
        text.AppendLine(JsonSerializer.Serialize(settings, SettingsJsonContext.Default.Settings));

        text.AppendLine();
        text.AppendLine($"Last {LogLines} log lines:");
        foreach (var line in log.ReadTail(LogLines))
        {
            text.AppendLine(line);
        }

        return text.ToString();
    }

    public bool CopyToClipboard() => clipboard.SetText(Build());
}
