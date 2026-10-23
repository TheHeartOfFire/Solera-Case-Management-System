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

            string targetUrl = browserType switch
            {
                BrowserType.OperaGX => "opera://extensions",
                BrowserType.Edge => "edge://extensions",
                _ => "chrome://extensions"
            };

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = $"--load-extension=\"{extDir}\" {targetUrl}",
                UseShellExecute = true
            };

            Process.Start(psi);
            _logger?.LogInfo($"Launched {browserType} with extension from: {extDir}");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error launching browser '{browserType}' with extension.", ex);
            return false;
        }
    }

    private string? EnsureManifestUpdated()
    {
        var extDir = GetExtensionDirectory();
        if (string.IsNullOrWhiteSpace(extDir)) return null;

        var manifestPath = Path.Combine(extDir, "com.solera.cms.bridge.json");
        if (!File.Exists(manifestPath)) return null;

        var appExePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(appExePath))
        {
            appExePath = Process.GetCurrentProcess().MainModule?.FileName;
        }

        if (string.IsNullOrWhiteSpace(appExePath)) return manifestPath;

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

    public static string? GetExtensionDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "BrowserExtension"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "BrowserExtension")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "BrowserExtension")),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AMFormsCST", "BrowserExtension")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(Path.Combine(candidate, "manifest.json")))
            {
                return candidate;
            }
        }

        return null;
    }

    public static string? FindBrowserExecutable(BrowserType browserType)
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        string[] candidates = browserType switch
        {
            BrowserType.OperaGX =>
            [
                Path.Combine(localAppData, "Programs", "Opera GX", "opera.exe"),
                Path.Combine(localAppData, "Programs", "Opera GX", "launcher.exe"),
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

