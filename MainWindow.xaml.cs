using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using PasswordExpiryNotification.ViewModels;

namespace PasswordExpiryNotification;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        if (!string.IsNullOrEmpty(viewModel.Settings.Smtp.Password))
            SmtpPasswordBox.Password = viewModel.Settings.Smtp.Password;
    }

    private void SmtpPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && sender is PasswordBox box)
            vm.Settings.Smtp.Password = box.Password;
    }

    private void PreviewGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
            vm.PreviewVm.RefreshMailPreview();
    }

    private void Hyperlink_OnRequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }
}
