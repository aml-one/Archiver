namespace Archiver.Core.Models;

public enum CopyFileResultStatus
{
    Success,
    Skipped,
    Failed,
    Retried
}

/// <summary>
/// Result for a single file copy operation.
/// </summary>
public sealed class CopyFileResult
{
    public string FileName { get; init; } = string.Empty;
    public string RelativePath { get; init; } = string.Empty;
    public CopyFileResultStatus Status { get; set; }
    public long BytesCopied { get; set; }
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public bool IsSuccess => Status == CopyFileResultStatus.Success;
}
