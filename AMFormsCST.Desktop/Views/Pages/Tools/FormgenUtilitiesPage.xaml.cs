using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.ControlsLookup;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Ui;
using Wpf.Ui.Controls;

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

            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            _logger?.LogInfo("FormgenUtilitiesPage unloaded. Scroll re-enabled.");
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
