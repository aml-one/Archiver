using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Archiver.UI.ViewModels;

namespace Archiver.UI.Views;

public partial class JobsPage : UserControl
{
    public JobsPage()
    {
        InitializeComponent();
    }

    private void JobItem_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is ListViewItem item && item.DataContext is JobItemViewModel job)
        {
            var window = Window.GetWindow(this);
            if (window?.DataContext is MainViewModel mainVm)
            {
                mainVm.EditJobCommand.Execute(job.Name);
            }
        }
    }
}
