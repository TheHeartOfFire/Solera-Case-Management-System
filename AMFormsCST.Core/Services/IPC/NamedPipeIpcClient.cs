using AMFormsCST.Core.Types.IPC;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace AMFormsCST.Core.Services.IPC;

/// <summary>
/// Client helper for sending IPC messages to the Solera CMS or Citadel Named Pipe.
/// </summary>
public static class NamedPipeIpcClient
{
    /// <summary>
    /// Sends an IPC message to a Named Pipe and waits for the response.
    /// </summary>
    public static async Task<IpcResponse> SendMessageAsync(
        IpcMessage message,
        string pipeName = NamedPipeIpcServer.DefaultPipeName,
        int timeoutMs = 3000,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var clientStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await clientStream.ConnectAsync(timeoutMs, linkedCts.Token)
                .WaitAsync(TimeSpan.FromMilliseconds(timeoutMs), linkedCts.Token)
                .ConfigureAwait(false);

            var encoding = new UTF8Encoding(false);
            using var reader = new StreamReader(clientStream, encoding, leaveOpen: true);
            using var writer = new StreamWriter(clientStream, encoding, leaveOpen: true);

            var messageJson = JsonSerializer.Serialize(message);
            await writer.WriteLineAsync(messageJson.AsMemory(), linkedCts.Token).ConfigureAwait(false);
            await writer.FlushAsync(linkedCts.Token).ConfigureAwait(false);

            var responseLine = await reader.ReadLineAsync(linkedCts.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseLine))
            {
                return IpcResponse.Fail("Empty response from IPC server.");
            }

            var response = JsonSerializer.Deserialize<IpcResponse>(responseLine);
            return response ?? IpcResponse.Fail("Null response payload from IPC server.");
        }
        catch (TimeoutException)
        {
            return IpcResponse.Fail($"IPC connection timed out after {timeoutMs}ms.");
        }
        catch (OperationCanceledException)
        {
            return IpcResponse.Fail("IPC connection cancelled or timed out.");
        }
        catch (Exception ex)
        {
            return IpcResponse.Fail($"IPC Client error: {ex.Message}");
        }
    }
}
