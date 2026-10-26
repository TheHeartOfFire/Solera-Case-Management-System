using AMFormsCST.Core.Services.IPC;
using AMFormsCST.Core.Types.IPC;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AMFormsCST.Desktop.Services;

/// <summary>
/// Implements the Chrome/Edge Native Messaging protocol over standard input/output.
/// Bridges browser extension requests directly to the local Solera CMS Named Pipe IPC server.
/// </summary>
public static class NativeMessagingBridge
{
    private const int MaxMessageSize = 1024 * 1024; // 1 MB per Chromium spec

    public static async Task RunAsync()
    {
        try
        {
            LogBridge($"NativeMessagingBridge started. PID={Environment.ProcessId}, Args=[{string.Join(", ", Environment.GetCommandLineArgs())}]");

            using var stdin = Console.OpenStandardInput();
            using var stdout = Console.OpenStandardOutput();

            byte[] lengthBuffer = new byte[4];

            while (true)
            {
                int bytesRead = await ReadExactAsync(stdin, lengthBuffer, 0, 4).ConfigureAwait(false);
                if (bytesRead < 4)
                {
                    LogBridge($"stdin closed (bytesRead={bytesRead}). Exiting loop.");
                    break; // Stream ended
                }

                int length = BitConverter.ToInt32(lengthBuffer, 0);
                if (length <= 0 || length > MaxMessageSize)
                {
                    LogBridge($"Invalid message length={length}. Exiting loop.");
                    break;
                }

                byte[] messageBuffer = new byte[length];
                int read = await ReadExactAsync(stdin, messageBuffer, 0, length).ConfigureAwait(false);
                if (read < length)
                {
                    LogBridge($"Incomplete message body read ({read}/{length}). Exiting loop.");
                    break;
                }

                string json = Encoding.UTF8.GetString(messageBuffer);
                LogBridge($"Received message ({length} bytes): {json}");

                IpcMessage? message = null;
                try
                {
                    message = JsonSerializer.Deserialize<IpcMessage>(json);
                }
                catch (Exception ex)
                {
                    LogBridge($"JSON deserialize error: {ex.Message}");
                    var errResponse = IpcResponse.Fail($"Invalid native message format: {ex.Message}");
                    await WriteResponseAsync(stdout, errResponse).ConfigureAwait(false);
                    continue;
                }

                message ??= new IpcMessage { Action = IpcActions.Ping };

                var response = await NamedPipeIpcClient.SendMessageAsync(message, timeoutMs: 2500).ConfigureAwait(false);
                if (!response.Success &&
                    string.Equals(message.Target, "SoleraCMS", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(Environment.GetEnvironmentVariable("SOLERA_NO_AUTOLAUNCH"), "1", StringComparison.OrdinalIgnoreCase))
                {
                    LogBridge($"Initial pipe send failed ({response.Message}). Attempting to launch SoleraCMS desktop app...");
                    if (TryLaunchDesktopApp())
                    {
                        for (int attempt = 0; attempt < 12; attempt++)
                        {
                            await Task.Delay(500).ConfigureAwait(false);
                            response = await NamedPipeIpcClient.SendMessageAsync(message, timeoutMs: 1500).ConfigureAwait(false);
                            if (response.Success)
                                break;
                        }
                    }
                }

                LogBridge($"Sending response: Success={response.Success}, Message={response.Message}");
                await WriteResponseAsync(stdout, response).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogBridge($"Unhandled exception in NativeMessagingBridge: {ex}");
        }
    }

    private static void LogBridge(string text)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "native_messaging.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, $"[{DateTime.Now:O}] {text}{Environment.NewLine}");
        }
        catch
        {
            // Never write to stdout/stderr or throw from diagnostic logging
        }
    }

    private static bool TryLaunchDesktopApp()
    {
        try
        {
            var exePath = BrowserIntegrationService.ResolveDesktopExecutablePath();
            if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
                return false;

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath),
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task WriteResponseAsync(Stream stdout, IpcResponse response)
    {
        string responseJson = JsonSerializer.Serialize(response);
        byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
        byte[] responseLengthBytes = BitConverter.GetBytes(responseBytes.Length);

        await stdout.WriteAsync(responseLengthBytes.AsMemory(0, 4)).ConfigureAwait(false);
        await stdout.WriteAsync(responseBytes.AsMemory(0, responseBytes.Length)).ConfigureAwait(false);
        await stdout.FlushAsync().ConfigureAwait(false);
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead)).ConfigureAwait(false);
            if (read == 0) return totalRead;
            totalRead += read;
        }
        return totalRead;
    }
}

