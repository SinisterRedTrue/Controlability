using Controlability.Core.Settings;
using Controlability.Platform.Logging;
using Microsoft.Extensions.Logging;

namespace Controlability.App.Services;

/// <summary>Sends <see cref="ILogger"/> output to the rolling log file; the file log applies its own (live) level.</summary>
public sealed class FileLoggerProvider(RollingFileLog log) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(log, categoryName);

    public void Dispose()
    {
    }

    private sealed class FileLogger(RollingFileLog log, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && log.IsEnabled(Map(logLevel));

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            log.Write(Map(logLevel), category, exception is null ? message : $"{message}{Environment.NewLine}{exception}");
        }

        private static AppLogLevel Map(LogLevel level) => level switch
        {
            LogLevel.Trace or LogLevel.Debug => AppLogLevel.Debug,
            LogLevel.Information => AppLogLevel.Information,
            LogLevel.Warning => AppLogLevel.Warning,
            _ => AppLogLevel.Error,
        };
    }
}
