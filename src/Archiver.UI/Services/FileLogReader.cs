using System.IO;
using System.Text.Json;
using Archiver.Core;

namespace Archiver.UI.Services;

/// <summary>
/// Reads structured JSON log files from the ProgramData logs directory.
/// </summary>
public sealed class FileLogReader : ILogReader
{
    private readonly string _logDirectory;

    public FileLogReader(string? logDirectory = null)
    {
        _logDirectory = logDirectory ?? Constants.DefaultLogDirectory;
    }

    public async Task<List<LogEntryModel>> ReadLogsAsync(
        string? dateFilter = null,
        string? levelFilter = null,
        string? searchText = null,
        int maxLines = 500)
    {
        var entries = new List<LogEntryModel>();

        try
        {
            if (!Directory.Exists(_logDirectory))
                return entries;

            string pattern;
            if (!string.IsNullOrWhiteSpace(dateFilter))
            {
                pattern = $"archiver-{dateFilter}.log";
            }
            else
            {
                // Default to today
                pattern = $"archiver-{DateTime.UtcNow:yyyy-MM-dd}.log";
            }

            var files = Directory.GetFiles(_logDirectory, pattern).OrderByDescending(f => f).ToList();
            if (files.Count == 0)
            {
                // Try any log file if specific date not found
                files = Directory.GetFiles(_logDirectory, "archiver-*.log")
                    .OrderByDescending(f => f)
                    .Take(1)
                    .ToList();
            }

            foreach (var file in files)
            {
                var lines = await File.ReadAllLinesAsync(file);

                // Read last N lines (most recent first)
                foreach (var line in lines.Reverse().Take(maxLines))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    try
                    {
                        var entry = JsonSerializer.Deserialize<LogEntryModel>(line,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (entry == null)
                            continue;

                        // Apply filters
                        if (!string.IsNullOrWhiteSpace(levelFilter) &&
                            !string.Equals(entry.Level, levelFilter, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!string.IsNullOrWhiteSpace(searchText) &&
                            !entry.Message.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
                            !entry.Category.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                            continue;

                        entries.Add(entry);
                    }
                    catch
                    {
                        // Skip malformed lines
                    }
                }
            }
        }
        catch
        {
            // Log directory may not exist yet
        }

        return entries;
    }

    public Task<string[]> GetAvailableDatesAsync()
    {
        try
        {
            if (!Directory.Exists(_logDirectory))
                return Task.FromResult(Array.Empty<string>());

            var dates = Directory.GetFiles(_logDirectory, "archiver-*.log")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .Select(f => f.Replace("archiver-", ""))
                .OrderByDescending(d => d)
                .ToArray();

            return Task.FromResult(dates);
        }
        catch
        {
            return Task.FromResult(Array.Empty<string>());
        }
    }
}
