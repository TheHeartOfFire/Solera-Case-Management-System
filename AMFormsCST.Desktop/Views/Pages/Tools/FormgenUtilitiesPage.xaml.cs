using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.ControlsLookup;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Controls;
using AMFormsCST.Desktop.ViewModels.Pages.Tools.FormgenUtils;

namespace AMFormsCST.Desktop.Views.Pages.Tools
{
    /// <summary>
    /// Interaction logic for FormgenUtilitiesPage.xaml
    /// </summary>
    [GalleryPage("Here are a collection of utilities for manipulating .formgen files.", SymbolRegular.DocumentTextToolbox24)]
    public partial class FormgenUtilitiesPage : Page
    {
        private readonly INavigationService _navigationService;
        private readonly ILogService? _logger;
        public FormgenUtilitiesViewModel ViewModel { get; }

        public FormgenUtilitiesPage(
            FormgenUtilitiesViewModel viewModel,
            INavigationService navigationService,
            ILogService? logger = null
        )
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            _navigationService = navigationService;
            _logger = logger;

            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            ViewModel.RequestScrollToPrompt += OnRequestScrollToPrompt;
            ViewModel.RequestScrollToField += OnRequestScrollToField;
            _logger?.LogInfo("FormgenUtilitiesPage initialized.");
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Application.Current?.Dispatcher == null)
                return;

            Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() =>
                {
                    if (_navigationService.GetNavigationControl() is not Wpf.Ui.Controls.NavigationView navigationView)
                    {
                        return;
                    }

                    navigationView.IsBackButtonVisible = Wpf.Ui.Controls.NavigationViewBackButtonVisible.Visible;

                    var parentScrollViewer = FindParent<ScrollViewer>(this);
                    if (parentScrollViewer != null)
                    {
                        parentScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                    }
                    _logger?.LogInfo("FormgenUtilitiesPage loaded. Back button visible, scroll disabled.");
                })
            );
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var parentScrollViewer = FindParent<ScrollViewer>(this);
            if (parentScrollViewer != null)
            {
                parentScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            }

            ViewModel.RequestScrollToPrompt -= OnRequestScrollToPrompt;
            ViewModel.RequestScrollToField -= OnRequestScrollToField;

            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            _logger?.LogInfo("FormgenUtilitiesPage unloaded. Scroll re-enabled.");
        }

        private void OnRequestScrollToPrompt(PromptItemViewModel item)
        {
            CenterDataGridItem(PromptsDataGrid, item);
        }

        private void OnRequestScrollToField(FieldItemViewModel item)
        {
            CenterDataGridItem(FieldsDataGrid, item);
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeItemNodeViewModel node)
            {
                ViewModel.SelectedNode = node;
            }
        }

        private void TreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeView tv && tv.SelectedItem is TreeItemNodeViewModel node)
            {
                ViewModel.NavigateToTreeNode(node);
                e.Handled = true;
            }
        }

        private static void CenterDataGridItem(System.Windows.Controls.DataGrid? grid, object? item)
        {
            if (grid == null || item == null) return;

            grid.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
            {
                grid.ScrollIntoView(item);
                grid.UpdateLayout();

                var scrollViewer = FindVisualChild<ScrollViewer>(grid);
                if (scrollViewer != null)
                {
                    var row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromItem(item);
                    if (row != null)
                    {
                        try
                        {
                            var transform = row.TransformToAncestor(scrollViewer);
                            var rowOffset = transform.Transform(new Point(0, 0)).Y + scrollViewer.VerticalOffset;
                            var targetOffset = rowOffset - (scrollViewer.ViewportHeight / 2.0) + (row.ActualHeight / 2.0);
                            scrollViewer.ScrollToVerticalOffset(Math.Max(0, targetOffset));
                        }
                        catch
                        {
                            // Fallback if visual tree transform is momentarily unmapped
                        }
                    }
                }
            });
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        private void OnDataGridPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not System.Windows.Controls.DataGrid dg) return;
            if (e.OriginalSource is System.Windows.Controls.TextBox or Wpf.Ui.Controls.TextBox) return;

            if (e.Key == Key.Delete)
            {
                if (dg == PromptsDataGrid)
                {
                    ViewModel.DeleteSelectedPromptsCommand.Execute(dg.SelectedItems);
                    e.Handled = true;
                }
                else if (dg == FieldsDataGrid)
                {
                    ViewModel.DeleteSelectedFieldsCommand.Execute(dg.SelectedItems);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Space)
            {
                if (dg.SelectedItems.Count > 0)
                {
                    bool anyUnchecked = dg.SelectedItems.OfType<PromptItemViewModel>().Any(p => !p.IsSelected)
                                     || dg.SelectedItems.OfType<FieldItemViewModel>().Any(f => !f.IsSelected);

                    foreach (var item in dg.SelectedItems)
                    {
                        if (item is PromptItemViewModel p) p.IsSelected = anyUnchecked;
                        else if (item is FieldItemViewModel f) f.IsSelected = anyUnchecked;
                    }
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Finds a parent of a given item on the visual tree.
        /// </summary>
        /// <typeparam name="T">The type of the queried item.</typeparam>
        /// <param name="child">A direct or indirect child of the queried item.</param>
        /// <returns>The first parent item that matches the submitted type parameter. 
        /// If not found, a null reference is returned.</returns>
        internal static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;
            
            return FindParent<T>(parentObject);
        }

    }
}
