using System.Text.Json.Serialization;

namespace Archiver.Core.Configuration;

/// <summary>
/// Defines a single copy job: what to copy, where to, when, and cleanup policy.
/// </summary>
public sealed class CopyJobOptions
{
    /// <summary>Unique name for this job.</summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional human-readable description.</summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>Whether this job is currently active.</summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;

    /// <summary>Source folder path (absolute, local).</summary>
    [JsonPropertyName("sourcePath")]
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>Whether to recurse into subdirectories.</summary>
    [JsonPropertyName("includeSubdirectories")]
    public bool IncludeSubdirectories { get; set; } = true;

    /// <summary>Semicolon-separated file patterns (e.g. "*.bak;*.zip"). Null means all files.</summary>
    [JsonPropertyName("fileFilter")]
    public string? FileFilter { get; set; }

    /// <summary>Destination network share path (UNC). Optional if local path is set.</summary>
    [JsonPropertyName("destinationNetworkPath")]
    public string? DestinationNetworkPath { get; set; }

    /// <summary>Destination local/external drive path. Optional. Can be used alongside or instead of network path.</summary>
    [JsonPropertyName("destinationLocalPath")]
    public string? DestinationLocalPath { get; set; }

    /// <summary>
    /// Name of a credential entry to use for the network destination.
    /// If null, machine account is used (domain environment).
    /// </summary>
    [JsonPropertyName("credentialName")]
    public string? CredentialName { get; set; }

    /// <summary>Schedule configuration.</summary>
    [JsonPropertyName("schedule")]
    public ScheduleOptions Schedule { get; set; } = new();

    /// <summary>Cleanup/trimming configuration.</summary>
    [JsonPropertyName("cleanup")]
    public CleanupOptions Cleanup { get; set; } = new();

    /// <summary>Validate that at least one destination is configured.</summary>
    public bool HasValidDestinations()
    {
        return !string.IsNullOrWhiteSpace(DestinationNetworkPath)
            || !string.IsNullOrWhiteSpace(DestinationLocalPath);
    }

    /// <summary>Parse the file filter into a list of patterns.</summary>
    public List<string> GetFilePatterns()
    {
        if (string.IsNullOrWhiteSpace(FileFilter))
            return new List<string> { "*" };

        return FileFilter.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                         .Select(p => p.StartsWith('*') ? p : "*" + p)
                         .ToList();
    }
}
