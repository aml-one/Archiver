using System.Windows;
using Archiver.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Archiver.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        // Handle dialog open/close from MainViewModel
        if (e.Property == DataContextProperty && DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(MainViewModel.IsDialogOpen) && vm.IsDialogOpen)
                {
                    ShowJobDialog(vm);
                }
            };
        }
    }

    private void ShowJobDialog(MainViewModel mainVm)
    {
        if (mainVm.DialogViewModel == null) return;

        var dialog = new JobEditDialog
        {
            Owner = this,
            DataContext = mainVm.DialogViewModel
        };

        var result = dialog.ShowDialog();
        mainVm.CloseDialog(); // refresh regardless of result
    }
}
