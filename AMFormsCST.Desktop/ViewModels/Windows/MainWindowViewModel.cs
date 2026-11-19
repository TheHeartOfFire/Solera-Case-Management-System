using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.Models.UserSettings;
using AMFormsCST.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Wpf.Ui.Appearance;

namespace AMFormsCST.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ISupportTool _supportTool;
    private readonly ILogService? _logger;
    private readonly IBugReportService _bugReportService;

    [ObservableProperty]
    private string _applicationTitle = "Case Management Tool";

    [ObservableProperty]
    private bool _isWindowTopmost;

    [ObservableProperty]
    private bool _isLightTheme;

    [ObservableProperty]
    private ObservableCollection<object> _navigationItems = [];

    [ObservableProperty]
    private ObservableCollection<object> _navigationFooter = [];

    public MainWindowViewModel(ISupportTool supportTool, IBugReportService bugReportService, ILogService? logger = null)
    {
        _supportTool = supportTool;
        _bugReportService = bugReportService;
        _logger = logger;
        _logger?.LogInfo("MainWindowViewModel initialized.");

        var aotSetting = _supportTool.Settings.UiSettings.Settings.OfType<AlwaysOnTopSetting>().FirstOrDefault();
        if (aotSetting != null)
        {
            IsWindowTopmost = aotSetting.IsEnabled;
            aotSetting.PropertyChanged += OnUiSettingsChanged;
            _logger?.LogDebug($"AlwaysOnTopSetting initialized: {IsWindowTopmost}");
        }

        var themeSetting = _supportTool.Settings.UiSettings.Settings.OfType<ThemeSetting>().FirstOrDefault();
        if (themeSetting != null)
        {
            IsLightTheme = themeSetting.Theme == ApplicationTheme.Light;
            themeSetting.PropertyChanged += OnUiSettingsChanged;
            _logger?.LogDebug($"ThemeSetting initialized: {IsLightTheme}");
        }
    }

    [RelayCommand]
    private async Task ReportBug()
    {
        await _bugReportService.CreateBugReportAsync();
    }

    private void OnUiSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        try
        {
            if (e.PropertyName == nameof(AlwaysOnTopSetting.IsEnabled) && sender is AlwaysOnTopSetting aotSetting)
            {
                IsWindowTopmost = aotSetting.IsEnabled;
                _logger?.LogInfo($"AlwaysOnTopSetting changed: {IsWindowTopmost}");
            }
            else if (e.PropertyName == nameof(ThemeSetting.Theme) && sender is ThemeSetting themeSetting)
            {
                IsLightTheme = themeSetting.Theme == ApplicationTheme.Light;
                _logger?.LogInfo($"ThemeSetting changed: {IsLightTheme}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnUiSettingsChanged.", ex);
        }
    }

    partial void OnIsWindowTopmostChanged(bool value)
    {
        try
        {
            var aotSetting = _supportTool.Settings.UiSettings.Settings.OfType<AlwaysOnTopSetting>().FirstOrDefault();
            if (aotSetting != null && aotSetting.IsEnabled != value)
            {
                aotSetting.IsEnabled = value;
                _supportTool.SaveAllSettings();
                _logger?.LogInfo($"IsWindowTopmost changed: {value}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnIsWindowTopmostChanged.", ex);
        }
    }

    partial void OnIsLightThemeChanged(bool value)
    {
        try
        {
            var themeSetting = _supportTool.Settings.UiSettings.Settings.OfType<ThemeSetting>().FirstOrDefault();
            if (themeSetting != null)
            {
                var newTheme = value ? ApplicationTheme.Light : ApplicationTheme.Dark;
                if (themeSetting.Theme != newTheme)
                {
                    themeSetting.Theme = newTheme;
                    ApplicationThemeManager.Apply(newTheme, Wpf.Ui.Controls.WindowBackdropType.Mica);
                    _supportTool.SaveAllSettings();
                    _logger?.LogInfo($"IsLightTheme changed: {value}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnIsLightThemeChanged.", ex);
        }
    }
}