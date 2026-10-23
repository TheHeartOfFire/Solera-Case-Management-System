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
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0x0000;

    /// <inheritdoc/>
    public bool IsRegistered()
    {
        try
        {
            if (Registry.CurrentUser.OpenSubKey($@"Software\Classes\{SoleraUtilsVerbKey}") != null)
                return true;

            if (Registry.CurrentUser.OpenSubKey(@"Software\Classes\formgenfile\shell\OpenInSoleraUtils") != null)
                return true;

            return false;
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

            // 1. Register Open in Solera Formgen Utilities under SystemFileAssociations and extensions
            RegisterVerb(SoleraUtilsVerbKey, "Open in Solera Formgen Utilities", $"\"{appExePath}\" --open-formgen \"%1\"", $"{appExePath},0");
            RegisterVerb(LegacySoleraUtilsVerbKey, "Open in Solera Formgen Utilities", $"\"{appExePath}\" --open-formgen \"%1\"", $"{appExePath},0");
            RegisterVerb(FgaSoleraUtilsVerbKey, "Open in Solera Formgen Utilities", $"\"{appExePath}\" --open-formgen \"%1\"", $"{appExePath},0");
            RegisterVerb(LegacyFgaSoleraUtilsVerbKey, "Open in Solera Formgen Utilities", $"\"{appExePath}\" --open-formgen \"%1\"", $"{appExePath},0");

            // 2. Register under associated ProgIDs (e.g. formgenfile, Applications\notepad.exe, etc.)
            var progIds = GetAssociatedProgIds();
            foreach (var progId in progIds)
            {
                string? appliesTo = progId.StartsWith("Applications\\", StringComparison.OrdinalIgnoreCase)
                    ? "System.FileExtension:=.formgen OR System.FileExtension:=.fga"
                    : null;

                RegisterVerb($@"{progId}\shell\OpenInSoleraUtils", "Open in Solera Formgen Utilities", $"\"{appExePath}\" --open-formgen \"%1\"", $"{appExePath},0", appliesTo);
            }

            // 3. Register Open in FormGen if path is valid
            bool hasFormGen = !string.IsNullOrWhiteSpace(formgenExePath) && File.Exists(formgenExePath);
            if (hasFormGen)
            {
                RegisterVerb(FormGenVerbKey, "Open in FormGen", $"\"{formgenExePath}\" \"%1\"", $"{formgenExePath},0");
                RegisterVerb(LegacyFormGenVerbKey, "Open in FormGen", $"\"{formgenExePath}\" \"%1\"", $"{formgenExePath},0");
                RegisterVerb(FgaFormGenVerbKey, "Open in FormGen", $"\"{formgenExePath}\" \"%1\"", $"{formgenExePath},0");
                RegisterVerb(LegacyFgaFormGenVerbKey, "Open in FormGen", $"\"{formgenExePath}\" \"%1\"", $"{formgenExePath},0");

                foreach (var progId in progIds)
                {
                    string? appliesTo = progId.StartsWith("Applications\\", StringComparison.OrdinalIgnoreCase)
                        ? "System.FileExtension:=.formgen OR System.FileExtension:=.fga"
                        : null;

                    RegisterVerb($@"{progId}\shell\OpenInFormGen", "Open in FormGen", $"\"{formgenExePath}\" \"%1\"", $"{formgenExePath},0", appliesTo);
                }
            }
            else
            {
                UnregisterVerb(FormGenVerbKey);
                UnregisterVerb(LegacyFormGenVerbKey);
                UnregisterVerb(FgaFormGenVerbKey);
                UnregisterVerb(LegacyFgaFormGenVerbKey);

                foreach (var progId in progIds)
                {
                    UnregisterVerb($@"{progId}\shell\OpenInFormGen");
                }
            }

            RefreshShell();
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

            var progIds = GetAssociatedProgIds();
            foreach (var progId in progIds)
            {
                UnregisterVerb($@"{progId}\shell\OpenInSoleraUtils");
                UnregisterVerb($@"{progId}\shell\OpenInFormGen");
            }

            RefreshShell();
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

    private static HashSet<string> GetAssociatedProgIds()
    {
        var progIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "formgenfile",
            "fgafile"
        };

        string[] extensions = [".formgen", ".fga"];
        foreach (var ext in extensions)
        {
            try
            {
                using var extKey = Registry.ClassesRoot.OpenSubKey(ext);
                if (extKey?.GetValue(null) is string defaultProgId && !string.IsNullOrWhiteSpace(defaultProgId))
                {
                    progIds.Add(defaultProgId);
                }
            }
            catch { }

            try
            {
                using var userChoiceKey = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{ext}\UserChoice");
                if (userChoiceKey?.GetValue("ProgId") is string userChoiceProgId && !string.IsNullOrWhiteSpace(userChoiceProgId))
                {
                    progIds.Add(userChoiceProgId);
                }
            }
            catch { }
        }

        return progIds;
    }

    private static void RefreshShell()
    {
        try
        {
            SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);
        }
        catch { }
    }

    private static void RegisterVerb(string subKeyPath, string menuText, string commandString, string? iconPath = null, string? appliesTo = null)
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

        if (!string.IsNullOrWhiteSpace(appliesTo))
        {
            verbKey.SetValue("AppliesTo", appliesTo);
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

