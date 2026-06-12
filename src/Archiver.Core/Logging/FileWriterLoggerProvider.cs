using Microsoft.Extensions.Logging;

namespace Archiver.Core.Logging;

/// <summary>
/// Logger provider that writes structured JSON logs to rolling daily files
/// in C:\ProgramData\Archiver\logs\.
/// </summary>
public sealed class FileWriterLoggerProvider : ILoggerProvider
{
    private readonly string _logDirectory;
    private readonly int _retentionDays;
    private readonly object _writeLock = new();
    private StreamWriter? _writer;
    private string _currentLogDate = string.Empty;

    public LogLevel MinLogLevel { get; } = LogLevel.Information;

    public FileWriterLoggerProvider(string? logDirectory = null, int retentionDays = 30)
    {
        _logDirectory = logDirectory ?? Constants.DefaultLogDirectory;
        _retentionDays = retentionDays;
        Directory.CreateDirectory(_logDirectory);
        CleanOldLogs();
    }

    public ILogger CreateLogger(string categoryName)
        => new FileWriterLogger(categoryName, this);

    public void WriteLog(string line)
    {
        lock (_writeLock)
        {
            try
            {
                var today = DateTime.UtcNow.ToString("yyyy-MM-dd");
                if (today != _currentLogDate || _writer == null)
                {
                    _writer?.Dispose();
                    var logFile = Path.Combine(_logDirectory, $"archiver-{today}.log");
                    _writer = new StreamWriter(logFile, append: true, System.Text.Encoding.UTF8, bufferSize: 4096)
                    {
                        AutoFlush = false
                    };
                    _currentLogDate = today;
                }

                _writer.Write(line);
                _writer.Flush();
            }
            catch
            {
                // Don't let logging failures crash the service
            }
        }
    }

    private void CleanOldLogs()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);
            foreach (var file in Directory.GetFiles(_logDirectory, "archiver-*.log"))
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
            // Best-effort cleanup
        }
    }

    public void Dispose()
    {
        lock (_writeLock)
        {
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
        }
    }
}
