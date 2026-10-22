using System.Text.Json;
using System.Text.Json.Serialization;

namespace Controlability.Platform.HidHide;

/// <param name="DeviceId">The stable <c>DeviceId</c> of the controller.</param>
/// <param name="InstancePath">What was passed to <c>--dev-hide</c>.</param>
public sealed record HiddenEntry(string DeviceId, string InstancePath);

internal sealed record HiddenJournalFile
{
    public int Version { get; set; } = 1;

    public List<HiddenEntry> Devices { get; set; } = [];
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(HiddenJournalFile))]
internal sealed partial class HiddenJournalJsonContext : JsonSerializerContext
{
}

/// <summary>
/// <c>hidden.json</c>: the devices we asked HidHide to hide. An entry is written before the hide and removed after the
/// unhide, so after a crash the next start knows exactly what to give back. Always read from and written to disk.
/// </summary>
public sealed class HiddenJournal(string path)
{
    private readonly object _gate = new();

    public string FilePath { get; } = path;

    public IReadOnlyList<HiddenEntry> Load()
    {
        lock (_gate)
        {
            return [.. Read()];
        }
    }

    public void Add(HiddenEntry entry)
    {
        lock (_gate)
        {
            var entries = Read();
            if (entries.Any(e => Same(e, entry)))
            {
                return;
            }

            entries.Add(entry);
            Write(entries);
        }
    }

    public void Remove(string instancePath)
    {
        lock (_gate)
        {
            var entries = Read();
            if (entries.RemoveAll(e => string.Equals(e.InstancePath, instancePath, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                Write(entries);
            }
        }
    }

    private static bool Same(HiddenEntry a, HiddenEntry b) =>
        a.DeviceId == b.DeviceId && string.Equals(a.InstancePath, b.InstancePath, StringComparison.OrdinalIgnoreCase);

    private List<HiddenEntry> Read()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            var file = JsonSerializer.Deserialize(File.ReadAllText(FilePath), HiddenJournalJsonContext.Default.HiddenJournalFile);
            return file?.Devices.Where(e => !string.IsNullOrWhiteSpace(e?.InstancePath)).ToList() ?? [];
        }
        catch (JsonException)
        {
            // A half-written or hand-edited file must not stop the app; HidHide's own list (hidhide command) stays the fallback.
            return [];
        }
    }

    private void Write(List<HiddenEntry> entries)
    {
        if (entries.Count == 0)
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }

            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(FilePath))!;
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(new HiddenJournalFile { Devices = entries }, HiddenJournalJsonContext.Default.HiddenJournalFile);
        var temp = Path.Combine(directory, $"{Path.GetFileName(FilePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, json);
            if (File.Exists(FilePath))
            {
                File.Replace(temp, FilePath, null);
            }
            else
            {
                File.Move(temp, FilePath);
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }
}
