using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using AMFormsCST.Core.Interfaces;

namespace AMFormsCST.Desktop.Services;

/// <summary>
/// Helper for dispatching form files directly to a running FormGen instance via IPC socket,
/// or launching FormGen with the FormGenAgent attached.
/// </summary>
public static class FormGenLauncherHelper
{
    private const string PortFileName = "formgen_agent.port";

    /// <summary>
    /// Test seam: lets a test (and only the async flow it starts) point the helper at a private directory,
    /// so parallel tests and background sync calls don't fight over the shared %TEMP% port file.
    /// </summary>
    internal static readonly System.Threading.AsyncLocal<string?> PortFileDirectoryOverride = new();

    private static string GetPortFilePath() =>
        Path.Combine(PortFileDirectoryOverride.Value ?? Path.GetTempPath(), PortFileName);

    /// <summary>
    /// Opens the specified .formgen or .fga file in FormGen.
    /// If FormGen is already running with FormGenAgent, the file is sent over localhost TCP in milliseconds.
    /// If not running, FormGen is launched with the agent and opens the file on startup.
    /// </summary>
    public static bool OpenInFormGen(string filePath, string? formgenBatPath = null, ILogService? logger = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var cleanPath = filePath.Trim().Trim('"');
        if (!File.Exists(cleanPath))
        {
            logger?.LogWarning($"FormGen launcher: Target file does not exist: {cleanPath}");
            return false;
        }

        // 1. Try IPC socket to existing FormGen instance
        if (TrySendViaIpc(cleanPath, logger))
        {
            logger?.LogInfo($"Successfully opened '{cleanPath}' in running FormGen via IPC socket.");
            return true;
        }

        // 2. If not running, launch NewFormGen.bat with file argument
        var batPath = ResolveFormGenBatchPath(formgenBatPath);
        if (!string.IsNullOrWhiteSpace(batPath) && File.Exists(batPath))
        {
            EnsureAgentDeployedAndPatched(batPath, logger);

            var dir = Path.GetDirectoryName(batPath);
            var startInfo = new ProcessStartInfo
            {
                FileName = batPath,
                Arguments = $"\"{cleanPath}\"",
                WorkingDirectory = dir,
                UseShellExecute = true
            };

            Process.Start(startInfo);
            logger?.LogInfo($"Launched FormGen via '{batPath}' with file '{cleanPath}'.");
            return true;
        }

        logger?.LogError($"FormGen launcher: Could not locate NewFormGen.bat to open '{cleanPath}'.");
        return false;
    }

    /// <summary>
    /// Pushes the form back to FormGen via IPC socket, checking if its UUID matches an open tab.
    /// If FormGen is running, focuses the matching tab and reloads, or opens in a new tab if no match.
    /// If FormGen is not running, launches FormGen with the file.
    /// </summary>
    public static bool PushFormToFormGen(string filePath, string? uuid = null, string? formgenBatPath = null, ILogService? logger = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return false;

        var cleanPath = filePath.Trim().Trim('"');
        if (!File.Exists(cleanPath))
        {
            logger?.LogWarning($"FormGen push: Target file does not exist: {cleanPath}");
            return false;
        }

        // Try pushing via IPC to running FormGen
        if (TryPushViaIpc(cleanPath, uuid, logger))
        {
            logger?.LogInfo($"Successfully pushed '{cleanPath}' (UUID: {uuid}) to running FormGen via IPC.");
            return true;
        }

        // If FormGen is not running, launch it
        logger?.LogInfo($"FormGen not running via IPC. Launching FormGen with '{cleanPath}'.");
        return OpenInFormGen(cleanPath, formgenBatPath, logger);
    }

    /// <summary>
    /// Sends a PUSH_FORM command to FormGenAgent via localhost TCP.
    /// </summary>
    public static bool TryPushViaIpc(string filePath, string? uuid = null, ILogService? logger = null)
    {
        try
        {
            var portFilePath = GetPortFilePath();
            if (!File.Exists(portFilePath))
                return false;

            var portText = File.ReadAllText(portFilePath).Trim();
            if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535)
                return false;

            using var client = new TcpClient { ReceiveTimeout = IpcIoTimeoutMs, SendTimeout = IpcIoTimeoutMs };
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            if (!connectTask.Wait(IpcConnectTimeoutMs))
            {
                // A slow response is not proof FormGen exited, so the port file is left alone.
                client.Close();
                return false;
            }

            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            using var reader = new StreamReader(stream, new UTF8Encoding(false));

            var payload = string.IsNullOrWhiteSpace(uuid)
                ? $"PUSH_FORM:{filePath}"
                : $"PUSH_FORM:{uuid.Trim()}|{filePath}";

            writer.WriteLine(payload);
            var response = reader.ReadLine();
            return string.Equals(response, "OK", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"IPC push to FormGen agent failed: {ex.Message}");
            DeletePortFileIfRefused(ex);
            return false;
        }
    }

    private const int IpcConnectTimeoutMs = 3000;
    private const int IpcIoTimeoutMs = 3000;

    /// <summary>
    /// Deletes the stale port file only when the OS confirms nothing is listening (connection refused).
    /// Timeouts and other transient errors must not delete it, since FormGen writes it only once at startup.
    /// </summary>
    private static void DeletePortFileIfRefused(Exception ex)
    {
        try
        {
            var refused = ex is SocketException { SocketErrorCode: SocketError.ConnectionRefused }
                || ex.InnerException is SocketException { SocketErrorCode: SocketError.ConnectionRefused }
                || (ex is AggregateException agg && agg.InnerExceptions.Any(e => e is SocketException { SocketErrorCode: SocketError.ConnectionRefused }));
            if (!refused) return;
            var p = GetPortFilePath();
            if (File.Exists(p)) File.Delete(p);
        }
        catch { }
    }


    /// <summary>
    /// Attempts to send the file path to the FormGen agent via localhost TCP.
    /// </summary>
    public static bool TrySendViaIpc(string filePath, ILogService? logger = null)
    {
        try
        {
            var portFilePath = GetPortFilePath();
            if (!File.Exists(portFilePath))
                return false;

            var portText = File.ReadAllText(portFilePath).Trim();
            if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535)
                return false;

            using var client = new TcpClient { ReceiveTimeout = IpcIoTimeoutMs, SendTimeout = IpcIoTimeoutMs };
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            if (!connectTask.Wait(IpcConnectTimeoutMs))
            {
                client.Close();
                return false;
            }

            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            using var reader = new StreamReader(stream, new UTF8Encoding(false));

            writer.WriteLine(filePath);
            var response = reader.ReadLine();
            return string.Equals(response, "OK", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"IPC communication with FormGen agent failed: {ex.Message}");
            DeletePortFileIfRefused(ex);
            return false;
        }
    }

    /// <summary>
    /// Checks whether FormGenAgent is actively listening on localhost TCP.
    /// </summary>
    public static bool IsAgentActive()
    {
        try
        {
            var portFilePath = GetPortFilePath();
            if (!File.Exists(portFilePath))
                return false;

            var portText = File.ReadAllText(portFilePath).Trim();
            if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535)
                return false;

            using var client = new TcpClient();
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            if (!connectTask.Wait(300))
            {
                client.Close();
                return false;
            }

            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            using var reader = new StreamReader(stream, new UTF8Encoding(false));

            writer.WriteLine("STATUS");
            var response = reader.ReadLine();
            return !string.IsNullOrWhiteSpace(response);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Sends current live variables and SCMT executable path to FormGenAgent via TCP socket.
    /// </summary>
    public static bool SyncStateToAgent(AMFormsCST.Core.Types.IPC.IpcLiveVariables? vars = null, string? scmtExecutablePath = null, ILogService? logger = null)
    {
        try
        {
            var portFilePath = GetPortFilePath();
            if (!File.Exists(portFilePath))
                return false;

            var portText = File.ReadAllText(portFilePath).Trim();
            if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535)
                return false;

            using var client = new TcpClient { ReceiveTimeout = IpcIoTimeoutMs, SendTimeout = IpcIoTimeoutMs };
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            if (!connectTask.Wait(IpcConnectTimeoutMs))
            {
                client.Close();
                return false;
            }

            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            using var reader = new StreamReader(stream, new UTF8Encoding(false));

            if (!string.IsNullOrWhiteSpace(scmtExecutablePath))
            {
                writer.WriteLine($"SCMT_PATH:{scmtExecutablePath}");
                var resp = reader.ReadLine();
                logger?.LogDebug($"FormGenAgent SCMT_PATH response: {resp}");
            }

            if (vars != null)
            {
                var dict = vars.ToDictionary();
                var json = System.Text.Json.JsonSerializer.Serialize(dict);
                writer.WriteLine($"VARS:{json}");
                var resp = reader.ReadLine();
                logger?.LogDebug($"FormGenAgent VARS response: {resp}");
            }

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"Failed to sync state to FormGenAgent: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Queries FormGenAgent for the currently open .formgen or .fga file path.
    /// </summary>
    public static string? GetActiveFormFromAgent(ILogService? logger = null)
    {
        try
        {
            var portFilePath = GetPortFilePath();
            if (!File.Exists(portFilePath))
                return null;

            var portText = File.ReadAllText(portFilePath).Trim();
            if (!int.TryParse(portText, out int port) || port <= 0 || port > 65535)
                return null;

            using var client = new TcpClient { ReceiveTimeout = IpcIoTimeoutMs, SendTimeout = IpcIoTimeoutMs };
            var connectTask = client.ConnectAsync("127.0.0.1", port);
            if (!connectTask.Wait(IpcConnectTimeoutMs))
            {
                client.Close();
                return null;
            }

            using var stream = client.GetStream();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
            using var reader = new StreamReader(stream, new UTF8Encoding(false));

            writer.WriteLine("GET_ACTIVE_FORM");
            var response = reader.ReadLine();
            if (!string.IsNullOrWhiteSpace(response))
            {
                return response.Trim();
            }

            return null;
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"Failed to get active form from FormGenAgent: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Resolves the path to NewFormGen.bat.
    /// </summary>
    public static string? ResolveFormGenBatchPath(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
        {
            var ext = Path.GetExtension(explicitPath);
            if (ext.Equals(".bat", StringComparison.OrdinalIgnoreCase) || ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
                return explicitPath;

            var dir = Path.GetDirectoryName(explicitPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var candidate = Path.Combine(dir, "NewFormGen.bat");
                if (File.Exists(candidate)) return candidate;
            }
        }

        // Check AppData default
        var defaultBat = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"NewFormGen\NewFormGen.bat");
        if (File.Exists(defaultBat))
            return defaultBat;

        return null;
    }

    /// <summary>
    /// Ensures FormGenAgent.jar is copied into the NewFormGen directory and NewFormGen.bat is configured with the agent.
    /// </summary>
    public static void EnsureAgentDeployedAndPatched(string batPath, ILogService? logger = null)
    {
        try
        {
            var dir = Path.GetDirectoryName(batPath);
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
                return;

            var ext = Path.GetExtension(batPath);
            if (!ext.Equals(".bat", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".cmd", StringComparison.OrdinalIgnoreCase))
                return;

            var agentDest = Path.Combine(dir, "FormGenAgent.jar");
            ExtractEmbeddedAgent(agentDest, logger);

            if (File.Exists(batPath))
            {
                var content = File.ReadAllText(batPath);
                bool modified = false;

                if (!content.Contains("FormGenAgent.jar", StringComparison.OrdinalIgnoreCase))
                {
                    content = content.Replace("com.automate.formgen.NewFormGen %*", "-javaagent:FormGenAgent.jar -Dformgen.file=\"%~f1\" com.automate.formgen.NewFormGen %*");
                    modified = true;
                }

                if (!content.Contains("cd /d \"%~dp0\"", StringComparison.OrdinalIgnoreCase))
                {
                    content = "cd /d \"%~dp0\"\r\n" + content;
                    modified = true;
                }

                if (modified)
                {
                    File.WriteAllText(batPath, content);
                    logger?.LogInfo($"Patched NewFormGen.bat at '{batPath}' with FormGenAgent.");
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"Failed to ensure agent deployed / bat patched: {ex.Message}");
        }
    }

    /// <summary>
    /// Restores NewFormGen.bat to its original vanilla configuration by removing the FormGenAgent javaagent parameter.
    /// </summary>
    public static void RestoreVanillaBatch(string batPath, ILogService? logger = null)
    {
        try
        {
            if (!File.Exists(batPath))
                return;

            var content = File.ReadAllText(batPath);
            if (content.Contains("FormGenAgent.jar", StringComparison.OrdinalIgnoreCase))
            {
                content = content.Replace("-javaagent:FormGenAgent.jar -Dformgen.file=\"%~f1\" ", "");
                content = content.Replace("-javaagent:FormGenAgent.jar -Dformgen.file=\"%~f1\"", "");
                content = content.Replace("-javaagent:FormGenAgent.jar ", "");
                File.WriteAllText(batPath, content);
                logger?.LogInfo($"Restored vanilla NewFormGen.bat at '{batPath}'.");
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"Failed to restore vanilla batch: {ex.Message}");
        }
    }

    private static void ExtractEmbeddedAgent(string destPath, ILogService? logger = null)
    {
        try
        {
            var assembly = typeof(FormGenLauncherHelper).Assembly;
            var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("FormGenAgent.jar", StringComparison.OrdinalIgnoreCase));
            if (resourceName != null)
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream != null)
                {
                    using var ms = new MemoryStream();
                    stream.CopyTo(ms);
                    var newBytes = ms.ToArray();

                    if (File.Exists(destPath))
                    {
                        try
                        {
                            var existingBytes = File.ReadAllBytes(destPath);
                            if (existingBytes.AsSpan().SequenceEqual(newBytes))
                                return;
                        }
                        catch { }
                    }

                    File.WriteAllBytes(destPath, newBytes);
                    logger?.LogInfo($"Extracted updated FormGenAgent.jar ({newBytes.Length} bytes) to '{destPath}'.");
                    return;
                }
            }

            var localTool = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Tools\FormGenAgent\FormGenAgent.jar");
            if (File.Exists(localTool))
            {
                var newBytes = File.ReadAllBytes(localTool);
                if (File.Exists(destPath))
                {
                    try
                    {
                        var existingBytes = File.ReadAllBytes(destPath);
                        if (existingBytes.AsSpan().SequenceEqual(newBytes))
                            return;
                    }
                    catch { }
                }

                File.WriteAllBytes(destPath, newBytes);
                logger?.LogInfo($"Copied updated FormGenAgent.jar from Tools to '{destPath}'.");
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"Could not extract FormGenAgent.jar (FormGen may currently have it locked; restart FormGen to apply agent updates): {ex.Message}");
        }
    }
}
