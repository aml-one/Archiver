using System.Collections.ObjectModel;
using Archiver.Core.Configuration;
using Archiver.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace Archiver.UI.ViewModels;

public partial class JobEditViewModel : ObservableObject
{
    private readonly IConfigurationStore _configStore;
    private readonly ICredentialProtector _credentialProtector;
    private readonly INetworkShareAuthenticator? _networkAuth;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _description;
    [ObservableProperty] private bool _enabled = true;
    [ObservableProperty] private string _sourcePath = string.Empty;
    [ObservableProperty] private bool _includeSubdirectories = true;
    [ObservableProperty] private string? _fileFilter;
    [ObservableProperty] private string? _destinationNetworkPath;
    [ObservableProperty] private string? _destinationLocalPath;
    [ObservableProperty] private string? _credentialName;
    [ObservableProperty] private ScheduleType _scheduleType = ScheduleType.Daily;
    [ObservableProperty] private string _dailyTimes = "02:00; 14:00";
    [ObservableProperty] private int _intervalMinutes = 60;
    [ObservableProperty] private string? _cronExpression = "0 */6 * * *";
    [ObservableProperty] private bool _cleanupEnabled;
    [ObservableProperty] private int _retentionDays = 60;
    [ObservableProperty] private int _minFreeDiskPercent = 5;
    [ObservableProperty] private bool _removeEmptyDirectories;

    // Credential editor
    [ObservableProperty] private string _credentialDomain = string.Empty;
    [ObservableProperty] private string _credentialUsername = string.Empty;
    [ObservableProperty] private string _credentialPassword = string.Empty;
    [ObservableProperty] private CredentialMethod _credentialMethod = CredentialMethod.Win32;
    [ObservableProperty] private string? _connectionTestResult;

    [ObservableProperty] private ObservableCollection<string> _availableCredentials = new();
    [ObservableProperty] private ObservableCollection<ScheduleType> _scheduleTypes = new(
        Enum.GetValues<ScheduleType>());

    public bool IsNew { get; set; } = true;
    private string _originalName = string.Empty;

    public JobEditViewModel(
        IConfigurationStore configStore,
        ICredentialProtector credentialProtector,
        INetworkShareAuthenticator? networkAuth = null)
    {
        _configStore = configStore;
        _credentialProtector = credentialProtector;
        _networkAuth = networkAuth;
        LoadCredentials();
    }

    /// <summary>Load from an existing job for editing.</summary>
    public void LoadFromJob(CopyJobOptions job)
    {
        IsNew = false;
        _originalName = job.Name;
        Name = job.Name;
        Description = job.Description;
        Enabled = job.Enabled;
        SourcePath = job.SourcePath;
        IncludeSubdirectories = job.IncludeSubdirectories;
        FileFilter = job.FileFilter;
        DestinationNetworkPath = job.DestinationNetworkPath;
        DestinationLocalPath = job.DestinationLocalPath;
        CredentialName = job.CredentialName;
        ScheduleType = job.Schedule.Type;
        DailyTimes = job.Schedule.Times != null ? string.Join("; ", job.Schedule.Times) : "";
        IntervalMinutes = job.Schedule.IntervalMinutes ?? 60;
        CronExpression = job.Schedule.CronExpression ?? "0 */6 * * *";
        CleanupEnabled = job.Cleanup.Enabled;
        RetentionDays = job.Cleanup.RetentionDays;
        MinFreeDiskPercent = job.Cleanup.MinFreeDiskPercent;
        RemoveEmptyDirectories = job.Cleanup.RemoveEmptyDirectories;
    }

    /// <summary>Build and save the job to config. Returns error message, or null on success.</summary>
    public string? Save()
    {
        if (string.IsNullOrWhiteSpace(Name))
            return "Name is required.";
        if (string.IsNullOrWhiteSpace(SourcePath))
            return "Source path is required.";
        if (string.IsNullOrWhiteSpace(DestinationNetworkPath) && string.IsNullOrWhiteSpace(DestinationLocalPath))
            return "At least one destination must be set.";

        var job = new CopyJobOptions
        {
            Name = Name.Trim(),
            Description = Description?.Trim(),
            Enabled = Enabled,
            SourcePath = SourcePath.Trim(),
            IncludeSubdirectories = IncludeSubdirectories,
            FileFilter = FileFilter?.Trim(),
            DestinationNetworkPath = DestinationNetworkPath?.Trim(),
            DestinationLocalPath = DestinationLocalPath?.Trim(),
            CredentialName = CredentialName?.Trim(),
            Schedule = new ScheduleOptions
            {
                Type = ScheduleType,
                Times = ScheduleType == ScheduleType.Daily
                    ? DailyTimes.Split(';', StringSplitOptions.RemoveEmptyEntries)
                        .Select(t => t.Trim()).ToList()
                    : null,
                IntervalMinutes = ScheduleType == ScheduleType.Interval ? IntervalMinutes : null,
                CronExpression = ScheduleType == ScheduleType.Cron ? CronExpression : null
            },
            Cleanup = new CleanupOptions
            {
                Enabled = CleanupEnabled,
                RetentionDays = RetentionDays,
                MinFreeDiskPercent = MinFreeDiskPercent,
                RemoveEmptyDirectories = RemoveEmptyDirectories
            }
        };

        var config = _configStore.LoadConfig();

        // Remove old entry if renaming
        if (!IsNew && !string.Equals(_originalName, job.Name, StringComparison.OrdinalIgnoreCase))
        {
            config.Jobs.RemoveAll(j => string.Equals(j.Name, _originalName, StringComparison.OrdinalIgnoreCase));
        }

        // Upsert
        var existing = config.Jobs.FindIndex(j =>
            string.Equals(j.Name, job.Name, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
            config.Jobs[existing] = job;
        else
            config.Jobs.Add(job);

        // Save credential if provided
        if (!string.IsNullOrWhiteSpace(CredentialUsername) && !string.IsNullOrWhiteSpace(CredentialPassword))
        {
            var credName = string.IsNullOrWhiteSpace(CredentialName) ? job.CredentialName : CredentialName;
            if (!string.IsNullOrWhiteSpace(credName))
            {
                var encrypted = _credentialProtector.Protect(CredentialPassword);
                var cred = config.Credentials?.FirstOrDefault(c =>
                    string.Equals(c.Name, credName, StringComparison.OrdinalIgnoreCase));

                if (cred != null)
                {
                    cred.Username = CredentialUsername;
                    cred.Domain = CredentialDomain;
                    cred.PasswordEncrypted = encrypted;
                    cred.Method = CredentialMethod;
                }
                else
                {
                    config.Credentials ??= new List<CredentialOptions>();
                    config.Credentials.Add(new CredentialOptions
                    {
                        Name = credName,
                        Username = CredentialUsername,
                        Domain = CredentialDomain,
                        PasswordEncrypted = encrypted,
                        Method = CredentialMethod,
                        TargetShares = DestinationNetworkPath is not null
                            ? new List<string> { DestinationNetworkPath }
                            : null
                    });
                }
            }
        }

        _configStore.SaveConfig(config);
        LoadCredentials();
        _originalName = job.Name;
        IsNew = false;
        return null; // success
    }

    [RelayCommand]
    public void BrowseSource()
    {
        var dialog = new OpenFolderDialog { Title = "Select Source Folder" };
        if (dialog.ShowDialog() == true)
            SourcePath = dialog.FolderName;
    }

    [RelayCommand]
    public void BrowseLocalDest()
    {
        var dialog = new OpenFolderDialog { Title = "Select Local/USB Destination" };
        if (dialog.ShowDialog() == true)
            DestinationLocalPath = dialog.FolderName;
    }

    [RelayCommand]
    public async Task TestConnection()
    {
        if (_networkAuth == null)
        {
            ConnectionTestResult = "Connection testing not available.";
            return;
        }

        if (string.IsNullOrWhiteSpace(DestinationNetworkPath))
        {
            ConnectionTestResult = "No network path specified.";
            return;
        }

        if (string.IsNullOrWhiteSpace(CredentialUsername) || string.IsNullOrWhiteSpace(CredentialPassword))
        {
            ConnectionTestResult = "No credentials provided. Will use machine account.";
            return;
        }

        var credential = new CredentialOptions
        {
            Name = "TestConnection",
            Username = CredentialUsername,
            Domain = CredentialDomain,
            Method = CredentialMethod
        };

        ConnectionTestResult = "Testing...";
        string? capturedError = null;
        bool capturedSuccess = false;
        await Task.Run(() =>
        {
            capturedSuccess = _networkAuth.TryConnect(DestinationNetworkPath, credential, CredentialPassword, out capturedError);
        });

        ConnectionTestResult = capturedSuccess ? "Connection successful!" : $"Connection failed: {capturedError}";
    }

    private void LoadCredentials()
    {
        var config = _configStore.LoadConfig();
        var names = config.Credentials?.Select(c => c.Name).ToList() ?? new List<string>();
        names.Insert(0, ""); // empty option
        AvailableCredentials = new ObservableCollection<string>(names);
    }

    [RelayCommand]
    public void Delete()
    {
        if (IsNew) return;

        var config = _configStore.LoadConfig();
        config.Jobs.RemoveAll(j =>
            string.Equals(j.Name, _originalName, StringComparison.OrdinalIgnoreCase));
        _configStore.SaveConfig(config);
    }
}
