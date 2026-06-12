using System.Collections.ObjectModel;
using System.Windows.Input;
using Archiver.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Archiver.UI.ViewModels;

public partial class ServiceStatusViewModel : ObservableObject
{
    private readonly IServiceController _serviceController;
    private readonly ILogReader _logReader;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "Checking...";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isElevated;

    [ObservableProperty]
    private ObservableCollection<LogEntryModel> _recentLogs = new();

    public ServiceStatusViewModel(IServiceController serviceController, ILogReader logReader)
    {
        _serviceController = serviceController;
        _logReader = logReader;
        IsElevated = serviceController.IsElevated;
    }

    [RelayCommand]
    public void RefreshStatus()
    {
        var status = _serviceController.GetStatus();
        IsInstalled = status.IsInstalled;
        IsRunning = status.IsRunning;
        StatusText = status.StatusText;
        ErrorMessage = status.ErrorMessage;
    }

    [RelayCommand]
    public async Task StartService()
    {
        if (!IsElevated) return;
        var success = await _serviceController.StartAsync();
        if (success)
        {
            await Task.Delay(2000); // wait for service to start
            RefreshStatus();
        }
    }

    [RelayCommand]
    public async Task StopService()
    {
        if (!IsElevated) return;
        var success = await _serviceController.StopAsync();
        if (success)
        {
            await Task.Delay(2000);
            RefreshStatus();
        }
    }

    [RelayCommand]
    public async Task LoadRecentLogs()
    {
        var logs = await _logReader.ReadLogsAsync(maxLines: 20);
        RecentLogs = new ObservableCollection<LogEntryModel>(logs);
    }

    public async Task InitializeAsync()
    {
        RefreshStatus();
        await LoadRecentLogs();
    }
}
