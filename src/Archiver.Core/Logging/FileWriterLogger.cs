using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Archiver.Core.Logging;

/// <summary>
/// Writes structured JSON log entries to a rolling daily log file.
/// Thread-safe — all writes go through a single lock in the provider.
/// </summary>
public sealed class FileWriterLogger : ILogger
{
    private readonly string _categoryName;
    private readonly FileWriterLoggerProvider _provider;

    internal FileWriterLogger(string categoryName, FileWriterLoggerProvider provider)
    {
        _categoryName = categoryName;
        _provider = provider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel)
        => logLevel >= _provider.MinLogLevel;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var entry = new LogEntry
        {
            Timestamp = DateTime.UtcNow.ToString("O"),
            Level = logLevel.ToString(),
            Category = _categoryName,
            Message = formatter(state, exception),
            Exception = exception?.ToString()
        };

        var line = JsonSerializer.Serialize(entry) + Environment.NewLine;
        _provider.WriteLog(line);
    }
}

/// <summary>
/// JSON-serializable log entry for structured file logging.
/// </summary>
public sealed class LogEntry
{
    public string Timestamp { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}
