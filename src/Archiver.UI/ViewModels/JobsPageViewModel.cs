using System.Collections.ObjectModel;
using System.IO;
using Archiver.Core;
using Archiver.Core.Configuration;
using Archiver.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Archiver.UI.ViewModels;

public partial class JobsPageViewModel : ObservableObject
{
    private readonly IConfigurationStore _configStore;

    [ObservableProperty]
    private ObservableCollection<JobItemViewModel> _jobs = new();

    public JobsPageViewModel(IConfigurationStore configStore)
    {
        _configStore = configStore;
    }

    [RelayCommand]
    public void LoadJobs()
    {
        try
        {
            var config = _configStore.LoadConfig();

            // Read status.json for last run info
            var statuses = LoadStatuses();

            var items = config.Jobs.Select(job =>
            {
                statuses.TryGetValue(job.Name, out var status);
                return new JobItemViewModel
                {
                    Name = job.Name,
                    Description = job.Description ?? "",
                    Enabled = job.Enabled,
                    SourcePath = job.SourcePath,
                    NetworkDest = job.DestinationNetworkPath ?? "",
                    LocalDest = job.DestinationLocalPath ?? "",
                    ScheduleText = FormatSchedule(job.Schedule),
                    CleanupEnabled = job.Cleanup.Enabled,
                    LastRun = status?.LastRunUtc.ToLocalTime().ToString("g") ?? "Never",
                    LastStatus = status?.LastStatus ?? "Idle",
                    CopiedFiles = status?.CopiedFiles ?? 0,
                    FailedFiles = status?.FailedFiles ?? 0
                };
            }).ToList();

            Jobs = new ObservableCollection<JobItemViewModel>(items);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load jobs: {ex.Message}");
        }
    }

    [RelayCommand]
    public void TriggerRun(string jobName)
    {
        try
        {
            var triggerFile = Path.Combine(
                Constants.DefaultConfigDirectory,
                $"{jobName}{Constants.TriggerFileExtension}");
            File.WriteAllText(triggerFile, DateTime.UtcNow.ToString("O"));
            System.Diagnostics.Debug.WriteLine($"Trigger file written for job '{jobName}'");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to trigger job '{jobName}': {ex.Message}");
        }
    }

    private static Dictionary<string, StoredJobStatus> LoadStatuses()
    {
        try
        {
            var path = Constants.DefaultStatusFilePath;
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, StoredJobStatus>>(json)
                       ?? new Dictionary<string, StoredJobStatus>();
            }
        }
        catch { /* ignore */ }
        return new Dictionary<string, StoredJobStatus>();
    }

    private static string FormatSchedule(ScheduleOptions schedule)
    {
        return schedule.Type switch
        {
            ScheduleType.Daily => $"Daily at {string.Join(", ", schedule.Times ?? new List<string>())}",
            ScheduleType.Interval => $"Every {schedule.IntervalMinutes ?? 60} min",
            ScheduleType.Cron => schedule.CronExpression ?? "?",
            _ => "Unknown"
        };
    }

    private sealed class StoredJobStatus
    {
        public string JobName { get; set; } = string.Empty;
        public DateTime LastRunUtc { get; set; }
        public string LastStatus { get; set; } = string.Empty;
        public int CopiedFiles { get; set; }
        public int FailedFiles { get; set; }
    }
}

/// <summary>
/// Lightweight display item for a job in the list.
/// </summary>
public partial class JobItemViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private string _sourcePath = string.Empty;
    [ObservableProperty] private string _networkDest = string.Empty;
    [ObservableProperty] private string _localDest = string.Empty;
    [ObservableProperty] private string _scheduleText = string.Empty;
    [ObservableProperty] private bool _cleanupEnabled;
    [ObservableProperty] private string _lastRun = "Never";
    [ObservableProperty] private string _lastStatus = "Idle";
    [ObservableProperty] private int _copiedFiles;
    [ObservableProperty] private int _failedFiles;
}
