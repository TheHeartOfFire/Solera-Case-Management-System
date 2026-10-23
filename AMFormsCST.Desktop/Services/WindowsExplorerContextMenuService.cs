using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.Services.Interfaces;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace AMFormsCST.Desktop.Services;

/// <summary>
/// Registers and unregisters Windows Explorer right-click context menu options for .formgen files
/// under CurrentUser (HKCU\Software\Classes), requiring no elevated admin permissions.
/// </summary>
public class WindowsExplorerContextMenuService : IWindowsExplorerContextMenuService
{
    private const string SoleraUtilsVerbKey = @"SystemFileAssociations\.formgen\shell\OpenInSoleraUtils";
    private const string FormGenVerbKey = @"SystemFileAssociations\.formgen\shell\OpenInFormGen";
    private const string LegacySoleraUtilsVerbKey = @".formgen\shell\OpenInSoleraUtils";
    private const string LegacyFormGenVerbKey = @".formgen\shell\OpenInFormGen";

    private const string FgaSoleraUtilsVerbKey = @"SystemFileAssociations\.fga\shell\OpenInSoleraUtils";
    private const string FgaFormGenVerbKey = @"SystemFileAssociations\.fga\shell\OpenInFormGen";
    private const string LegacyFgaSoleraUtilsVerbKey = @".fga\shell\OpenInSoleraUtils";
    private const string LegacyFgaFormGenVerbKey = @".fga\shell\OpenInFormGen";

    private readonly ILogService? _logger;

    public WindowsExplorerContextMenuService(ILogService? logger = null)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{SoleraUtilsVerbKey}");
            return key != null;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error checking explorer context menu registry status.", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public void RegisterContextMenu(string? formgenExePath = null)
    {
        try
        {
            var appExePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(appExePath))
            {
                appExePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            if (string.IsNullOrWhiteSpace(appExePath))
            {
                _logger?.LogError("Unable to determine current application executable path for context menu registration.");
                return;
            }

            // Register Open in Solera Formgen Utilities under SystemFileAssociations and extensions
            RegisterVerb(SoleraUtilsVerbKey, "Open in Solera Formgen Utilities", $"\"{appExePath}\" --open-formgen \"%1\"", $"{appExePath},0");
            RegisterVerb(LegacySoleraUtilsVerbKey, "Open in Solera Formgen Utilities", $"\"{appExePath}\" --open-formgen \"%1\"", $"{appExePath},0");
            RegisterVerb(FgaSoleraUtilsVerbKey, "Open in Solera Formgen Utilities", $"\"{appExePath}\" --open-formgen \"%1\"", $"{appExePath},0");
            RegisterVerb(LegacyFgaSoleraUtilsVerbKey, "Open in Solera Formgen Utilities", $"\"{appExePath}\" --open-formgen \"%1\"", $"{appExePath},0");

            // Register Open in FormGen if path is valid
            if (!string.IsNullOrWhiteSpace(formgenExePath) && File.Exists(formgenExePath))
            {
                RegisterVerb(FormGenVerbKey, "Open in FormGen", $"\"{formgenExePath}\" \"%1\"", $"{formgenExePath},0");
                RegisterVerb(LegacyFormGenVerbKey, "Open in FormGen", $"\"{formgenExePath}\" \"%1\"", $"{formgenExePath},0");
                RegisterVerb(FgaFormGenVerbKey, "Open in FormGen", $"\"{formgenExePath}\" \"%1\"", $"{formgenExePath},0");
                RegisterVerb(LegacyFgaFormGenVerbKey, "Open in FormGen", $"\"{formgenExePath}\" \"%1\"", $"{formgenExePath},0");
            }
            else
            {
                UnregisterVerb(FormGenVerbKey);
                UnregisterVerb(LegacyFormGenVerbKey);
                UnregisterVerb(FgaFormGenVerbKey);
                UnregisterVerb(LegacyFgaFormGenVerbKey);
            }

            _logger?.LogInfo("Windows Explorer context menu for .formgen/.fga files registered successfully.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to register Windows Explorer context menu.", ex);
        }
    }

    /// <inheritdoc/>
    public void UnregisterContextMenu()
    {
        try
        {
            UnregisterVerb(SoleraUtilsVerbKey);
            UnregisterVerb(LegacySoleraUtilsVerbKey);
            UnregisterVerb(FormGenVerbKey);
            UnregisterVerb(LegacyFormGenVerbKey);
            UnregisterVerb(FgaSoleraUtilsVerbKey);
            UnregisterVerb(LegacyFgaSoleraUtilsVerbKey);
            UnregisterVerb(FgaFormGenVerbKey);
            UnregisterVerb(LegacyFgaFormGenVerbKey);
            _logger?.LogInfo("Windows Explorer context menu for .formgen/.fga files unregistered.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to unregister Windows Explorer context menu.", ex);
        }
    }

    /// <inheritdoc/>
    public void SyncWithSettings(bool isEnabled, string? formgenExePath = null)
    {
        if (isEnabled)
        {
            RegisterContextMenu(formgenExePath);
        }
        else
        {
            UnregisterContextMenu();
        }
    }

    private static void RegisterVerb(string subKeyPath, string menuText, string commandString, string? iconPath = null)
    {
        using var classesKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
        if (classesKey == null) return;

        using var verbKey = classesKey.CreateSubKey(subKeyPath);
        if (verbKey == null) return;

        verbKey.SetValue("", menuText);
        if (!string.IsNullOrWhiteSpace(iconPath))
        {
            verbKey.SetValue("Icon", iconPath);
        }

        using var commandKey = verbKey.CreateSubKey("command");
        commandKey?.SetValue("", commandString);
    }

    private static void UnregisterVerb(string subKeyPath)
    {
        try
        {
            using var classesKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes", writable: true);
            classesKey?.DeleteSubKeyTree(subKeyPath, throwOnMissingSubKey: false);
        }
        catch
        {
            // Ignore missing keys during removal
        }
    }
}

