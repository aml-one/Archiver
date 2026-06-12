using System.Text.Json.Serialization;

namespace Archiver.Core.Configuration;

public enum ScheduleType
{
    Daily,
    Interval,
    Cron
}

/// <summary>
/// Defines when a copy job runs. Supports three modes: Daily (wall-clock times),
/// Interval (every N minutes), and Cron (arbitrary expression).
/// </summary>
public sealed class ScheduleOptions
{
    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ScheduleType Type { get; set; } = ScheduleType.Daily;

    /// <summary>Wall-clock times in "HH:mm" format. Used when Type is Daily.</summary>
    [JsonPropertyName("times")]
    public List<string>? Times { get; set; }

    /// <summary>Interval in minutes. Used when Type is Interval.</summary>
    [JsonPropertyName("intervalMinutes")]
    public int? IntervalMinutes { get; set; }

    /// <summary>Cron expression. Used when Type is Cron.</summary>
    [JsonPropertyName("cronExpression")]
    public string? CronExpression { get; set; }

    /// <summary>Parse "HH:mm" into a TimeSpan.</summary>
    public static TimeSpan ParseTime(string timeString)
    {
        if (TimeSpan.TryParse(timeString, out var ts))
            return ts;
        throw new FormatException($"Invalid time format: '{timeString}'. Expected HH:mm.");
    }
}
