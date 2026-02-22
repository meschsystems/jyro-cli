using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Jyro.Cli;

/// <summary>
/// A simple file-based logger provider that supports both text and JSON Lines output formats.
/// </summary>
public sealed partial class SimpleFileLoggerProvider : ILoggerProvider
{
    private readonly string _filePath;
    private readonly bool _useJson;
    private readonly object _lock = new();

    private sealed record LogEntry(string Timestamp, string LogLevel, string Category, string Message, string? Exception);

    [JsonSerializable(typeof(LogEntry))]
    private sealed partial class LogEntryJsonContext : JsonSerializerContext;


    public SimpleFileLoggerProvider(string filePath, bool useJson = false)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _useJson = useJson;

        // Ensure directory exists
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new SimpleFileLogger(_filePath, categoryName, _useJson, _lock);
    }

    public void Dispose()
    {
        // Nothing to dispose
    }

    private sealed class SimpleFileLogger : ILogger
    {
        private readonly string _filePath;
        private readonly string _categoryName;
        private readonly bool _useJson;
        private readonly object _lock;

        public SimpleFileLogger(string filePath, string categoryName, bool useJson, object lockObject)
        {
            _filePath = filePath;
            _categoryName = categoryName;
            _useJson = useJson;
            _lock = lockObject;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);

            string logLine;
            if (_useJson)
            {
                logLine = JsonSerializer.Serialize(
                    new LogEntry(
                        DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz"),
                        logLevel.ToString(),
                        _categoryName,
                        message,
                        exception?.ToString()),
                    LogEntryJsonContext.Default.LogEntry);
            }
            else
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                logLine = $"{timestamp} [{logLevel}] {_categoryName}: {message}";
                if (exception != null)
                {
                    logLine += Environment.NewLine + exception.ToString();
                }
            }

            lock (_lock)
            {
                File.AppendAllText(_filePath, logLine + Environment.NewLine);
            }
        }
    }
}
