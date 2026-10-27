using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.ViewModels.Pages;
using System.Windows.Controls;
using Wpf.Ui.Abstractions.Controls;

namespace AMFormsCST.Desktop.Views.Pages;

public partial class SettingsPage : Page, INavigableView<SettingsViewModel>
{
    public SettingsViewModel ViewModel { get; }
    private readonly ILogService? _logger;

    public SettingsPage(SettingsViewModel viewModel, ILogService? logger = null)
    {
        ViewModel = viewModel;
        _logger = logger;
        DataContext = ViewModel;

        InitializeComponent();
        _logger?.LogInfo("SettingsPage initialized.");
    }

    private void SettingsScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scv && scv.ScrollableHeight > 0)
        {
            var targetOffset = scv.VerticalOffset - (e.Delta * 0.6);
            if (targetOffset < 0)
            {
                targetOffset = 0;
            }
            else if (targetOffset > scv.ScrollableHeight)
            {
                targetOffset = scv.ScrollableHeight;
            }

            scv.ScrollToVerticalOffset(targetOffset);
            e.Handled = true;
        }
    }
}