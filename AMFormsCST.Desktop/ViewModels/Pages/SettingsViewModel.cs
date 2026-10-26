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

    [ObservableProperty]
    private bool _isFormGenPatched = true;

    [ObservableProperty]
    private string _formGenPatchStatus = string.Empty;

    [ObservableProperty]
    private bool _formGenPreserveUdfTab = true;

    [ObservableProperty]
    private bool _formGenCanvasPasteField = true;

    [ObservableProperty]
    private bool _formGenAlreadyOpenToast = true;

    [ObservableProperty]
    private bool _formGenContextMenus = true;

    [ObservableProperty]
    private bool _formGenEditorUndoRedo = true;

    [ObservableProperty]
    private bool _formGenDialogAutoFill = true;

    [ObservableProperty]
    private bool _formGenAutoRestoreForms = true;

    [ObservableProperty]
    private bool _enableShiftRightClickFlyout = true;

    [ObservableProperty]
    private bool _isBrowserBridgeRegistered;

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
        UpdateFormGenPatchStatus();
        UpdateBrowserStatus();
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

        IsFormGenPatched = userSettings.IsFormGenPatched;
        FormGenPreserveUdfTab = userSettings.FormGenPreserveUdfTab;
        FormGenCanvasPasteField = userSettings.FormGenCanvasPasteField;
        FormGenAlreadyOpenToast = userSettings.FormGenAlreadyOpenToast;
        FormGenContextMenus = userSettings.FormGenContextMenus;
        FormGenEditorUndoRedo = userSettings.FormGenEditorUndoRedo;
        FormGenDialogAutoFill = userSettings.FormGenDialogAutoFill;
        FormGenAutoRestoreForms = userSettings.FormGenAutoRestoreForms;
        EnableShiftRightClickFlyout = userSettings.EnableShiftRightClickFlyout;

        FormGenLauncherHelper.WriteAgentConfig(userSettings, _logger);

        UpdateBrowserStatus();
        UpdateFormgenAssociationStatus();
        UpdateFormGenPatchStatus();

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
                    UpdateFormGenPatchStatus();
                    _logger?.LogInfo($"Explorer context menu settings updated: Enabled={EnableExplorerContextMenu}, Path={FormGenExecutablePath}");
                    break;

                case nameof(IsFormGenPatched):
                case nameof(FormGenPreserveUdfTab):
                case nameof(FormGenCanvasPasteField):
                case nameof(FormGenAlreadyOpenToast):
                case nameof(FormGenContextMenus):
                case nameof(FormGenEditorUndoRedo):
                case nameof(FormGenDialogAutoFill):
                case nameof(FormGenAutoRestoreForms):
                case nameof(EnableShiftRightClickFlyout):
                    var us = _supportTool.Settings.UserSettings;
                    us.IsFormGenPatched = IsFormGenPatched;
                    us.FormGenPreserveUdfTab = FormGenPreserveUdfTab;
                    us.FormGenCanvasPasteField = FormGenCanvasPasteField;
                    us.FormGenAlreadyOpenToast = FormGenAlreadyOpenToast;
                    us.FormGenContextMenus = FormGenContextMenus;
                    us.FormGenEditorUndoRedo = FormGenEditorUndoRedo;
                    us.FormGenDialogAutoFill = FormGenDialogAutoFill;
                    us.FormGenAutoRestoreForms = FormGenAutoRestoreForms;
                    us.EnableShiftRightClickFlyout = EnableShiftRightClickFlyout;

                    FormGenLauncherHelper.WriteAgentConfig(us, _logger);
                    SyncFeatureTogglesToAgent();
                    UpdateFormGenPatchStatus();
                    _logger?.LogInfo($"FormGen feature toggle '{e.PropertyName}' updated.");
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

    private void SyncFeatureTogglesToAgent()
    {
        if (!_isInitialized || !IsFormGenPatched) return;
        _ = Task.Run(() =>
        {
            try
            {
                var ipcService = App.GetService<IpcService>();
                var vars = ipcService?.GetLiveVariables();
                if (vars != null)
                {
                    FormGenLauncherHelper.SyncStateToAgent(vars, Environment.ProcessPath, _logger);
                }
            }
            catch { }
        });
    }

    public void UpdateFormGenPatchStatus()
    {
        bool batPatched = FormGenLauncherHelper.IsBatchPatched(FormGenExecutablePath);
        bool agentActive = FormGenLauncherHelper.IsAgentActive();

        if (IsFormGenPatched)
        {
            if (agentActive)
            {
                FormGenPatchStatus = "Patched & Active (FormGenAgent connected to running FormGen)";
            }
            else if (batPatched)
            {
                FormGenPatchStatus = "Patched & Ready (Modifications active on FormGen launch)";
            }
            else
            {
                FormGenPatchStatus = "Enabled (Click 'Patch FormGen' to deploy payload to FormGen)";
            }
        }
        else
        {
            FormGenPatchStatus = "Unpatched (Vanilla FormGen — all modifications removed; feature states saved)";
        }
    }

    [RelayCommand]
    private void PatchFormGen()
    {
        try
        {
            var batPath = FormGenLauncherHelper.ResolveFormGenBatchPath(FormGenExecutablePath);
            if (string.IsNullOrWhiteSpace(batPath) || !File.Exists(batPath))
            {
                _snackbarService?.Show(
                    "FormGen Not Found",
                    "Could not find NewFormGen.bat. Please browse for NewFormGen.bat in Windows Integration above.",
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(4));
                return;
            }

            var openForms = FormGenLauncherHelper.GetOpenFormsFromAgent(_logger);
            bool isRunning = FormGenLauncherHelper.IsFormGenProcessRunning();

            string openFormsText = openForms.Count > 0
                ? $"\n\nOpen form(s) that will be automatically reopened ({openForms.Count}):\n• " +
                  string.Join("\n• ", openForms.Select(Path.GetFileName))
                : string.Empty;

            string message = isRunning
                ? "FormGen is currently running.\n\n" +
                  "Patching FormGen will deploy the Solera payload, close FormGen, and relaunch it with your modifications enabled.\n\n" +
                  "Please save any unsaved changes in FormGen before proceeding." +
                  openFormsText +
                  "\n\nAre you ready to reload FormGen and apply the patch?"
                : "Deploy the Solera payload to FormGen and launch FormGen now?" +
                  openFormsText +
                  "\n\nClick 'Yes' to patch and launch FormGen, or 'No' to patch without launching.";

            var buttons = isRunning ? System.Windows.MessageBoxButton.OKCancel : System.Windows.MessageBoxButton.YesNoCancel;
            var result = System.Windows.MessageBox.Show(
                message,
                "Patch FormGen — Prepare for Reload",
                buttons,
                System.Windows.MessageBoxImage.Information);

            if (result == System.Windows.MessageBoxResult.Cancel)
                return;

            bool shouldRelaunch = isRunning ? (result == System.Windows.MessageBoxResult.OK) : (result == System.Windows.MessageBoxResult.Yes);

            // Re-query open forms right before closing in case the user saved a new form while the dialog was up
            if (isRunning)
            {
                var latestForms = FormGenLauncherHelper.GetOpenFormsFromAgent(_logger);
                if (latestForms.Count > 0)
                {
                    openForms = latestForms;
                }
            }

            IsFormGenPatched = true;
            _supportTool.Settings.UserSettings.IsFormGenPatched = true;
            _supportTool.SaveAllSettings();

            bool ok = FormGenLauncherHelper.PatchAndReloadFormGen(
                batPath,
                _supportTool.Settings.UserSettings,
                openForms,
                relaunchFormGen: shouldRelaunch,
                _logger);

            if (ok)
            {
                UpdateFormGenPatchStatus();
                _snackbarService?.Show(
                    "FormGen Patched",
                    shouldRelaunch
                        ? $"Solera tools enabled and FormGen reloaded{(openForms.Count > 0 ? $" (restoring {openForms.Count} form(s))" : "")}."
                        : "Solera tools enabled for NewFormGen.bat. Feature toggles are active.",
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                    TimeSpan.FromSeconds(4));
            }
            else
            {
                // Patch failed: revert state
                FormGenLauncherHelper.IsPatchingAllowed = false;
                IsFormGenPatched = false;
                _supportTool.Settings.UserSettings.IsFormGenPatched = false;
                _supportTool.SaveAllSettings();
                UpdateFormGenPatchStatus();

                _snackbarService?.Show(
                    "Patch Failed",
                    "Could not patch FormGen. Check logs for details.",
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(4));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error patching FormGen.", ex);
        }
    }

    [RelayCommand]
    private void UnpatchFormGen()
    {
        try
        {
            var batPath = FormGenLauncherHelper.ResolveFormGenBatchPath(FormGenExecutablePath);
            var batDir = !string.IsNullOrWhiteSpace(batPath) ? Path.GetDirectoryName(batPath) : null;
            if (string.IsNullOrWhiteSpace(batPath) || (!File.Exists(batPath) && (string.IsNullOrWhiteSpace(batDir) || !Directory.Exists(batDir))))
            {
                _snackbarService?.Show(
                    "FormGen Not Found",
                    "Could not find NewFormGen.bat. Please browse for NewFormGen.bat in Windows Integration above.",
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(4));
                return;
            }

            var openForms = FormGenLauncherHelper.GetOpenFormsFromAgent(_logger);
            bool isRunning = FormGenLauncherHelper.IsFormGenProcessRunning();

            string openFormsText = openForms.Count > 0
                ? $"\n\nOpen form(s) to reopen after unpatching ({openForms.Count}):\n• " +
                  string.Join("\n• ", openForms.Select(Path.GetFileName))
                : string.Empty;

            string message = isRunning
                ? "FormGen is currently running.\n\n" +
                  "Unpatching FormGen will close FormGen, remove all traces of Solera modifications (restoring vanilla NewFormGen.bat and deleting FormGenAgent.jar), and relaunch vanilla FormGen.\n\n" +
                  "Individual feature toggles will be disabled while unpatched, but their states will be remembered.\n\n" +
                  "Please save any unsaved changes in FormGen before proceeding." +
                  openFormsText +
                  "\n\nProceed with unpatching and reloading FormGen?"
                : "Remove all traces of Solera modifications from FormGen (restore vanilla NewFormGen.bat and delete FormGenAgent.jar)?\n\n" +
                  "Individual feature toggles will be disabled while unpatched, but their states will be remembered.";

            var result = System.Windows.MessageBox.Show(
                message,
                "Unpatch FormGen — Remove Modifications",
                System.Windows.MessageBoxButton.OKCancel,
                System.Windows.MessageBoxImage.Warning);

            if (result != System.Windows.MessageBoxResult.OK)
                return;

            if (isRunning)
            {
                var latestForms = FormGenLauncherHelper.GetOpenFormsFromAgent(_logger);
                if (latestForms.Count > 0)
                {
                    openForms = latestForms;
                }
            }

            bool ok = FormGenLauncherHelper.UnpatchAndReloadFormGen(
                batPath,
                _supportTool.Settings.UserSettings,
                openForms,
                relaunchFormGen: isRunning,
                _logger);

            if (ok)
            {
                // Unpatch succeeded: update UI and persist setting
                IsFormGenPatched = false;
                _supportTool.Settings.UserSettings.IsFormGenPatched = false;
                _supportTool.SaveAllSettings();
                UpdateFormGenPatchStatus();

                _snackbarService?.Show(
                    "FormGen Unpatched",
                    isRunning
                        ? "All modifications removed and vanilla FormGen reloaded. Feature states are preserved."
                        : "All modifications removed from FormGen. Feature states are preserved.",
                    ControlAppearance.Success,
                    new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                    TimeSpan.FromSeconds(4));
            }
            else
            {
                // Unpatch failed: restore state in helper and UI
                FormGenLauncherHelper.IsPatchingAllowed = true;
                IsFormGenPatched = true;
                _supportTool.Settings.UserSettings.IsFormGenPatched = true;
                _supportTool.SaveAllSettings();
                UpdateFormGenPatchStatus();

                _snackbarService?.Show(
                    "Unpatch Failed",
                    "Failed to unpatch FormGen. Please check logs for details.",
                    ControlAppearance.Danger,
                    new SymbolIcon(SymbolRegular.ErrorCircle24),
                    TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error unpatching FormGen.", ex);
        }
    }

    [RelayCommand]
    private void BrowseFormGenExecutable()
    {
        try
        {
            var openFileDialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select FormGen Executable or Batch File",
                Filter = "Executables & Batch (*.exe;*.bat;*.cmd;javaw.exe)|*.exe;*.cmd;*.bat;javaw.exe|All files (*.*)|*.*",
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
            IsBrowserBridgeRegistered = isReg;
            BrowserBridgeStatus = isReg
                ? $"Registered & Ready (ID: {_browserIntegrationService.ExtensionId})"
                : "Not Registered (Click 'Register Bridge' to connect)";
        }
        else
        {
            IsBrowserBridgeRegistered = false;
            BrowserBridgeStatus = "Service unavailable";
        }
    }

    [RelayCommand]
    private void RegisterBrowserBridge()
    {
        _browserIntegrationService?.RegisterNativeMessagingHost();
        UpdateBrowserStatus();
        _snackbarService?.Show(
            "Bridge Registered",
            "Browser Native Messaging Bridge registered in Windows Registry.",
            ControlAppearance.Success,
            new SymbolIcon(SymbolRegular.CheckmarkCircle24),
            TimeSpan.FromSeconds(3));
    }

    [RelayCommand]
    private void RemoveBrowserBridge()
    {
        _browserIntegrationService?.UnregisterNativeMessagingHost();
        UpdateBrowserStatus();
        _snackbarService?.Show(
            "Bridge Removed",
            "Browser Native Messaging Bridge removed from Windows Registry.",
            ControlAppearance.Info,
            new SymbolIcon(SymbolRegular.Delete24),
            TimeSpan.FromSeconds(3));
    }

    [RelayCommand]
    private void LaunchOperaGXExtension()
    {
        LaunchBrowserExtensionWithFeedback(Services.Interfaces.BrowserType.OperaGX, "Opera GX");
    }

    [RelayCommand]
    private void LaunchEdgeExtension()
    {
        LaunchBrowserExtensionWithFeedback(Services.Interfaces.BrowserType.Edge, "Microsoft Edge");
    }

    [RelayCommand]
    private void LaunchChromeExtension()
    {
        LaunchBrowserExtensionWithFeedback(Services.Interfaces.BrowserType.Chrome, "Google Chrome");
    }

    private void LaunchBrowserExtensionWithFeedback(Services.Interfaces.BrowserType browserType, string browserName)
    {
        if (_browserIntegrationService == null) return;

        if (!_browserIntegrationService.IsBrowserInstalled(browserType))
        {
            _snackbarService?.Show(
                $"{browserName} Not Found",
                $"Could not locate {browserName} on this machine.",
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(4));
            return;
        }

        bool launched = _browserIntegrationService.LaunchBrowserWithExtension(browserType);
        UpdateBrowserStatus();

        if (launched)
        {
            _snackbarService?.Show(
                $"{browserName} Extension Setup",
                $"Bridge registered and {browserName} launched with extension setup (folder path copied to clipboard).",
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(4));
        }
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