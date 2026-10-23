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

            // Ensure primary 'open' verb routes through SCMT launcher (tries FormGen first, falls back to SCMT)
            RegisterVerb(@"formgenfile\shell\open", "Open", $"\"{appExePath}\" --open-in-formgen \"%1\"", $"{appExePath},0");
            RegisterVerb(@"fgafile\shell\open", "Open", $"\"{appExePath}\" --open-in-formgen \"%1\"", $"{appExePath},0");

            // 3. Register Open in FormGen if path is valid
            var resolvedPath = !string.IsNullOrWhiteSpace(formgenExePath)
                ? ResolveShortcutTarget(formgenExePath)
                : FormGenLauncherHelper.ResolveFormGenBatchPath();
            bool hasFormGen = !string.IsNullOrWhiteSpace(resolvedPath) && File.Exists(resolvedPath);
            if (hasFormGen)
            {
                FormGenLauncherHelper.EnsureAgentDeployedAndPatched(resolvedPath!, _logger);
                string formGenCommand = !string.IsNullOrWhiteSpace(appExePath)
                    ? $"\"{appExePath}\" --open-in-formgen \"%1\""
                    : FormatLaunchCommand(resolvedPath!);
                RegisterVerb(FormGenVerbKey, "Open in FormGen", formGenCommand, $"{resolvedPath},0");
                RegisterVerb(LegacyFormGenVerbKey, "Open in FormGen", formGenCommand, $"{resolvedPath},0");
                RegisterVerb(FgaFormGenVerbKey, "Open in FormGen", formGenCommand, $"{resolvedPath},0");
                RegisterVerb(LegacyFgaFormGenVerbKey, "Open in FormGen", formGenCommand, $"{resolvedPath},0");

                foreach (var progId in progIds)
                {
                    string? appliesTo = progId.StartsWith("Applications\\", StringComparison.OrdinalIgnoreCase)
                        ? "System.FileExtension:=.formgen OR System.FileExtension:=.fga"
                        : null;

                    RegisterVerb($@"{progId}\shell\OpenInFormGen", "Open in FormGen", formGenCommand, $"{resolvedPath},0", appliesTo);
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

            // Restore vanilla FormGen open command if FormGen exists, or remove our wrapper
            var formgenBat = FormGenLauncherHelper.ResolveFormGenBatchPath();
            if (!string.IsNullOrWhiteSpace(formgenBat) && File.Exists(formgenBat))
            {
                FormGenLauncherHelper.RestoreVanillaBatch(formgenBat, _logger);
                string vanillaCommand = FormatLaunchCommand(formgenBat);
                RegisterVerb(@"formgenfile\shell\open", "Open", vanillaCommand, $"{formgenBat},0");
                RegisterVerb(@"fgafile\shell\open", "Open", vanillaCommand, $"{formgenBat},0");
            }
            else
            {
                UnregisterVerb(@"formgenfile\shell\open");
                UnregisterVerb(@"fgafile\shell\open");
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

    /// <inheritdoc/>
    public bool IsFormgenAssociated()
    {
        try
        {
            // Check if UserChoice is overriding with another app (e.g. notepad)
            using var userChoiceKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.formgen\UserChoice");
            if (userChoiceKey != null)
            {
                var progId = userChoiceKey.GetValue("ProgId") as string;
                if (!string.Equals(progId, "formgenfile", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            // Check if .formgen is pointing to formgenfile
            using var extKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.formgen");
            var defaultProgId = extKey?.GetValue("") as string;
            if (!string.Equals(defaultProgId, "formgenfile", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Check if formgenfile\shell\open\command is configured
            using var cmdKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\formgenfile\shell\open\command");
            var cmd = cmdKey?.GetValue("") as string;
            return !string.IsNullOrWhiteSpace(cmd);
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error checking if .formgen is associated with FormGen.", ex);
            return false;
        }
    }

    /// <inheritdoc/>
    public bool AssociateFormgen(string formgenPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(formgenPath))
            {
                _logger?.LogError("Cannot associate .formgen: Path is empty.");
                return false;
            }

            var targetPath = ResolveShortcutTarget(formgenPath);
            if (!File.Exists(targetPath))
            {
                _logger?.LogError($"Cannot associate .formgen: File '{targetPath}' does not exist.");
                return false;
            }

            var appExePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(appExePath))
            {
                appExePath = Process.GetCurrentProcess().MainModule?.FileName;
            }

            FormGenLauncherHelper.EnsureAgentDeployedAndPatched(targetPath, _logger);
            string launchCommand = !string.IsNullOrWhiteSpace(appExePath)
                ? $"\"{appExePath}\" --open-in-formgen \"%1\""
                : FormatLaunchCommand(targetPath);



            // 1. Clear stubborn UserChoice overrides for .formgen and .fga
            DeleteUserChoice(".formgen");
            DeleteUserChoice(".fga");

            // 2. Set file associations under HKCU\Software\Classes
            using var classesKey = Registry.CurrentUser.CreateSubKey(@"Software\Classes");
            if (classesKey != null)
            {
                // .formgen
                using (var extKey = classesKey.CreateSubKey(".formgen"))
                {
                    extKey?.SetValue("", "formgenfile");
                }

                using (var progKey = classesKey.CreateSubKey("formgenfile"))
                {
                    progKey?.SetValue("", "FormGen File");
                    using var iconKey = progKey?.CreateSubKey("DefaultIcon");
                    iconKey?.SetValue("", $"{targetPath},0");

                    using var cmdKey = progKey?.CreateSubKey(@"shell\open\command");
                    cmdKey?.SetValue("", launchCommand);
                }

                // .fga
                using (var extKey = classesKey.CreateSubKey(".fga"))
                {
                    extKey?.SetValue("", "fgafile");
                }

                using (var progKey = classesKey.CreateSubKey("fgafile"))
                {
                    progKey?.SetValue("", "FormGen Archive File");
                    using var iconKey = progKey?.CreateSubKey("DefaultIcon");
                    iconKey?.SetValue("", $"{targetPath},0");

                    using var cmdKey = progKey?.CreateSubKey(@"shell\open\command");
                    cmdKey?.SetValue("", launchCommand);
                }
            }

            // 3. Register under Explorer FileExts OpenWithProgids
            try
            {
                using var owpFormgen = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.formgen\OpenWithProgids");
                owpFormgen?.SetValue("formgenfile", Array.Empty<byte>(), RegistryValueKind.None);

                using var owpFga = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.fga\OpenWithProgids");
                owpFga?.SetValue("fgafile", Array.Empty<byte>(), RegistryValueKind.None);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Non-fatal error updating OpenWithProgids: {ex.Message}");
            }

            // 4. Ensure context menu entries are also updated/registered
            RegisterContextMenu(targetPath);

            // 5. Notify Explorer to flush its association cache
            RefreshShell();

            _logger?.LogInfo($"Successfully associated .formgen and .fga files with '{targetPath}'.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to associate .formgen files with FormGen: {ex.Message}", ex);
            return false;
        }
    }

    private static void DeleteUserChoice(string extension)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "reg.exe",
                Arguments = $@"delete ""HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}\UserChoice"" /f",
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
            process?.WaitForExit(3000);
        }
        catch { }

        try
        {
            using var parent = Registry.CurrentUser.OpenSubKey($@"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\{extension}", writable: true);
            parent?.DeleteSubKeyTree("UserChoice", throwOnMissingSubKey: false);
        }
        catch { }
    }

    private static string FormatLaunchCommand(string path)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".jar", StringComparison.OrdinalIgnoreCase))
        {
            return $"javaw.exe -jar \"{path}\" \"%1\"";
        }

        if (ext.Equals(".bat", StringComparison.OrdinalIgnoreCase) || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            var dir = Path.GetDirectoryName(path);
            return $"\"cmd.exe\" /c \"cd /d \"{dir}\" && \"{path}\" \"%1\"\"";
        }

        return $"\"{path}\" \"%1\"";
    }

    private static string ResolveShortcutTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return path;

        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic? shell = Activator.CreateInstance(shellType);
                    dynamic? shortcut = shell?.CreateShortcut(path);
                    string? target = shortcut?.TargetPath;
                    if (!string.IsNullOrWhiteSpace(target) && File.Exists(target))
                    {
                        return target;
                    }
                }
            }
            catch { }
        }

        return path;
    }
}

