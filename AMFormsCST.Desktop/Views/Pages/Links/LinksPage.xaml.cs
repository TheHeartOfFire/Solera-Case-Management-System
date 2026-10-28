using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.ViewModels.Pages.Links;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop.Views.Pages.Links
{
    /// <summary>
    /// Interaction logic for LinksPage.xaml
    /// </summary>
    public partial class LinksPage : INavigableView<LinksViewModel>
    {
        private readonly INavigationService? _navigationService;
        private readonly ILogService? _logger;

        public LinksViewModel ViewModel { get; }

        public LinksPage(LinksViewModel viewModel, INavigationService navigationService, ILogService? logger = null)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            _navigationService = navigationService;
            _logger = logger;
            InitializeComponent();
            Loaded += HandleLoaded;
            Unloaded += HandleUnloaded;
            _logger?.LogInfo("LinksPage initialized.");
        }

        private void HandleLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_navigationService is null) return;
                INavigationView? navigationControl = _navigationService.GetNavigationControl();
                if (
                    navigationControl?.BreadcrumbBar != null
                    && navigationControl.BreadcrumbBar.Visibility != Visibility.Visible
                )
                {
                    navigationControl.BreadcrumbBar.SetCurrentValue(VisibilityProperty, Visibility.Visible);
                }
                _logger?.LogInfo("LinksPage loaded. BreadcrumbBar set to Visible.");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error in LinksPage HandleLoaded.", ex);
            }
        }

        private void HandleUnloaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_navigationService is null) return;
                INavigationView? navigationControl = _navigationService.GetNavigationControl();
                if (
                    navigationControl?.BreadcrumbBar != null
                    && navigationControl.BreadcrumbBar.Visibility != Visibility.Collapsed
                )
                {
                    navigationControl.BreadcrumbBar.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
                }

                Loaded -= HandleLoaded;
                Unloaded -= HandleUnloaded;
                _logger?.LogInfo("LinksPage unloaded. BreadcrumbBar set to Collapsed.");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error in LinksPage HandleUnloaded.", ex);
            }
        }

        private void OnPagePreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var scrollViewer = (LinksScrollViewer != null && LinksScrollViewer.ScrollableHeight > 0)
                ? LinksScrollViewer
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
}