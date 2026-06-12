using Archiver.Core.Configuration;
using Archiver.Core.Models;
using Microsoft.Extensions.Logging;

namespace Archiver.Core.Services;

/// <summary>
/// Core file copy engine with retry resilience, file lock handling, and copy verification.
/// </summary>
public sealed class FileCopier : IFileCopier
{
    private readonly ILogger<FileCopier> _logger;
    private readonly ISystemClock _clock;

    public FileCopier(ILogger<FileCopier> logger, ISystemClock clock)
    {
        _logger = logger;
        _clock = clock;
    }

    public async Task<JobRunResult> CopyFilesAsync(
        CopyJobOptions job,
        string? networkDestination,
        string? localDestination,
        CancellationToken cancellationToken = default)
    {
        var result = new JobRunResult
        {
            JobName = job.Name,
            StartedAt = _clock.UtcNow,
            Status = JobStatus.Running
        };

        try
        {
            var sourcePath = job.SourcePath;
            if (!Directory.Exists(sourcePath))
            {
                result.Status = JobStatus.Error;
                result.ErrorMessage = $"Source path not found: {sourcePath}";
                _logger.LogError("[{JobName}] Source path not found: {Path}", job.Name, sourcePath);
                return result;
            }

            var searchOption = job.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var patterns = job.GetFilePatterns();

            var files = EnumerateFiles(sourcePath, patterns, searchOption);
            _logger.LogInformation("[{JobName}] Found {FileCount} files to copy from {Source}",
                job.Name, files.Count, sourcePath);

            if (files.Count == 0)
            {
                result.Status = JobStatus.Completed;
                result.CompletedAt = _clock.UtcNow;
                return result;
            }

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var relativePath = Path.GetRelativePath(sourcePath, filePath);
                var fileResult = new CopyFileResult
                {
                    FileName = Path.GetFileName(filePath),
                    RelativePath = relativePath,
                    Timestamp = _clock.UtcNow
                };

                try
                {
                    // Copy to network destination
                    if (!string.IsNullOrWhiteSpace(networkDestination))
                    {
                        await CopyFileWithRetryAsync(
                            filePath, networkDestination, relativePath, fileResult, cancellationToken);
                    }

                    // Copy to local destination
                    if (!string.IsNullOrWhiteSpace(localDestination))
                    {
                        await CopyFileWithRetryAsync(
                            filePath, localDestination, relativePath, fileResult, cancellationToken);
                    }

                    if (fileResult.IsSuccess)
                        fileResult.Status = CopyFileResultStatus.Success;
                }
                catch (OperationCanceledException)
                {
                    fileResult.Status = CopyFileResultStatus.Skipped;
                    fileResult.ErrorMessage = "Operation cancelled";
                    result.Status = JobStatus.Error;
                    result.ErrorMessage = "Job was cancelled mid-execution";
                }
                catch (Exception ex)
                {
                    fileResult.Status = CopyFileResultStatus.Failed;
                    fileResult.ErrorMessage = ex.Message;
                    _logger.LogError(ex, "[{JobName}] Unexpected error copying {File}", job.Name, filePath);
                }

                result.FileResults.Add(fileResult);
            }

            result.CompletedAt = _clock.UtcNow;

            if (result.Status != JobStatus.Error)
                result.Status = JobStatus.Completed;

            _logger.LogInformation(
                "[{JobName}] Copy completed: {Copied}/{Total} files, {Skipped} skipped, {Failed} failed ({Duration:F1}s)",
                job.Name, result.CopiedFiles, result.TotalFiles,
                result.SkippedFiles, result.FailedFiles, result.Duration.TotalSeconds);
        }
        catch (OperationCanceledException)
        {
            result.Status = JobStatus.Error;
            result.ErrorMessage = "Job was cancelled";
            result.CompletedAt = _clock.UtcNow;
        }
        catch (Exception ex)
        {
            result.Status = JobStatus.Error;
            result.ErrorMessage = ex.Message;
            result.CompletedAt = _clock.UtcNow;
            _logger.LogError(ex, "[{JobName}] Fatal error during copy", job.Name);
        }

        return result;
    }

    /// <summary>
    /// Copy a single file with exponential-backoff retry for transient network and IO errors.
    /// </summary>
    private async Task CopyFileWithRetryAsync(
        string sourceFile,
        string destinationRoot,
        string relativePath,
        CopyFileResult fileResult,
        CancellationToken cancellationToken)
    {
        var destFile = Path.Combine(destinationRoot, relativePath);
        var destDir = Path.GetDirectoryName(destFile)!;
        Directory.CreateDirectory(destDir);

        var sourceFileInfo = new FileInfo(sourceFile);
        int maxRetries = 3;
        var delay = TimeSpan.FromSeconds(10);

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await CopyFileAsync(sourceFile, destFile, sourceFileInfo, fileResult, cancellationToken);
                return; // success
            }
            catch (Exception ex) when (attempt < maxRetries && IsTransient(ex))
            {
                fileResult.RetryCount = attempt + 1;
                _logger.LogWarning(
                    "[Retry {Attempt}/{Max}] Transient error copying {File}: {Message}. Waiting {Delay}s...",
                    attempt + 1, maxRetries, Path.GetFileName(sourceFile), ex.Message, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 2); // exponential backoff
            }
        }
    }

    private static bool IsTransient(Exception ex)
    {
        return ex is IOException ||
               ex is UnauthorizedAccessException ||
               ex is System.Net.Sockets.SocketException ||
               ex is System.Net.Http.HttpRequestException;
    }

    private async Task CopyFileAsync(
        string sourceFile,
        string destFile,
        FileInfo sourceFileInfo,
        CopyFileResult fileResult,
        CancellationToken cancellationToken)
    {
        // Try to open source file (handles locks)
        await using var sourceStream = await OpenFileWithRetryAsync(sourceFile, cancellationToken);

        // Write to destination
        await using var destStream = new FileStream(
            destFile, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 81920, useAsync: true);

        await sourceStream.CopyToAsync(destStream, cancellationToken);
        await destStream.FlushAsync(cancellationToken);

        // Verify copy by comparing file sizes
        destStream.Close();
        var destFileInfo = new FileInfo(destFile);
        if (destFileInfo.Length != sourceFileInfo.Length)
        {
            File.Delete(destFile);
            throw new IOException(
                $"Size mismatch for {Path.GetFileName(sourceFile)}: " +
                $"source={sourceFileInfo.Length}, dest={destFileInfo.Length}");
        }

        // Preserve the last write time
        File.SetLastWriteTimeUtc(destFile, sourceFileInfo.LastWriteTimeUtc);

        fileResult.BytesCopied += sourceFileInfo.Length;
    }

    private static async Task<FileStream> OpenFileWithRetryAsync(string filePath, CancellationToken cancellationToken)
    {
        var maxAttempts = 6; // ~30 seconds total
        var delay = TimeSpan.FromSeconds(5);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return new FileStream(
                    filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 81920, useAsync: true);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                await Task.Delay(delay, cancellationToken);
                delay = TimeSpan.FromSeconds(delay.TotalSeconds * 1.5);
            }
        }

        return new FileStream(
            filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 81920, useAsync: true);
    }

    private List<string> EnumerateFiles(string sourcePath, List<string> patterns, SearchOption searchOption)
    {
        var files = new List<string>();

        foreach (var pattern in patterns)
        {
            try
            {
                var matched = Directory.EnumerateFiles(sourcePath, pattern, searchOption);
                files.AddRange(matched);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning("Access denied enumerating {Path} with pattern {Pattern}: {Message}",
                    sourcePath, pattern, ex.Message);
            }
            catch (DirectoryNotFoundException)
            {
                _logger.LogDebug("Directory not found during enumeration of {Path} with pattern {Pattern}",
                    sourcePath, pattern);
            }
        }

        return files.Distinct().OrderBy(f => f).ToList();
    }

    public async Task<int> CleanupSourceAsync(
        CopyJobOptions job,
        CancellationToken cancellationToken = default)
    {
        if (!job.Cleanup.Enabled)
            return 0;

        var deletedCount = 0;

        try
        {
            var sourcePath = job.SourcePath;
            if (!Directory.Exists(sourcePath))
                return 0;

            // Check minimum free disk percentage
            if (job.Cleanup.MinFreeDiskPercent > 0)
            {
                var root = Path.GetPathRoot(sourcePath);
                if (root != null)
                {
                    var driveInfo = new DriveInfo(root);
                    var freePercent = (double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize * 100.0;
                    if (freePercent < job.Cleanup.MinFreeDiskPercent)
                    {
                        _logger.LogWarning(
                            "[{JobName}] Skipping cleanup: free disk space ({FreePercent:F1}%) below minimum ({MinPercent}%)",
                            job.Name, freePercent, job.Cleanup.MinFreeDiskPercent);
                        return 0;
                    }
                }
            }

            var cutoff = _clock.UtcNow.AddDays(-job.Cleanup.RetentionDays);
            var searchOption = job.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var patterns = job.GetFilePatterns();

            var files = EnumerateFiles(sourcePath, patterns, searchOption);

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.LastWriteTimeUtc < cutoff)
                    {
                        fileInfo.Delete();
                        deletedCount++;
                        _logger.LogDebug("[{JobName}] Cleaned up: {File} (last modified: {Date:O})",
                            job.Name, Path.GetRelativePath(sourcePath, filePath), fileInfo.LastWriteTimeUtc);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("[{JobName}] Failed to delete {File}: {Message}",
                        job.Name, Path.GetRelativePath(sourcePath, filePath), ex.Message);
                }
            }

            // Remove empty directories if configured
            if (job.Cleanup.RemoveEmptyDirectories && job.IncludeSubdirectories)
            {
                RemoveEmptyDirectories(sourcePath);
            }

            if (deletedCount > 0)
            {
                _logger.LogInformation("[{JobName}] Cleanup completed: {Count} files deleted (retention: {Days} days)",
                    job.Name, deletedCount, job.Cleanup.RetentionDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{JobName}] Error during cleanup", job.Name);
        }

        return deletedCount;
    }

    private static void RemoveEmptyDirectories(string rootPath)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(rootPath))
            {
                RemoveEmptyDirectories(dir);
                if (!Directory.EnumerateFileSystemEntries(dir).Any())
                {
                    Directory.Delete(dir, false);
                }
            }
        }
        catch
        {
            // Skip directories we can't enumerate or delete
        }
    }
}
