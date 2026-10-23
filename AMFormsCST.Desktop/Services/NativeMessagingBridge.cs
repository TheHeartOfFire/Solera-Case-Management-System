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
        using var stdin = Console.OpenStandardInput();
        using var stdout = Console.OpenStandardOutput();

        byte[] lengthBuffer = new byte[4];

        while (true)
        {
            int bytesRead = await ReadExactAsync(stdin, lengthBuffer, 0, 4);
            if (bytesRead < 4) break; // Stream ended

            int length = BitConverter.ToInt32(lengthBuffer, 0);
            if (length <= 0 || length > MaxMessageSize) break;

            byte[] messageBuffer = new byte[length];
            int read = await ReadExactAsync(stdin, messageBuffer, 0, length);
            if (read < length) break;

            string json = Encoding.UTF8.GetString(messageBuffer);

            IpcMessage? message = null;
            try
            {
                message = JsonSerializer.Deserialize<IpcMessage>(json);
            }
            catch (Exception ex)
            {
                var errResponse = IpcResponse.Fail($"Invalid native message format: {ex.Message}");
                await WriteResponseAsync(stdout, errResponse);
                continue;
            }

            message ??= new IpcMessage { Action = IpcActions.Ping };

            var response = await NamedPipeIpcClient.SendMessageAsync(message, timeoutMs: 5000);
            await WriteResponseAsync(stdout, response);
        }
    }

    private static async Task WriteResponseAsync(Stream stdout, IpcResponse response)
    {
        string responseJson = JsonSerializer.Serialize(response);
        byte[] responseBytes = Encoding.UTF8.GetBytes(responseJson);
        byte[] responseLengthBytes = BitConverter.GetBytes(responseBytes.Length);

        await stdout.WriteAsync(responseLengthBytes.AsMemory(0, 4));
        await stdout.WriteAsync(responseBytes.AsMemory(0, responseBytes.Length));
        await stdout.FlushAsync();
    }

    private static async Task<int> ReadExactAsync(Stream stream, byte[] buffer, int offset, int count)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead));
            if (read == 0) return totalRead;
            totalRead += read;
        }
        return totalRead;
    }
}

