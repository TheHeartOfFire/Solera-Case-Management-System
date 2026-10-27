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

    private void OnPagePreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var scrollViewer = (SettingsScrollViewer != null && SettingsScrollViewer.ScrollableHeight > 0)
            ? SettingsScrollViewer
            : FindParentScrollViewer(this);

        if (scrollViewer != null && scrollViewer.ScrollableHeight > 0)
        {
            var targetOffset = scrollViewer.VerticalOffset - (e.Delta * 0.6);
            if (targetOffset < 0)
            {
                targetOffset = 0;
            }
            else if (targetOffset > scrollViewer.ScrollableHeight)
            {
                targetOffset = scrollViewer.ScrollableHeight;
            }

            scrollViewer.ScrollToVerticalOffset(targetOffset);
            e.Handled = true;
        }
    }

    private static ScrollViewer? FindParentScrollViewer(System.Windows.DependencyObject? child)
    {
        while (child != null)
        {
            if (child is not System.Windows.Media.Visual && child is not System.Windows.Media.Media3D.Visual3D)
            {
                child = System.Windows.LogicalTreeHelper.GetParent(child);
            }
            else
            {
                child = System.Windows.Media.VisualTreeHelper.GetParent(child) ?? System.Windows.LogicalTreeHelper.GetParent(child);
            }

            if (child is ScrollViewer sv && sv.ScrollableHeight > 0)
            {
                return sv;
            }
        }
        return null;
    }
}
