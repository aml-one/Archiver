namespace Archiver.Core.Models;

/// <summary>
/// Aggregated result from a single execution of a copy job.
/// </summary>
public sealed class JobRunResult
{
    public string JobName { get; init; } = string.Empty;
    public JobStatus Status { get; set; } = JobStatus.Idle;
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;

    public List<CopyFileResult> FileResults { get; set; } = new();

    public int TotalFiles => FileResults.Count;
    public int CopiedFiles => FileResults.Count(r => r.Status == CopyFileResultStatus.Success);
    public int SkippedFiles => FileResults.Count(r => r.Status == CopyFileResultStatus.Skipped);
    public int FailedFiles => FileResults.Count(r => r.Status == CopyFileResultStatus.Failed);
    public long TotalBytesCopied => FileResults.Sum(r => r.BytesCopied);
    public string? ErrorMessage { get; set; }
    public int CleanupFilesDeleted { get; set; }
}
