using Archiver.Core.Configuration;
using Archiver.Core.Models;

namespace Archiver.Core.Services;

/// <summary>
/// Handles file enumeration and copy operations with retry and verification.
/// </summary>
public interface IFileCopier
{
    /// <summary>
    /// Copy all matching files from the source to the specified destinations.
    /// </summary>
    Task<JobRunResult> CopyFilesAsync(
        CopyJobOptions job,
        string? networkDestination,
        string? localDestination,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up source files older than the retention period.
    /// </summary>
    Task<int> CleanupSourceAsync(
        CopyJobOptions job,
        CancellationToken cancellationToken = default);
}
