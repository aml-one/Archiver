using System.Text.Json.Serialization;

namespace Archiver.Core.Configuration;

/// <summary>
/// Cleanup/trimming policy for source folders after copy.
/// </summary>
public sealed class CleanupOptions
{
    /// <summary>Whether cleanup is enabled for this job.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    /// <summary>Delete files older than this many days. 0 means delete all files after copy.</summary>
    [JsonPropertyName("retentionDays")]
    public int RetentionDays { get; set; }

    /// <summary>
    /// Stop cleanup if free disk space falls below this percentage.
    /// A safety guard to avoid deleting files when the disk is already low.
    /// </summary>
    [JsonPropertyName("minFreeDiskPercent")]
    public int MinFreeDiskPercent { get; set; }

    /// <summary>
    /// If true, deletes empty subdirectories after removing expired files.
    /// </summary>
    [JsonPropertyName("removeEmptyDirectories")]
    public bool RemoveEmptyDirectories { get; set; }
}
