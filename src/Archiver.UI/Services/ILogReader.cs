namespace Archiver.UI.Services;

/// <summary>
/// Reads log entries from the rolling log files.
/// </summary>
public interface ILogReader
{
    Task<List<LogEntryModel>> ReadLogsAsync(string? dateFilter = null, string? levelFilter = null, string? searchText = null, int maxLines = 500);
    Task<string[]> GetAvailableDatesAsync();
}

public sealed class LogEntryModel
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Exception { get; set; }
}
