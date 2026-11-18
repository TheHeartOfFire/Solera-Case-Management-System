using AMFormsCST.Core.Interfaces;
using System.Windows;
using Velopack;
using Velopack.Sources;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop.Services;

public class UpdateManagerService : IUpdateManagerService
{
    private readonly ISnackbarService _snackbarService;
    private readonly UpdateManager _updateManager;
    private readonly ILogService? _logger;

    public UpdateManagerService(ISnackbarService snackbarService, ILogService? logger = null)
    {
        _snackbarService = snackbarService;
        _logger = logger;
#if DEV_BUILD
        _updateManager = new UpdateManager(
            new GithubSource("https://github.com/TheHeartOfFire/AutoMate-Forms-Customer-Support-Tool", null, true),
            new UpdateOptions { ExplicitChannel = "dev" });
#else
        _updateManager = new UpdateManager(new GithubSource("https://github.com/TheHeartOfFire/AutoMate-Forms-Customer-Support-Tool", null, false));
#endif
        _logger?.LogInfo("UpdateManagerService initialized.");
    }

    public async Task CheckForUpdatesAsync()
    {
        try
        {
            _logger?.LogInfo("Checking for updates...");
            var newVersion = await _updateManager.CheckForUpdatesAsync();
            if (newVersion == null)
            {
                _snackbarService.Show("No Updates Found", "Your application is up to date.", ControlAppearance.Success, new SymbolIcon(SymbolRegular.Checkmark24), TimeSpan.FromSeconds(3));
                _logger?.LogInfo("No updates found.");
                return;
            }

            _logger?.LogInfo($"Update available: {newVersion.TargetFullRelease.Version}");
            await DownloadAndApplyUpdate(newVersion);
        }
        catch (Exception ex)
        {
            _snackbarService.Show("Update Check Failed", ex.Message, ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            _logger?.LogError("Update check failed.", ex);
        }
    }
    
    public async Task CheckForUpdatesOnStartupAsync()
    {
        await Task.Run(async () =>
        {
            try
            {
                _logger?.LogInfo("Checking for updates on startup...");
                var newVersion = await _updateManager.CheckForUpdatesAsync();
                if (newVersion == null)
                {
                    _logger?.LogInfo("No updates found on startup.");
                    return;
                }

                _logger?.LogInfo($"Update available on startup: {newVersion.TargetFullRelease.Version}");
                // Dispatch UI-related update tasks to the main thread
                await Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    await DownloadAndApplyUpdate(newVersion);
                });
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Update check on startup failed: {ex.Message}");
            }
        });
    }

    private async Task DownloadAndApplyUpdate(UpdateInfo newVersion)
    {
        try
        {
            _snackbarService.Show("Update Available", $"Downloading version {newVersion.TargetFullRelease.Version}...", ControlAppearance.Info, new SymbolIcon(SymbolRegular.ArrowDownload24), TimeSpan.FromSeconds(5));
            _logger?.LogInfo($"Downloading update: {newVersion.TargetFullRelease.Version}");

            await _updateManager.DownloadUpdatesAsync(newVersion);

            _logger?.LogInfo("Applying update and restarting application.");
            _updateManager.ApplyUpdatesAndRestart(newVersion);
        }
        catch (Exception ex)
        {
            _snackbarService.Show("Update Failed", ex.Message, ControlAppearance.Danger, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));
            _logger?.LogError("Update failed.", ex);
        }
    }
}

