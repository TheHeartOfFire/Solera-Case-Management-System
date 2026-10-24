using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.Models.UserSettings;
using AMFormsCST.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Wpf.Ui;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop.ViewModels.Pages;

public partial class SettingsViewModel : ViewModel
{
    private readonly IUpdateManagerService _updateManagerService;
    private readonly ISupportTool _supportTool;
    private readonly IBugReportService _bugReportService;
    private readonly Services.Interfaces.IWindowsExplorerContextMenuService? _explorerContextMenuService;
    private readonly Services.Interfaces.IBrowserIntegrationService? _browserIntegrationService;
    private readonly ISnackbarService? _snackbarService;
    private readonly ILogService? _logger;
    private bool _isInitialized = false;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private ApplicationTheme _currentTheme;

    [ObservableProperty]
    private bool _alwaysOnTop;

    [ObservableProperty]
    private bool _selectNewTemplate;

    [ObservableProperty]
    private bool _capitalizeFormCode;

    [ObservableProperty]
    private string _extSeparator = string.Empty;

    [ObservableProperty]
    private bool _enableExplorerContextMenu;

    [ObservableProperty]
    private string _formGenExecutablePath = string.Empty;

    [ObservableProperty]
    private string _browserBridgeStatus = string.Empty;

    [ObservableProperty]
    private bool _isOperaGXInstalled;

    [ObservableProperty]
    private bool _isEdgeInstalled;

    [ObservableProperty]
    private bool _isChromeInstalled;

    [ObservableProperty]
    private bool _isFormgenAssociated;

    [ObservableProperty]
    private string _formgenAssociationStatus = string.Empty;

    public SettingsViewModel(
        IUpdateManagerService updateManagerService,
        ISupportTool supportTool,
        IBugReportService bugReportService,
        Services.Interfaces.IWindowsExplorerContextMenuService? explorerContextMenuService = null,
        Services.Interfaces.IBrowserIntegrationService? browserIntegrationService = null,
        ISnackbarService? snackbarService = null,
        ILogService? logger = null)
    {
        _updateManagerService = updateManagerService;
        _supportTool = supportTool;
        _bugReportService = bugReportService;
        _explorerContextMenuService = explorerContextMenuService;
        _browserIntegrationService = browserIntegrationService;
        _snackbarService = snackbarService;
        _logger = logger;
        _logger?.LogInfo("SettingsViewModel initialized.");

        PropertyChanged += OnSettingsViewModelPropertyChanged;
        if (!_isInitialized)
            InitializeViewModel();
    }

    public void OnNavigatedTo()
    {
        _logger?.LogInfo("SettingsPage navigated to.");
        UpdateFormgenAssociationStatus();
    }

    private void InitializeViewModel()
    {
        AppVersion = $"Version {GetAppVersion()}";
        _logger?.LogDebug($"AppVersion set: {AppVersion}");

        var themeSetting = _supportTool.Settings.UiSettings.Settings.OfType<ThemeSetting>().FirstOrDefault();
        if (themeSetting != null) CurrentTheme = themeSetting.Theme;

        var aotSetting = _supportTool.Settings.UiSettings.Settings.OfType<AlwaysOnTopSetting>().FirstOrDefault();
        if (aotSetting != null) AlwaysOnTop = aotSetting.IsEnabled;

        var templateSetting = _supportTool.Settings.UiSettings.Settings.OfType<NewTemplateSetting>().FirstOrDefault();
        if (templateSetting != null) SelectNewTemplate = templateSetting.SelectNewTemplate;

        var formCodeSetting = _supportTool.Settings.UiSettings.Settings.OfType<CapitalizeFormCodeSetting>().FirstOrDefault();
        if (formCodeSetting != null) CapitalizeFormCode = formCodeSetting.CapitalizeFormCode;

        var userSettings = _supportTool.Settings.UserSettings;
        ExtSeparator = userSettings.ExtSeparator;
        EnableExplorerContextMenu = userSettings.EnableExplorerContextMenu;
        FormGenExecutablePath = userSettings.FormGenExecutablePath;

        UpdateBrowserStatus();
        UpdateFormgenAssociationStatus();

        _isInitialized = true;
        _logger?.LogInfo("SettingsViewModel initialized and settings loaded.");
    }

    private void OnSettingsViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            switch (e.PropertyName)
            {
                case nameof(CurrentTheme):
                    var themeSetting = _supportTool.Settings.UiSettings.Settings.OfType<ThemeSetting>().FirstOrDefault();
                    if (themeSetting != null && themeSetting.Theme != CurrentTheme)
                    {
                        themeSetting.Theme = CurrentTheme;
                        ApplicationThemeManager.Apply(CurrentTheme);
                        _logger?.LogInfo($"Theme changed: {CurrentTheme}");
                    }
                    break;

                case nameof(AlwaysOnTop):
                    var aotSetting = _supportTool.Settings.UiSettings.Settings.OfType<AlwaysOnTopSetting>().FirstOrDefault();
                    if (aotSetting != null)
                    {
                        aotSetting.IsEnabled = AlwaysOnTop;
                        _logger?.LogInfo($"AlwaysOnTop changed: {AlwaysOnTop}");
                    }
                    break;

                case nameof(SelectNewTemplate):
                    var templateSetting = _supportTool.Settings.UiSettings.Settings.OfType<NewTemplateSetting>().FirstOrDefault();
                    if (templateSetting != null)
                    {
                        templateSetting.SelectNewTemplate = SelectNewTemplate;
                        _logger?.LogInfo($"SelectNewTemplate changed: {SelectNewTemplate}");
                    }
                    break;

                case nameof(CapitalizeFormCode):
                    var formCodeSetting = _supportTool.Settings.UiSettings.Settings.OfType<CapitalizeFormCodeSetting>().FirstOrDefault();
                    if (formCodeSetting != null)
                    {
                        formCodeSetting.CapitalizeFormCode = CapitalizeFormCode;
                        _logger?.LogInfo($"CapitalizeFormCode changed: {CapitalizeFormCode}");
                    }
                    break;

                case nameof(ExtSeparator):
                    _supportTool.Settings.UserSettings.ExtSeparator = ExtSeparator;
                    _logger?.LogInfo($"ExtSeparator changed: {ExtSeparator}");
                    break;

                case nameof(EnableExplorerContextMenu):
                case nameof(FormGenExecutablePath):
                    _supportTool.Settings.UserSettings.EnableExplorerContextMenu = EnableExplorerContextMenu;
                    _supportTool.Settings.UserSettings.FormGenExecutablePath = FormGenExecutablePath;
                    _explorerContextMenuService?.SyncWithSettings(EnableExplorerContextMenu, FormGenExecutablePath);
                    UpdateFormgenAssociationStatus();
                    _logger?.LogInfo($"Explorer context menu settings updated: Enabled={EnableExplorerContextMenu}, Path={FormGenExecutablePath}");
                    break;
            }

            if (_isInitialized)
            {
                _supportTool.SaveAllSettings();
                _logger?.LogInfo("Settings saved after property change.");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnSettingsViewModelPropertyChanged.", ex);
        }
    }

    [RelayCommand]
    private void BrowseFormGenExecutable()
    {
        try
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select FormGen Executable",
                Filter = "Executables (*.exe;javaw.exe)|*.exe;*.cmd;*.bat;javaw.exe|All files (*.*)|*.*",
                CheckFileExists = true
            };

            if (openFileDialog.ShowDialog() == true)
            {
                FormGenExecutablePath = openFileDialog.FileName;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error browsing for FormGen executable.", ex);
        }
    }

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        try
        {
            await _updateManagerService.CheckForUpdatesAsync();
            _logger?.LogInfo("CheckForUpdates command executed.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error during CheckForUpdates command.", ex);
        }
    }

    [RelayCommand]
    private async Task ReportBug()
    {
        await _bugReportService.CreateBugReportAsync();
    }

    public void UpdateBrowserStatus()
    {
        if (_browserIntegrationService != null)
        {
            IsOperaGXInstalled = _browserIntegrationService.IsBrowserInstalled(Services.Interfaces.BrowserType.OperaGX);
            IsEdgeInstalled = _browserIntegrationService.IsBrowserInstalled(Services.Interfaces.BrowserType.Edge);
            IsChromeInstalled = _browserIntegrationService.IsBrowserInstalled(Services.Interfaces.BrowserType.Chrome);

            bool isReg = _browserIntegrationService.IsRegistered();
            BrowserBridgeStatus = isReg
                ? $"Registered & Ready (ID: {_browserIntegrationService.ExtensionId})"
                : "Not Registered (Click 'Register Bridge' to connect)";
        }
        else
        {
            BrowserBridgeStatus = "Service unavailable";
        }
    }

    [RelayCommand]
    private void RegisterBrowserBridge()
    {
        _browserIntegrationService?.RegisterNativeMessagingHost();
        UpdateBrowserStatus();
    }

    [RelayCommand]
    private void LaunchOperaGXExtension()
    {
        _browserIntegrationService?.LaunchBrowserWithExtension(Services.Interfaces.BrowserType.OperaGX);
        UpdateBrowserStatus();
    }

    [RelayCommand]
    private void LaunchEdgeExtension()
    {
        _browserIntegrationService?.LaunchBrowserWithExtension(Services.Interfaces.BrowserType.Edge);
        UpdateBrowserStatus();
    }

    [RelayCommand]
    private void LaunchChromeExtension()
    {
        _browserIntegrationService?.LaunchBrowserWithExtension(Services.Interfaces.BrowserType.Chrome);
        UpdateBrowserStatus();
    }

    public void UpdateFormgenAssociationStatus()
    {
        if (_explorerContextMenuService != null)
        {
            IsFormgenAssociated = _explorerContextMenuService.IsFormgenAssociated();
            FormgenAssociationStatus = IsFormgenAssociated
                ? "FormGen is configured as default handler for .formgen and .fga files."
                : "Not associated (double-clicking may open another application like Notepad).";
        }
        else
        {
            FormgenAssociationStatus = "Service unavailable";
        }
    }

    [RelayCommand]
    private void AssociateFormGen()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(FormGenExecutablePath))
            {
                _snackbarService?.Show(
                    "Path Required",
                    "Please specify or browse for the FormGen executable or batch file first.",
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(4));
                return;
            }

            if (!File.Exists(FormGenExecutablePath))
            {
                _snackbarService?.Show(
                    "File Not Found",
                    $"The specified path does not exist: {FormGenExecutablePath}",
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(4));
                return;
            }

            if (_explorerContextMenuService != null)
            {
                bool success = _explorerContextMenuService.AssociateFormgen(FormGenExecutablePath);
                UpdateFormgenAssociationStatus();

                if (success)
                {
                    _snackbarService?.Show(
                        "FormGen Associated",
                        ".formgen and .fga files are now set to open with FormGen by default.",
                        ControlAppearance.Success,
                        new SymbolIcon(SymbolRegular.Checkmark24),
                        TimeSpan.FromSeconds(4));
                }
                else
                {
                    _snackbarService?.Show(
                        "Association Error",
                        "Could not complete file association. Check logs for details.",
                        ControlAppearance.Danger,
                        new SymbolIcon(SymbolRegular.ErrorCircle24),
                        TimeSpan.FromSeconds(4));
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error associating FormGen as default file handler.", ex);
            _snackbarService?.Show(
                "Association Error",
                ex.Message,
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(5));
        }
    }

    private static string GetAppVersion() => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion ?? "1.0.0";
    
}