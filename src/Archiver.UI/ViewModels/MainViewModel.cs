using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Archiver.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceProvider _serviceProvider;

    [ObservableProperty] private object? _currentPage;
    [ObservableProperty] private string _currentPageTitle = "Service Status";

    // Child ViewModels
    public ServiceStatusViewModel ServiceStatus { get; }
    public JobsPageViewModel JobsPage { get; }
    public LogsPageViewModel LogsPage { get; }

    // Dialog
    [ObservableProperty] private bool _isDialogOpen;
    [ObservableProperty] private JobEditViewModel? _dialogViewModel;

    public MainViewModel(
        IServiceProvider serviceProvider,
        ServiceStatusViewModel serviceStatus,
        JobsPageViewModel jobsPage,
        LogsPageViewModel logsPage)
    {
        _serviceProvider = serviceProvider;
        ServiceStatus = serviceStatus;
        JobsPage = jobsPage;
        LogsPage = logsPage;

        CurrentPage = serviceStatus;
    }

    [RelayCommand]
    public async Task ShowServiceStatus()
    {
        CurrentPageTitle = "Service Status";
        CurrentPage = ServiceStatus;
        await ServiceStatus.InitializeAsync();
    }

    [RelayCommand]
    public async Task ShowJobs()
    {
        CurrentPageTitle = "Copy Jobs";
        CurrentPage = JobsPage;
        JobsPage.LoadJobs();
    }

    [RelayCommand]
    public async Task ShowLogs()
    {
        CurrentPageTitle = "Logs";
        CurrentPage = LogsPage;
        await LogsPage.InitializeAsync();
    }

    [RelayCommand]
    public void AddNewJob()
    {
        var dialog = _serviceProvider.GetRequiredService<JobEditViewModel>();
        dialog.IsNew = true;
        DialogViewModel = dialog;
        IsDialogOpen = true;
    }

    [RelayCommand]
    public void EditJob(string jobName)
    {
        var configStore = _serviceProvider.GetRequiredService<Core.Services.IConfigurationStore>();
        var config = configStore.LoadConfig();
        var job = config.Jobs.FirstOrDefault(j =>
            string.Equals(j.Name, jobName, StringComparison.OrdinalIgnoreCase));

        if (job == null) return;

        var dialog = _serviceProvider.GetRequiredService<JobEditViewModel>();
        dialog.LoadFromJob(job);
        DialogViewModel = dialog;
        IsDialogOpen = true;
    }

    [RelayCommand]
    public void CloseDialog()
    {
        IsDialogOpen = false;
        DialogViewModel = null;
        JobsPage.LoadJobs(); // refresh
    }

    [RelayCommand]
    public void SaveDialog()
    {
        if (DialogViewModel == null) return;

        var error = DialogViewModel.Save();
        if (error == null)
        {
            CloseDialog();
        }
        else
        {
            // Error is surfaced through the dialog
        }
    }
}
