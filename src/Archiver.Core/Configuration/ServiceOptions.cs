using System.Text.Json.Serialization;

namespace Archiver.Core.Configuration;

/// <summary>
/// Global service options that apply across all jobs.
/// </summary>
public sealed class ServiceOptions
{
    /// <summary>Days to retain log files before auto-deleting.</summary>
    [JsonPropertyName("logRetentionDays")]
    public int LogRetentionDays { get; set; } = Constants.DefaultLogRetentionDays;

    /// <summary>Number of retry attempts for transient copy failures.</summary>
    [JsonPropertyName("copyRetryCount")]
    public int CopyRetryCount { get; set; } = Constants.DefaultCopyRetryCount;

    /// <summary>Base delay in seconds between retries.</summary>
    [JsonPropertyName("copyRetryDelaySeconds")]
    public int CopyRetryDelaySeconds { get; set; } = Constants.DefaultCopyRetryDelaySeconds;

    /// <summary>Multiplier for exponential backoff (e.g. 2.0 = 10s, 20s, 40s).</summary>
    [JsonPropertyName("copyRetryBackoffMultiplier")]
    public double CopyRetryBackoffMultiplier { get; set; } = Constants.DefaultCopyRetryBackoffMultiplier;

    /// <summary>Max seconds to wait for a locked file before skipping it.</summary>
    [JsonPropertyName("fileLockTimeoutSeconds")]
    public int FileLockTimeoutSeconds { get; set; } = Constants.DefaultFileLockTimeoutSeconds;

    /// <summary>Maximum number of jobs that can execute simultaneously.</summary>
    [JsonPropertyName("simultaneousJobCount")]
    public int SimultaneousJobCount { get; set; } = Constants.DefaultSimultaneousJobCount;
}
