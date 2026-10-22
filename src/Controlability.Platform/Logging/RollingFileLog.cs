using System.Globalization;
using System.Text;
using Controlability.Core.Settings;

namespace Controlability.Platform.Logging;

/// <summary>
/// One log file per day (<c>app-YYYYMMDD.log</c>) in a folder, old files deleted after <see cref="RetentionDays"/> days.
/// Thread safe; never throws, because a full disk must not take the app down.
/// </summary>
public sealed class RollingFileLog
{
    public const int RetentionDays = 7;

    private readonly string _folder;
    private readonly Func<DateTime> _now;
    private readonly object _gate = new();
    private string? _currentFile;
    private DateOnly _pruned;

    public RollingFileLog(string folder, AppLogLevel level = AppLogLevel.Information, Func<DateTime>? now = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        _folder = folder;
        Level = level;
        _now = now ?? (() => DateTime.Now);
    }

    /// <summary>Entries below this level are dropped. Can be changed while the app runs.</summary>
    public AppLogLevel Level { get; set; }

    public string Folder => _folder;

    public bool IsEnabled(AppLogLevel level) => level >= Level;

    public static string FileNameFor(DateOnly day) => $"app-{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.log";

    public void Write(AppLogLevel level, string category, string message)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        var now = _now();
        var line = $"{now:yyyy-MM-dd HH:mm:ss.fff} [{Tag(level)}] {category}: {message}{Environment.NewLine}";
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(_folder);
                var day = DateOnly.FromDateTime(now);
                _currentFile = Path.Combine(_folder, FileNameFor(day));
                File.AppendAllText(_currentFile, line, Encoding.UTF8);
                if (_pruned != day)
                {
                    _pruned = day;
                    Prune(day);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nowhere left to report it.
        }
    }

    /// <summary>The newest <paramref name="count"/> lines across the log files, oldest first.</summary>
    public IReadOnlyList<string> ReadTail(int count)
    {
        var lines = new List<string>();
        try
        {
            if (!Directory.Exists(_folder))
            {
                return lines;
            }

            // File names sort by date, so walking back from the newest file stops as soon as we have enough.
            foreach (var file in Directory.GetFiles(_folder, "app-*.log").OrderByDescending(f => f, StringComparer.Ordinal))
            {
                string[] fileLines;
                lock (_gate)
                {
                    using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    fileLines = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
                }

                lines.InsertRange(0, fileLines.Select(l => l.TrimEnd('\r')));
                if (lines.Count >= count)
                {
                    break;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Return what was read.
        }

        return lines.Count <= count ? lines : lines.GetRange(lines.Count - count, count);
    }

    private void Prune(DateOnly today)
    {
        var oldest = today.AddDays(-(RetentionDays - 1));
        foreach (var file in Directory.GetFiles(_folder, "app-*.log"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (name.Length == 12
                && DateOnly.TryParseExact(name[4..], "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                && day < oldest)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Still open somewhere; try again tomorrow.
                }
            }
        }
    }

    private static string Tag(AppLogLevel level) => level switch
    {
        AppLogLevel.Debug => "DBG",
        AppLogLevel.Information => "INF",
        AppLogLevel.Warning => "WRN",
        _ => "ERR",
    };
}
