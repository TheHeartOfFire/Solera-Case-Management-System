using AMFormsCST.Desktop.ViewModels.Dialogs;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop.Views.Dialogs
{
    public partial class AddEditLinkWindow : FluentWindow
    {
        public AddLinkDialogViewModel ViewModel { get; }

        public AddEditLinkWindow(AddLinkDialogViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = viewModel;
            InitializeComponent();

            if (Application.Current?.MainWindow is Window mainWindow && mainWindow.IsVisible)
            {
                Owner = mainWindow;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ViewModel.DisplayName))
            {
                System.Windows.MessageBox.Show("Please enter a display name for the link.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(ViewModel.Path))
            {
                System.Windows.MessageBox.Show("Please enter a URL or file path for the link.", "Validation Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}

