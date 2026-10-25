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
    private const string ConfigFileName = "formgen_agent.properties";
    private const string SessionFormsFileName = "formgen_session_forms.txt";
    private const string LogFileName = "formgen_agent.log";

    /// <summary>
    /// Tracks whether FormGen patching is currently enabled in user settings.
    /// When false (Unpatched), OpenInFormGen will launch vanilla FormGen without deploying or injecting FormGenAgent.jar.
    /// </summary>
    public static bool IsPatchingAllowed { get; set; } = true;

    /// <summary>
    /// Test seam: lets a test (and only the async flow it starts) point the helper at a private directory,
    /// so parallel tests and background sync calls don't fight over the shared %TEMP% port file.
    /// </summary>
    internal static readonly System.Threading.AsyncLocal<string?> PortFileDirectoryOverride = new();

    private static string GetTempDir() => PortFileDirectoryOverride.Value ?? Path.GetTempPath();

    private static string GetPortFilePath() => Path.Combine(GetTempDir(), PortFileName);
    public static string GetConfigFilePath() => Path.Combine(GetTempDir(), ConfigFileName);
    public static string GetSessionFormsFilePath() => Path.Combine(GetTempDir(), SessionFormsFileName);
    public static string GetLogFilePath() => Path.Combine(GetTempDir(), LogFileName);

    /// <summary>
    /// Writes the user's individual FormGen feature toggle states to %TEMP%\formgen_agent.properties
    /// so FormGenAgent can read them at startup and live at runtime.
    /// </summary>
    public static void WriteAgentConfig(AMFormsCST.Core.Interfaces.UserSettings.IUserSettings? userSettings, ILogService? logger = null)
    {
        if (userSettings == null) return;
        IsPatchingAllowed = userSettings.IsFormGenPatched;

        if (!userSettings.IsFormGenPatched)
            return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"FormGenPreserveUdfTab={userSettings.FormGenPreserveUdfTab.ToString().ToLowerInvariant()}");
            sb.AppendLine($"FormGenCanvasPasteField={userSettings.FormGenCanvasPasteField.ToString().ToLowerInvariant()}");
            sb.AppendLine($"FormGenAlreadyOpenToast={userSettings.FormGenAlreadyOpenToast.ToString().ToLowerInvariant()}");
            sb.AppendLine($"FormGenContextMenus={userSettings.FormGenContextMenus.ToString().ToLowerInvariant()}");
            sb.AppendLine($"FormGenEditorUndoRedo={userSettings.FormGenEditorUndoRedo.ToString().ToLowerInvariant()}");
            sb.AppendLine($"FormGenDialogAutoFill={userSettings.FormGenDialogAutoFill.ToString().ToLowerInvariant()}");
            sb.AppendLine($"FormGenAutoRestoreForms={userSettings.FormGenAutoRestoreForms.ToString().ToLowerInvariant()}");

            File.WriteAllText(GetConfigFilePath(), sb.ToString(), new UTF8Encoding(false));
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"Failed to write FormGenAgent config file: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens the specified .formgen or .fga file in FormGen.
    /// If FormGen is already running with FormGenAgent, the file is sent over localhost TCP in milliseconds.
    /// If not running, FormGen is launched (with the agent if IsPatchingAllowed is true, or vanilla if unpatched).
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

        // 1. Try IPC socket to existing FormGen instance (only when patched)
        if (IsPatchingAllowed && TrySendViaIpc(cleanPath, logger))
        {
            logger?.LogInfo($"Successfully opened '{cleanPath}' in running FormGen via IPC socket.");
            return true;
        }

        // 2. If not running, launch NewFormGen.bat with file argument
        var batPath = ResolveFormGenBatchPath(formgenBatPath);
        if (!string.IsNullOrWhiteSpace(batPath) && File.Exists(batPath))
        {
            if (IsPatchingAllowed)
            {
                EnsureAgentDeployedAndPatched(batPath, logger);
            }

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
        if (IsPatchingAllowed && TryPushViaIpc(cleanPath, uuid, logger))
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
    /// Checks whether NewFormGen.bat is currently patched with FormGenAgent.jar and the jar exists in the FormGen directory.
    /// </summary>
    public static bool IsBatchPatched(string? explicitBatPath = null)
    {
        try
        {
            var batPath = ResolveFormGenBatchPath(explicitBatPath);
            if (string.IsNullOrWhiteSpace(batPath) || !File.Exists(batPath))
                return false;

            var dir = Path.GetDirectoryName(batPath);
            if (string.IsNullOrWhiteSpace(dir))
                return false;

            var jarPath = Path.Combine(dir, "FormGenAgent.jar");
            if (!File.Exists(jarPath))
                return false;

            var content = File.ReadAllText(batPath);
            return content.Contains("FormGenAgent.jar", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks whether any FormGen JVM process is currently running (either via Agent IPC or window title).
    /// </summary>
    public static bool IsFormGenProcessRunning()
    {
        if (IsAgentActive())
            return true;

        try
        {
            foreach (var procName in new[] { "javaw", "java" })
            {
                foreach (var proc in Process.GetProcessesByName(procName))
                {
                    try
                    {
                        var title = proc.MainWindowTitle;
                        if (!string.IsNullOrWhiteSpace(title) &&
                            (title.Contains("FormGen", StringComparison.OrdinalIgnoreCase) ||
                             title.Contains("Form Generator", StringComparison.OrdinalIgnoreCase)))
                        {
                            return true;
                        }
                    }
                    catch { }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
        }
        catch { }

        return false;
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
    /// Queries FormGenAgent for all currently open .formgen / .fga file paths across all tabs,
    /// falling back to the persisted session forms file or active form if needed.
    /// </summary>
    public static List<string> GetOpenFormsFromAgent(ILogService? logger = null)
    {
        var results = new List<string>();
        try
        {
            var portFilePath = GetPortFilePath();
            if (File.Exists(portFilePath))
            {
                var portText = File.ReadAllText(portFilePath).Trim();
                if (int.TryParse(portText, out int port) && port > 0 && port <= 65535)
                {
                    using var client = new TcpClient { ReceiveTimeout = IpcIoTimeoutMs, SendTimeout = IpcIoTimeoutMs };
                    var connectTask = client.ConnectAsync("127.0.0.1", port);
                    if (connectTask.Wait(IpcConnectTimeoutMs))
                    {
                        using var stream = client.GetStream();
                        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                        using var reader = new StreamReader(stream, new UTF8Encoding(false));

                        writer.WriteLine("GET_OPEN_FORMS");
                        var response = reader.ReadLine();
                        if (!string.IsNullOrWhiteSpace(response))
                        {
                            foreach (var part in response.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            {
                                var clean = part.Trim().Trim('"');
                                if (File.Exists(clean) && !results.Contains(clean, StringComparer.OrdinalIgnoreCase))
                                {
                                    results.Add(clean);
                                }
                            }
                        }
                    }
                    else
                    {
                        client.Close();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"Failed to query GET_OPEN_FORMS from FormGenAgent: {ex.Message}");
        }

        if (results.Count == 0)
        {
            var active = GetActiveFormFromAgent(logger);
            if (!string.IsNullOrWhiteSpace(active) && File.Exists(active))
            {
                results.Add(active);
            }
        }

        if (results.Count == 0)
        {
            try
            {
                var sessionFile = GetSessionFormsFilePath();
                if (File.Exists(sessionFile))
                {
                    foreach (var line in File.ReadAllLines(sessionFile))
                    {
                        var clean = line.Trim().Trim('"');
                        if (File.Exists(clean) && !results.Contains(clean, StringComparer.OrdinalIgnoreCase))
                        {
                            results.Add(clean);
                        }
                    }
                }
            }
            catch { }
        }

        return results;
    }

    /// <summary>
    /// Gracefully requests FormGen shutdown or terminates running FormGen JVM instances and waits for file locks to release.
    /// </summary>
    public static void CloseFormGenProcesses(ILogService? logger = null)
    {
        // 1. Ask FormGenAgent to close gracefully via IPC if listening
        try
        {
            var portFilePath = GetPortFilePath();
            if (File.Exists(portFilePath))
            {
                var portText = File.ReadAllText(portFilePath).Trim();
                if (int.TryParse(portText, out int port) && port > 0 && port <= 65535)
                {
                    using var client = new TcpClient { ReceiveTimeout = 1000, SendTimeout = 1000 };
                    if (client.ConnectAsync("127.0.0.1", port).Wait(500))
                    {
                        using var stream = client.GetStream();
                        using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
                        writer.WriteLine("SHUTDOWN");
                    }
                }
            }
        }
        catch { }

        // 2. Also find any javaw/java processes with FormGen window title and close/kill them
        try
        {
            foreach (var procName in new[] { "javaw", "java" })
            {
                foreach (var proc in Process.GetProcessesByName(procName))
                {
                    try
                    {
                        var title = proc.MainWindowTitle;
                        if (!string.IsNullOrWhiteSpace(title) &&
                            (title.Contains("FormGen", StringComparison.OrdinalIgnoreCase) ||
                             title.Contains("Form Generator", StringComparison.OrdinalIgnoreCase)))
                        {
                            logger?.LogInfo($"Closing FormGen process PID {proc.Id} ('{title}')...");
                            proc.Kill();
                            proc.WaitForExit(3000);
                        }
                    }
                    catch { }
                    finally
                    {
                        proc.Dispose();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger?.LogWarning($"Error while closing FormGen processes: {ex.Message}");
        }

        try
        {
            var p = GetPortFilePath();
            if (File.Exists(p)) File.Delete(p);
        }
        catch { }
    }

    /// <summary>
    /// Deploys FormGenAgent.jar, patches NewFormGen.bat, closes any running FormGen instance,
    /// and relaunches FormGen restoring all previously open forms.
    /// </summary>
    public static bool PatchAndReloadFormGen(
        string? explicitBatPath,
        AMFormsCST.Core.Interfaces.UserSettings.IUserSettings? userSettings,
        IReadOnlyList<string>? openFormsToRestore = null,
        bool relaunchFormGen = true,
        ILogService? logger = null)
    {
        try
        {
            var batPath = ResolveFormGenBatchPath(explicitBatPath);
            if (string.IsNullOrWhiteSpace(batPath) || !File.Exists(batPath))
            {
                logger?.LogWarning("PatchAndReloadFormGen: Could not locate NewFormGen.bat.");
                return false;
            }

            var formsToReopen = openFormsToRestore?.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? GetOpenFormsFromAgent(logger);

            bool wasRunning = IsFormGenProcessRunning();
            if (wasRunning)
            {
                CloseFormGenProcesses(logger);
                System.Threading.Thread.Sleep(500);
            }

            IsPatchingAllowed = true;
            if (userSettings != null)
            {
                userSettings.IsFormGenPatched = true;
                WriteAgentConfig(userSettings, logger);
            }

            // Persist open forms to session file so FormGenAgent restores all of them on startup
            try
            {
                File.WriteAllLines(GetSessionFormsFilePath(), formsToReopen, new UTF8Encoding(false));
            }
            catch { }

            EnsureAgentDeployedAndPatched(batPath, logger);

            if (relaunchFormGen)
            {
                var dir = Path.GetDirectoryName(batPath);
                var firstForm = formsToReopen.FirstOrDefault();
                var startInfo = new ProcessStartInfo
                {
                    FileName = batPath,
                    Arguments = !string.IsNullOrWhiteSpace(firstForm) ? $"\"{firstForm}\"" : string.Empty,
                    WorkingDirectory = dir,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                logger?.LogInfo($"Relaunched patched FormGen via '{batPath}' (restoring {formsToReopen.Count} open form(s)).");
            }

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError("Error in PatchAndReloadFormGen.", ex);
            return false;
        }
    }

    /// <summary>
    /// Removes all traces of FormGen modification (restores vanilla NewFormGen.bat, deletes FormGenAgent.jar,
    /// removes agent temp files), closes FormGen if running, and relaunches vanilla FormGen restoring open forms.
    /// </summary>
    public static bool UnpatchAndReloadFormGen(
        string? explicitBatPath,
        AMFormsCST.Core.Interfaces.UserSettings.IUserSettings? userSettings,
        IReadOnlyList<string>? openFormsToRestore = null,
        bool relaunchFormGen = true,
        ILogService? logger = null)
    {
        try
        {
            var batPath = ResolveFormGenBatchPath(explicitBatPath);
            if (string.IsNullOrWhiteSpace(batPath) || !File.Exists(batPath))
            {
                logger?.LogWarning("UnpatchAndReloadFormGen: Could not locate NewFormGen.bat.");
                return false;
            }

            var formsToReopen = openFormsToRestore?.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList()
                ?? GetOpenFormsFromAgent(logger);

            bool wasRunning = IsFormGenProcessRunning();
            if (wasRunning)
            {
                CloseFormGenProcesses(logger);
                System.Threading.Thread.Sleep(600);
            }

            IsPatchingAllowed = false;
            if (userSettings != null)
            {
                userSettings.IsFormGenPatched = false;
            }

            // 1. Restore NewFormGen.bat to clean vanilla state
            RestoreVanillaBatch(batPath, logger);

            // 2. Delete FormGenAgent.jar from the FormGen folder (with retry in case JVM is releasing file handle)
            var dir = Path.GetDirectoryName(batPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var agentJarPath = Path.Combine(dir, "FormGenAgent.jar");
                for (int i = 0; i < 5 && File.Exists(agentJarPath); i++)
                {
                    try
                    {
                        File.Delete(agentJarPath);
                        logger?.LogInfo($"Deleted '{agentJarPath}'.");
                        break;
                    }
                    catch
                    {
                        System.Threading.Thread.Sleep(300);
                    }
                }
            }

            // 3. Remove all agent temp files (port, config, log, session forms)
            foreach (var tempFile in new[] { GetPortFilePath(), GetConfigFilePath(), GetLogFilePath(), GetSessionFormsFilePath() })
            {
                try
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
                catch { }
            }

            // 4. Relaunch vanilla FormGen if requested
            if (relaunchFormGen)
            {
                var firstForm = formsToReopen.FirstOrDefault();
                var startInfo = new ProcessStartInfo
                {
                    FileName = batPath,
                    Arguments = !string.IsNullOrWhiteSpace(firstForm) ? $"\"{firstForm}\"" : string.Empty,
                    WorkingDirectory = dir,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                logger?.LogInfo($"Relaunched vanilla FormGen via '{batPath}'.");
            }

            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError("Error in UnpatchAndReloadFormGen.", ex);
            return false;
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
    /// Restores NewFormGen.bat to its original vanilla configuration by removing the FormGenAgent javaagent parameter and cd prefix.
    /// </summary>
    public static void RestoreVanillaBatch(string batPath, ILogService? logger = null)
    {
        try
        {
            if (!File.Exists(batPath))
                return;

            var content = File.ReadAllText(batPath);
            var updated = content
                .Replace("cd /d \"%~dp0\"\r\n", "")
                .Replace("cd /d \"%~dp0\"\n", "")
                .Replace("-javaagent:FormGenAgent.jar -Dformgen.file=\"%~f1\" ", "")
                .Replace("-javaagent:FormGenAgent.jar -Dformgen.file=\"%~f1\"", "")
                .Replace("-javaagent:FormGenAgent.jar ", "")
                .Replace("-javaagent:FormGenAgent.jar", "");

            if (!string.Equals(content, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(batPath, updated);
                logger?.LogInfo($"Restored vanilla NewFormGen.bat at '{batPath}'.");
            }

            var dir = Path.GetDirectoryName(batPath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                var jarPath = Path.Combine(dir, "FormGenAgent.jar");
                if (File.Exists(jarPath))
                {
                    try
                    {
                        File.Delete(jarPath);
                        logger?.LogInfo($"Removed FormGenAgent.jar from '{dir}'.");
                    }
                    catch { }
                }
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
