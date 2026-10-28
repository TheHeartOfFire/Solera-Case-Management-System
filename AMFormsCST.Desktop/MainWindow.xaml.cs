using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.Interfaces;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.ViewModels;
using AMFormsCST.Desktop.Views.Pages;
using AMFormsCST.Desktop.Views.Pages.Links;
using AMFormsCST.Desktop.Views.Pages.Tools;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop;
/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : IWindow
{
    public MainWindowViewModel ViewModel { get; }
    private readonly ILogService _logger;

    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService,
        ILogService logger,
        IUpdateManagerService updateManagerService,
        IBugReportService bugReportService
    )
    {
        updateManagerService.CheckForUpdatesOnStartupAsync().ConfigureAwait(false);
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);

        ViewModel = viewModel;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInfo("MainWindow initialized.");
        DataContext = this;

        ViewModel.NavigationItems =
        [
            new NavigationViewItem("Dashboard", SymbolRegular.Home24, typeof(DashboardPage)),
            new NavigationViewItem("Tools", SymbolRegular.Wrench24, typeof(ToolsPage)),
            new NavigationViewItem("Quick Links", SymbolRegular.Link24, typeof(LinksPage))
        ];

        ViewModel.NavigationFooter =
        [
            new NavigationViewItem("Settings", SymbolRegular.Settings24, typeof(SettingsPage))
        ];

        InitializeComponent();

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        navigationService.SetNavigationControl(NavigationView);
        contentDialogService.SetDialogHost(RootContentDialog);
    }

    private void OnNavigationSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not NavigationView navigationView)
        {
            _logger.LogWarning("Navigation selection changed but sender is not NavigationView.");
            return;
        }

        NavigationView.SetCurrentValue(
            NavigationView.HeaderVisibilityProperty,
            navigationView.SelectedItem?.TargetPageType != typeof(DashboardPage)
            && navigationView.SelectedItem?.TargetPageType != typeof(SettingsPage)
                ? Visibility.Visible
                : Visibility.Collapsed
        );
        _logger.LogInfo($"Navigation selection changed: {navigationView.SelectedItem?.TargetPageType}");
    }

    private void NavigationView_OnPaneOpened(NavigationView sender, RoutedEventArgs args)
    {
        _logger.LogInfo("Navigation pane opened.");
    }

    private void NavigationView_OnPaneClosed(NavigationView sender, RoutedEventArgs args)
    {
        _logger.LogInfo("Navigation pane closed.");
    }
}