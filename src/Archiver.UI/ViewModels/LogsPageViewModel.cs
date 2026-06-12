using System.Collections.ObjectModel;
using Archiver.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Archiver.UI.ViewModels;

public partial class LogsPageViewModel : ObservableObject
{
    private readonly ILogReader _logReader;

    [ObservableProperty] private ObservableCollection<LogEntryModel> _entries = new();
    [ObservableProperty] private ObservableCollection<string> _availableDates = new();
    [ObservableProperty] private ObservableCollection<string> _logLevels = new(
        new[] { "All", "Information", "Warning", "Error", "Critical" });

    [ObservableProperty] private string? _selectedDate;
    [ObservableProperty] private string _selectedLevel = "All";
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private int _maxLines = 500;

    public LogsPageViewModel(ILogReader logReader)
    {
        _logReader = logReader;
    }

    [RelayCommand]
    public async Task LoadLogs()
    {
        var levelFilter = SelectedLevel == "All" ? null : SelectedLevel;
        var entries = await _logReader.ReadLogsAsync(
            dateFilter: SelectedDate,
            levelFilter: levelFilter,
            searchText: SearchText,
            maxLines: MaxLines);

        Entries = new ObservableCollection<LogEntryModel>(
            entries.OrderBy(e => e.Timestamp));
    }

    [RelayCommand]
    public async Task LoadDates()
    {
        var dates = await _logReader.GetAvailableDatesAsync();
        AvailableDates = new ObservableCollection<string>(dates);
    }

    public async Task InitializeAsync()
    {
        await LoadDates();
        await LoadLogs();
    }
}
