using System.Collections.Concurrent;
using System.Text.Json;
using Archiver.Core;
using Archiver.Core.Configuration;
using Archiver.Core.Models;
using Archiver.Core.Services;
using Cronos;

namespace Archiver.Service.Scheduling;

/// <summary>
/// Background service that manages all copy job timers, executes jobs,
/// reacts to config changes, handles "Run Now" trigger files, and
/// writes status after each run.
/// </summary>
public sealed class JobScheduler : BackgroundService
{
    private readonly IConfigurationStore _configStore;
    private readonly INetworkShareAuthenticator _networkAuth;
    private readonly ICredentialProtector _credentialProtector;
    private readonly IFileCopier _fileCopier;
    private readonly ISystemClock _clock;
    private readonly ILogger<JobScheduler> _logger;
    private readonly IHostApplicationLifetime _appLifetime;

    private readonly ConcurrentDictionary<string, Timer> _timers = new();
    private readonly ConcurrentDictionary<string, bool> _runningJobs = new();
    private FileSystemWatcher? _triggerWatcher;
    private ArchiverOptions _currentConfig = new();
    private int _activeJobCount;

    public JobScheduler(
        IConfigurationStore configStore,
        INetworkShareAuthenticator networkAuth,
        ICredentialProtector credentialProtector,
        IFileCopier fileCopier,
        ISystemClock clock,
        ILogger<JobScheduler> logger,
        IHostApplicationLifetime appLifetime)
    {
        _configStore = configStore;
        _networkAuth = networkAuth;
        _credentialProtector = credentialProtector;
        _fileCopier = fileCopier;
        _clock = clock;
        _logger = logger;
        _appLifetime = appLifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Archiver JobScheduler starting");

        _currentConfig = _configStore.LoadConfig();

        // Subscribe to config changes
        _configStore.ConfigurationChanged += OnConfigurationChanged;

        // Start trigger file watcher for "Run Now"
        StartTriggerWatcher();

        // Schedule all enabled jobs
        ScheduleAllJobs();

        _logger.LogInformation("Archiver JobScheduler started. {JobCount} jobs configured, {EnabledCount} enabled.",
            _currentConfig.Jobs.Count, _currentConfig.Jobs.Count(j => j.Enabled));

        // Keep running until stopped
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Archiver JobScheduler shutting down");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Archiver JobScheduler stopping — waiting for running jobs to complete");

        // Dispose all timers
        foreach (var (name, timer) in _timers)
        {
            await timer.DisposeAsync();
        }
        _timers.Clear();

        _configStore.ConfigurationChanged -= OnConfigurationChanged;
        _triggerWatcher?.Dispose();

        // Wait for active jobs to finish (max 30 seconds)
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (_activeJobCount > 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(500, cancellationToken);
        }

        _logger.LogInformation("Archiver JobScheduler stopped. {ActiveJobs} jobs were still running.", _activeJobCount);
    }

    // ── Scheduling ──────────────────────────────────────────────────

    private void ScheduleAllJobs()
    {
        foreach (var (name, timer) in _timers)
        {
            timer.Dispose();
        }
        _timers.Clear();

        foreach (var job in _currentConfig.Jobs)
        {
            if (!job.Enabled)
            {
                _logger.LogDebug("Job '{JobName}' is disabled, skipping schedule", job.Name);
                continue;
            }

            ScheduleJob(job);
        }
    }

    private void ScheduleJob(CopyJobOptions job)
    {
        var dueTime = CalculateNextDueTime(job);
        var period = GetPeriod(job);

        _logger.LogInformation("Scheduling job '{JobName}': next run at {NextRun:O}, period {Period}",
            job.Name, DateTime.UtcNow + dueTime,
            period == Timeout.InfiniteTimeSpan ? "computed per-run" : period.ToString());

        var timer = new Timer(
            callback: _ => OnTimerElapsed(job),
            state: null,
            dueTime: dueTime,
            period: period);

        _timers[job.Name] = timer;
    }

    private TimeSpan CalculateNextDueTime(CopyJobOptions job)
    {
        var now = _clock.Now;

        return job.Schedule.Type switch
        {
            ScheduleType.Daily => CalculateNextDailyTime(job, now),
            ScheduleType.Interval => TimeSpan.FromMinutes(job.Schedule.IntervalMinutes ?? 60),
            ScheduleType.Cron => CalculateNextCronTime(job, now),
            _ => TimeSpan.FromMinutes(60)
        };
    }

    private TimeSpan CalculateNextDailyTime(CopyJobOptions job, DateTime now)
    {
        if (job.Schedule.Times == null || job.Schedule.Times.Count == 0)
            return TimeSpan.FromHours(1);

        var todayTimes = job.Schedule.Times
            .Select(ScheduleOptions.ParseTime)
            .Select(ts => now.Date + ts)
            .OrderBy(t => t)
            .ToList();

        // Find next time today
        var next = todayTimes.FirstOrDefault(t => t > now);

        if (next == default)
        {
            // All times passed today — schedule for first time tomorrow
            next = now.Date.AddDays(1) + ScheduleOptions.ParseTime(job.Schedule.Times.OrderBy(ScheduleOptions.ParseTime).First());
        }

        return next - now;
    }

    private TimeSpan CalculateNextCronTime(CopyJobOptions job, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(job.Schedule.CronExpression))
            return TimeSpan.FromHours(1);

        try
        {
            var expression = CronExpression.Parse(job.Schedule.CronExpression, CronFormat.Standard);

            var next = expression.GetNextOccurrence(now, TimeZoneInfo.Local);
            if (next.HasValue)
                return next.Value - now;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Invalid cron expression for job '{JobName}': {Expression}",
                job.Name, job.Schedule.CronExpression);
        }

        return TimeSpan.FromHours(1);
    }

    private static TimeSpan GetPeriod(CopyJobOptions job)
    {
        return job.Schedule.Type switch
        {
            ScheduleType.Interval => TimeSpan.FromMinutes(job.Schedule.IntervalMinutes ?? 60),
            ScheduleType.Cron => Timeout.InfiniteTimeSpan, // re-schedule after each run
            ScheduleType.Daily => Timeout.InfiniteTimeSpan, // re-schedule after each run
            _ => Timeout.InfiniteTimeSpan
        };
    }

    // ── Execution ───────────────────────────────────────────────────

    private async void OnTimerElapsed(CopyJobOptions job)
    {
        // Re-read job from current config (may have changed)
        var currentJob = _currentConfig.Jobs.FirstOrDefault(j =>
            string.Equals(j.Name, job.Name, StringComparison.OrdinalIgnoreCase));

        if (currentJob == null || !currentJob.Enabled)
        {
            _logger.LogDebug("Job '{JobName}' no longer enabled or removed — skipping", job.Name);
            return;
        }

        // Prevent overlapping runs
        if (_runningJobs.TryGetValue(job.Name, out var running) && running)
        {
            _logger.LogWarning("Job '{JobName}' is already running — skipping this cycle", job.Name);
            return;
        }

        // Respect simultaneous job limit
        if (_activeJobCount >= (_currentConfig.Service?.SimultaneousJobCount ?? Constants.DefaultSimultaneousJobCount))
        {
            _logger.LogWarning("Concurrent job limit reached — skipping '{JobName}' this cycle", job.Name);
            return;
        }

        _runningJobs[job.Name] = true;
        Interlocked.Increment(ref _activeJobCount);

        try
        {
            await ExecuteJobAsync(currentJob);
        }
        finally
        {
            _runningJobs[job.Name] = false;
            Interlocked.Decrement(ref _activeJobCount);
        }
    }

    public async Task ExecuteJobAsync(CopyJobOptions job)
    {
        var jobResult = new JobRunResult
        {
            JobName = job.Name,
            StartedAt = _clock.UtcNow,
            Status = JobStatus.Running
        };

        _logger.LogInformation("[{JobName}] Starting execution cycle", job.Name);

        try
        {
            // Validate
            if (!job.HasValidDestinations())
            {
                jobResult.Status = JobStatus.Error;
                jobResult.ErrorMessage = "At least one destination must be configured";
                _logger.LogError("[{JobName}] No valid destinations configured", job.Name);
                return;
            }

            if (!Directory.Exists(job.SourcePath))
            {
                jobResult.Status = JobStatus.Error;
                jobResult.ErrorMessage = $"Source path does not exist: {job.SourcePath}";
                _logger.LogError("[{JobName}] Source path does not exist: {Path}", job.Name, job.SourcePath);
                return;
            }

            // Authenticate network share if needed
            string? networkDest = job.DestinationNetworkPath;
            if (!string.IsNullOrWhiteSpace(networkDest))
            {
                await AuthenticateNetworkShare(job, networkDest);
            }

            // Copy files
            jobResult = await _fileCopier.CopyFilesAsync(
                job,
                networkDest,
                job.DestinationLocalPath,
                CancellationToken.None);

            // Cleanup source if enabled
            if (job.Cleanup.Enabled)
            {
                var deleted = await _fileCopier.CleanupSourceAsync(job, CancellationToken.None);
                jobResult.CleanupFilesDeleted = deleted;
            }

            // Write status
            WriteStatusFile(jobResult);
        }
        catch (Exception ex)
        {
            jobResult.Status = JobStatus.Error;
            jobResult.ErrorMessage = ex.Message;
            _logger.LogError(ex, "[{JobName}] Fatal error during execution", job.Name);
        }
        finally
        {
            jobResult.CompletedAt = _clock.UtcNow;

            _logger.LogInformation(
                "[{JobName}] Execution complete. Status: {Status}, Copied: {Copied}, Failed: {Failed}, Cleaned: {Cleaned}, Duration: {Duration:F1}s",
                job.Name, jobResult.Status, jobResult.CopiedFiles, jobResult.FailedFiles,
                jobResult.CleanupFilesDeleted, jobResult.Duration.TotalSeconds);
        }
    }

    private async Task AuthenticateNetworkShare(CopyJobOptions job, string networkPath)
    {
        // Determine which credential to use
        var credential = _currentConfig.GetCredential(job.CredentialName)
                      ?? _currentConfig.FindCredentialForShare(networkPath);

        if (credential == null)
        {
            _logger.LogDebug("[{JobName}] No credentials configured for {Share} — using machine account",
                job.Name, networkPath);
            return;
        }

        var password = _credentialProtector.Unprotect(credential.PasswordEncrypted ?? string.Empty);
        if (string.IsNullOrEmpty(password))
        {
            _logger.LogWarning("[{JobName}] Credential '{CredName}' password is empty or could not be decrypted",
                job.Name, credential.Name);
            return;
        }

        var connected = _networkAuth.Connect(networkPath, credential, password);
        if (!connected)
        {
            _logger.LogWarning("[{JobName}] Failed to authenticate to {Share} with credential '{CredName}'",
                job.Name, networkPath, credential.Name);
        }
    }

    // ── Status File ─────────────────────────────────────────────────

    private void WriteStatusFile(JobRunResult result)
    {
        try
        {
            var statusPath = Constants.DefaultStatusFilePath;

            // Read existing status entries
            Dictionary<string, StoredJobStatus> statuses;
            if (File.Exists(statusPath))
            {
                var existing = File.ReadAllText(statusPath);
                statuses = JsonSerializer.Deserialize<Dictionary<string, StoredJobStatus>>(existing)
                           ?? new Dictionary<string, StoredJobStatus>();
            }
            else
            {
                statuses = new Dictionary<string, StoredJobStatus>();
            }

            // Update this job's entry
            statuses[result.JobName] = new StoredJobStatus
            {
                JobName = result.JobName,
                LastRunUtc = result.StartedAt,
                LastStatus = result.Status.ToString(),
                CopiedFiles = result.CopiedFiles,
                FailedFiles = result.FailedFiles,
                CleanedFiles = result.CleanupFilesDeleted,
                DurationSeconds = result.Duration.TotalSeconds,
                ErrorMessage = result.ErrorMessage
            };

            var json = JsonSerializer.Serialize(statuses, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(statusPath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to write status file: {Message}", ex.Message);
        }
    }

    // ── Trigger File Watcher (Run Now) ──────────────────────────────

    private void StartTriggerWatcher()
    {
        _triggerWatcher = new FileSystemWatcher(
            Constants.DefaultConfigDirectory,
            $"*{Constants.TriggerFileExtension}")
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true
        };

        _triggerWatcher.Created += OnTriggerFileCreated;
    }

    private void OnTriggerFileCreated(object sender, FileSystemEventArgs e)
    {
        try
        {
            // Extract job name: "MyJob.trigger" -> "MyJob"
            var triggerName = Path.GetFileNameWithoutExtension(e.Name);
            var job = _currentConfig.Jobs.FirstOrDefault(j =>
                string.Equals(j.Name, triggerName, StringComparison.OrdinalIgnoreCase));

            if (job != null && job.Enabled)
            {
                _logger.LogInformation("Trigger file detected for job '{JobName}' — running now", job.Name);
                Task.Run(() => OnTimerElapsed(job));
            }

            // Clean up trigger file
            try { File.Delete(e.FullPath); } catch { /* ignore */ }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling trigger file: {Path}", e.FullPath);
        }
    }

    private void OnConfigurationChanged(object? sender, ArchiverOptions newConfig)
    {
        _logger.LogInformation("Configuration reloaded — rescheduling all jobs");
        _currentConfig = newConfig;
        ScheduleAllJobs();
    }
}

/// <summary>
/// Lightweight status entry written to status.json after each job run.
/// </summary>
internal sealed class StoredJobStatus
{
    public string JobName { get; set; } = string.Empty;
    public DateTime LastRunUtc { get; set; }
    public string LastStatus { get; set; } = string.Empty;
    public int CopiedFiles { get; set; }
    public int FailedFiles { get; set; }
    public int CleanedFiles { get; set; }
    public double DurationSeconds { get; set; }
    public string? ErrorMessage { get; set; }
}
