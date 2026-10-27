using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.Services.Interfaces;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace AMFormsCST.Desktop.Services;

/// <summary>
/// Service managing browser extension native messaging registration and automatic browser launch.
/// </summary>
public class BrowserIntegrationService : IBrowserIntegrationService
{
    public const string FixedExtensionId = "epjedpmecdfdbfalaklkfbjcpacpfhlp";
    private const string HostName = "com.solera.cms.bridge";

    private static readonly string[] RegistryKeys =
    [
        $@"Software\Opera Software\Opera GX Stable\NativeMessagingHosts\{HostName}",
        $@"Software\Opera Software\Opera Stable\NativeMessagingHosts\{HostName}",
        $@"Software\Google\Chrome\NativeMessagingHosts\{HostName}",
        $@"Software\Microsoft\Edge\NativeMessagingHosts\{HostName}"
    ];

    private readonly ILogService? _logger;

    public BrowserIntegrationService(ILogService? logger = null)
    {
        _logger = logger;
    }

    public string ExtensionId => FixedExtensionId;

    public bool IsRegistered()
    {
        try
        {
            foreach (var keyPath in RegistryKeys)
            {
                using var key = Registry.CurrentUser.OpenSubKey(keyPath);
                if (key != null && key.GetValue(null) is string path && File.Exists(path))
                {
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error checking browser native messaging registration.", ex);
            return false;
        }
    }

    public void RegisterNativeMessagingHost()
    {
        try
        {
            var manifestPath = EnsureManifestUpdated();
            if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath))
            {
                _logger?.LogError("Cannot register native messaging host: manifest file not found.");
                return;
            }

            foreach (var keyPath in RegistryKeys)
            {
                try
                {
                    using var key = Registry.CurrentUser.CreateSubKey(keyPath);
                    key?.SetValue(null, manifestPath, RegistryValueKind.String);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning($"Failed to write registry key '{keyPath}': {ex.Message}");
                }
            }

            _logger?.LogInfo($"Browser native messaging host registered successfully pointing to: {manifestPath}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error registering browser native messaging host.", ex);
        }
    }

    public void UnregisterNativeMessagingHost()
    {
        try
        {
            foreach (var keyPath in RegistryKeys)
            {
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(keyPath, throwOnMissingSubKey: false);
                }
                catch { }
            }
            _logger?.LogInfo("Browser native messaging host registrations removed.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error unregistering browser native messaging host.", ex);
        }
    }

    public bool IsBrowserInstalled(BrowserType browserType)
    {
        var exe = FindBrowserExecutable(browserType);
        return !string.IsNullOrWhiteSpace(exe) && File.Exists(exe);
    }

    public bool LaunchBrowserWithExtension(BrowserType browserType)
    {
        try
        {
            RegisterNativeMessagingHost();

            var extDir = GetExtensionDirectory();
            if (string.IsNullOrWhiteSpace(extDir) || !Directory.Exists(extDir))
            {
                _logger?.LogError("Cannot launch browser: extension directory not found.");
                return false;
            }

            var exe = FindBrowserExecutable(browserType);
            if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe))
            {
                _logger?.LogWarning($"Browser executable for '{browserType}' not found.");
                return false;
            }

            string browserDisplayName = browserType switch
            {
                BrowserType.OperaGX => "Opera GX",
                BrowserType.Edge => "Microsoft Edge",
                _ => "Google Chrome"
            };

            string extensionsUrl = browserType switch
            {
                BrowserType.OperaGX => "opera://extensions",
                BrowserType.Edge => "edge://extensions",
                _ => "chrome://extensions"
            };

            // Copy extension directory path to clipboard for instant Ctrl+V if needed
            try
            {
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    try { System.Windows.Clipboard.SetText(extDir); } catch { }
                });
            }
            catch { }

            var guidePath = EnsureInstallGuideHtml(extDir, browserDisplayName, extensionsUrl);
            var guideUri = new Uri(guidePath).AbsoluteUri;

            bool isAlreadyRunning = IsBrowserProcessRunning(browserType, exe);
            bool restartBrowser = false;

            if (isAlreadyRunning && System.Windows.Application.Current != null)
            {
                var choice = System.Windows.MessageBox.Show(
                    $"{browserDisplayName} is currently open.\n\n" +
                    $"To automatically enable the Solera extension, {browserDisplayName} needs to restart.\n\n" +
                    $"• Click 'Yes' to restart {browserDisplayName} (your open tabs will be restored).\n" +
                    $"• Click 'No' to keep {browserDisplayName} open and view quick setup steps.",
                    $"Enable Solera Extension in {browserDisplayName}",
                    System.Windows.MessageBoxButton.YesNoCancel,
                    System.Windows.MessageBoxImage.Question);

                if (choice == System.Windows.MessageBoxResult.Cancel)
                    return false;

                restartBrowser = choice == System.Windows.MessageBoxResult.Yes;
            }

            if (restartBrowser)
            {
                CloseBrowserProcesses(browserType, exe);
                System.Threading.Thread.Sleep(700);
            }

            if (!isAlreadyRunning || restartBrowser)
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"--load-extension=\"{extDir}\" \"{guideUri}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
                _logger?.LogInfo($"Launched {browserDisplayName} with --load-extension from: {extDir}");
            }
            else
            {
                // Browser is already running and user chose not to restart:
                // Open the guide HTML in a new tab in the running browser (which activates the browser window)
                // and open Windows Explorer with the extension folder selected for 1-second drag-and-drop / Load Unpacked.
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"\"{guideUri}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{extDir}\"",
                        UseShellExecute = true
                    });
                }
                catch { }

                _logger?.LogInfo($"Opened extension setup guide in running {browserDisplayName} and revealed folder: {extDir}");
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error launching browser '{browserType}' with extension.", ex);
            return false;
        }
    }

    private static bool IsBrowserProcessRunning(BrowserType browserType, string exePath)
    {
        try
        {
            string procName = browserType switch
            {
                BrowserType.OperaGX => "opera",
                BrowserType.Edge => "msedge",
                _ => "chrome"
            };

            var procs = Process.GetProcessesByName(procName);
            try
            {
                if (procs.Length == 0) return false;
                if (browserType == BrowserType.OperaGX)
                {
                    var exeDir = Path.GetDirectoryName(exePath);
                    foreach (var p in procs)
                    {
                        try
                        {
                            var pPath = p.MainModule?.FileName;
                            if (!string.IsNullOrWhiteSpace(pPath) &&
                                !string.IsNullOrWhiteSpace(exeDir) &&
                                pPath.StartsWith(exeDir, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                        catch
                        {
                            return true;
                        }
                    }
                }
                return procs.Length > 0;
            }
            finally
            {
                foreach (var p in procs) p.Dispose();
            }
        }
        catch
        {
            return false;
        }
    }

    private static void CloseBrowserProcesses(BrowserType browserType, string exePath)
    {
        try
        {
            string procName = browserType switch
            {
                BrowserType.OperaGX => "opera",
                BrowserType.Edge => "msedge",
                _ => "chrome"
            };

            var exeDir = Path.GetDirectoryName(exePath);
            var procs = Process.GetProcessesByName(procName);
            try
            {
                foreach (var p in procs)
                {
                    try
                    {
                        if (browserType == BrowserType.OperaGX && !string.IsNullOrWhiteSpace(exeDir))
                        {
                            var pPath = p.MainModule?.FileName;
                            if (!string.IsNullOrWhiteSpace(pPath) && !pPath.StartsWith(exeDir, StringComparison.OrdinalIgnoreCase))
                                continue;
                        }
                        if (p.MainWindowHandle != IntPtr.Zero)
                        {
                            p.CloseMainWindow();
                        }
                    }
                    catch { }
                }

                System.Threading.Thread.Sleep(500);

                foreach (var p in procs)
                {
                    try
                    {
                        if (!p.HasExited)
                        {
                            p.Kill();
                            p.WaitForExit(1500);
                        }
                    }
                    catch { }
                }
            }
            finally
            {
                foreach (var p in procs) p.Dispose();
            }
        }
        catch { }
    }

    private static string EnsureInstallGuideHtml(string extDir, string browserName, string extensionsUrl)
    {
        var htmlPath = Path.Combine(extDir, "install-guide.html");
        try
        {
            var escapedDir = extDir.Replace("\\", "\\\\");
            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<title>Solera CMS Browser Extension Setup</title>
<style>
  body {{ font-family: 'Segoe UI', system-ui, sans-serif; background: #12131a; color: #f0f2f8; margin: 0; padding: 40px 20px; display: flex; justify-content: center; }}
  .card {{ max-width: 680px; width: 100%; background: #1c1e29; border: 1px solid #2e3244; border-radius: 12px; padding: 28px 32px; box-shadow: 0 12px 32px rgba(0,0,0,0.45); }}
  h1 {{ margin: 0 0 8px 0; font-size: 22px; color: #63b3ed; }}
  p {{ color: #b5bcd0; line-height: 1.55; margin: 8px 0 16px 0; }}
  .step {{ background: #242736; border-left: 4px solid #63b3ed; padding: 12px 16px; border-radius: 6px; margin-bottom: 12px; }}
  .step strong {{ color: #fff; }}
  code {{ background: #12131a; color: #90cdf4; padding: 4px 8px; border-radius: 4px; font-family: Consolas, monospace; font-size: 13px; word-break: break-all; }}
  .btn {{ display: inline-block; background: #3182ce; color: #fff; border: none; padding: 8px 14px; border-radius: 6px; cursor: pointer; font-weight: 600; margin-top: 8px; margin-right: 8px; }}
  .btn:hover {{ background: #2b6cb0; }}
  .badge {{ display: inline-block; background: #276749; color: #9ae6b4; font-size: 12px; font-weight: 600; padding: 3px 10px; border-radius: 999px; margin-bottom: 12px; }}
</style>
</head>
<body>
  <div class=""card"">
    <div class=""badge"">✓ Ready to Connect</div>
    <h1>Solera Extension for {browserName}</h1>
    <p>Follow these two quick steps to finish enabling the Solera extension in {browserName}:</p>
    <div class=""step"">
      <strong>Step 1:</strong> In {browserName}, open <code>{extensionsUrl}</code> and turn on <strong>Developer mode</strong> (toggle switch in the top-right corner).<br/>
      <button class=""btn"" onclick=""navigator.clipboard.writeText('{extensionsUrl}'); this.textContent='Copied extensions URL!';"">Copy Extensions Page Address</button>
    </div>
    <div class=""step"">
      <strong>Step 2:</strong> Click <strong>Load unpacked</strong> and paste (<code>Ctrl+V</code>) the folder path below (already copied to your clipboard):<br/><br/>
      <code>{extDir}</code><br/>
      <button class=""btn"" onclick=""navigator.clipboard.writeText('{escapedDir}'); this.textContent='Copied Folder Path!';"">Copy Folder Path</button>
    </div>
  </div>
</body>
</html>";
            File.WriteAllText(htmlPath, html);
        }
        catch { }
        return htmlPath;
    }

    public string? EnsureManifestUpdated()
    {
        var extDir = GetExtensionDirectory();
        if (string.IsNullOrWhiteSpace(extDir)) return null;

        var manifestPath = Path.Combine(extDir, "com.solera.cms.bridge.json");
        if (!File.Exists(manifestPath)) return null;

        var appExePath = ResolveDesktopExecutablePath();
        if (string.IsNullOrWhiteSpace(appExePath)) return manifestPath;

        // Never overwrite the git repository template manifest directly if localAppData copy failed
        var localAppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMFormsCST", "BrowserExtension");
        if (!manifestPath.StartsWith(localAppDataDir, StringComparison.OrdinalIgnoreCase))
        {
            return manifestPath;
        }

        try
        {
            var content = File.ReadAllText(manifestPath);
            var escapedExe = appExePath.Replace("\\", "\\\\");
            var updated = System.Text.RegularExpressions.Regex.Replace(
                content,
                "\"path\"\\s*:\\s*\"[^\"]*\"",
                $"\"path\": \"{escapedExe}\"");

            if (!updated.Contains(FixedExtensionId))
            {
                updated = updated.Replace(
                    "\"allowed_origins\": [",
                    $"\"allowed_origins\": [\n    \"chrome-extension://{FixedExtensionId}/\",");
            }

            File.WriteAllText(manifestPath, updated);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Could not update bridge manifest with current exe path: {ex.Message}");
        }

        return manifestPath;
    }

    public static string? ResolveDesktopExecutablePath()
    {
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            try { processPath = Process.GetCurrentProcess().MainModule?.FileName; } catch { }
        }

        if (!string.IsNullOrWhiteSpace(processPath) &&
            processPath.EndsWith("AMFormsCST.Desktop.exe", StringComparison.OrdinalIgnoreCase) &&
            !processPath.Contains("AMFormsCST.Test", StringComparison.OrdinalIgnoreCase) &&
            File.Exists(processPath))
        {
            return processPath;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AMFormsCST.Desktop", "bin", "Debug", "net9.0-windows", "AMFormsCST.Desktop.exe")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "AMFormsCST.Desktop", "bin", "Release", "net9.0-windows", "AMFormsCST.Desktop.exe")),
            Path.Combine(localAppData, "AMFormsCST", "current", "AMFormsCST.Desktop.exe"),
            Path.Combine(localAppData, "SoleraCaseManagement", "current", "AMFormsCST.Desktop.exe"),
            Path.Combine(AppContext.BaseDirectory, "AMFormsCST.Desktop.exe")
        };

        return candidates.FirstOrDefault(c => !c.Contains("AMFormsCST.Test", StringComparison.OrdinalIgnoreCase) && File.Exists(c));
    }

    public static string? GetExtensionDirectory()
    {
        var localAppDataDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMFormsCST", "BrowserExtension");

        var sourceCandidates = new[]
        {
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BrowserExtension")),
            Path.Combine(AppContext.BaseDirectory, "BrowserExtension"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "BrowserExtension"))
        };

        string? sourceDir = sourceCandidates.FirstOrDefault(c => File.Exists(Path.Combine(c, "manifest.json")));

        if (sourceDir != null)
        {
            try
            {
                Directory.CreateDirectory(localAppDataDir);
                foreach (var file in Directory.GetFiles(sourceDir))
                {
                    var destFile = Path.Combine(localAppDataDir, Path.GetFileName(file));
                    File.Copy(file, destFile, overwrite: true);
                }
                return localAppDataDir;
            }
            catch
            {
                return sourceDir;
            }
        }

        if (File.Exists(Path.Combine(localAppDataDir, "manifest.json")))
        {
            return localAppDataDir;
        }

        return null;
    }

    public static string? FindBrowserExecutable(BrowserType browserType)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        if (browserType == BrowserType.OperaGX)
        {
            // Check Opera Software registry keys first for custom or standard Opera GX installs
            try
            {
                using var operaKey = Registry.CurrentUser.OpenSubKey(@"Software\Opera Software");
                var regInstallPath = operaKey?.GetValue("Last Opera GX Stable Install Path") as string
                                  ?? operaKey?.GetValue("Last Install Path") as string;
                if (!string.IsNullOrWhiteSpace(regInstallPath))
                {
                    var regOperaExe = Path.Combine(regInstallPath, "opera.exe");
                    if (File.Exists(regOperaExe)) return regOperaExe;
                    var regLauncherExe = Path.Combine(regInstallPath, "launcher.exe");
                    if (File.Exists(regLauncherExe)) return regLauncherExe;
                }
            }
            catch { }
        }

        string[] candidates = browserType switch
        {
            BrowserType.OperaGX =>
            [
                Path.Combine(localAppData, "Programs", "Opera GX", "opera.exe"),
                Path.Combine(localAppData, "Programs", "Opera GX", "launcher.exe"),
                Path.Combine(programFiles, "Opera GX", "opera.exe"),
                Path.Combine(programFiles, "Opera GX", "launcher.exe"),
                Path.Combine(programFilesX86, "Opera GX", "launcher.exe")
            ],
            BrowserType.Edge =>
            [
                Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe")
            ],
            _ =>
            [
                Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
                Path.Combine(localAppData, "Google", "Chrome", "Application", "chrome.exe")
            ]
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        return null;
    }
}

