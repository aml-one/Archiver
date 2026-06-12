using System.Windows;
using System.Windows.Controls;
using Archiver.UI.ViewModels;

namespace Archiver.UI.Views;

public partial class JobEditDialog : Window
{
    public JobEditDialog()
    {
        InitializeComponent();
    }

    private void CredentialPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is JobEditViewModel vm && sender is PasswordBox pb)
        {
            vm.CredentialPassword = pb.Password;
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is JobEditViewModel vm)
        {
            var error = vm.Save();
            if (error != null)
            {
                MessageBox.Show(error, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}
