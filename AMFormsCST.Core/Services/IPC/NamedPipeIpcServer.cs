using AMFormsCST.Core.Types.IPC;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace AMFormsCST.Core.Services.IPC;

/// <summary>
/// Asynchronous Named Pipe server for local Inter-Process Communication.
/// </summary>
public class NamedPipeIpcServer : IAsyncDisposable
{
    public const string DefaultPipeName = "SoleraCMS_IPC";

    private readonly string _pipeName;
    private readonly Func<IpcMessage, Task<IpcResponse>> _messageHandler;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;
    private bool _isDisposed;

    public bool IsRunning => _listenerTask != null && !_listenerTask.IsCompleted;

    public NamedPipeIpcServer(Func<IpcMessage, Task<IpcResponse>> messageHandler, string pipeName = DefaultPipeName)
    {
        _messageHandler = messageHandler ?? throw new ArgumentNullException(nameof(messageHandler));
        _pipeName = pipeName;
    }

    private readonly ManualResetEventSlim _started = new(false);

    /// <summary>
    /// Starts the Named Pipe server listening loop in the background and waits until the pipe is ready.
    /// </summary>
    public void Start()
    {
        if (IsRunning) return;

        _started.Reset();
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
        _started.Wait(3000);
    }

    private NamedPipeServerStream? _currentServerStream;

    /// <summary>
    /// Stops the listening loop and closes the pipe.
    /// </summary>
    public async Task StopAsync()
    {
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            _cts.Cancel();
        }

        try
        {
            _currentServerStream?.Dispose();
        }
        catch { }

        if (_listenerTask != null)
        {
            try
            {
                await _listenerTask.WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch (OperationCanceledException) { }
            catch (TimeoutException) { }
            catch { }
            finally
            {
                _listenerTask = null;
            }
        }
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            NamedPipeServerStream? serverStream = null;
            try
            {
                serverStream = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.InOut,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                _currentServerStream = serverStream;
                _started.Set();

                using (ct.Register(() =>
                {
                    try { serverStream?.Dispose(); } catch { }
                }))
                {
                    await serverStream.WaitForConnectionAsync(ct);
                }

                _currentServerStream = null;

                // Handle client connection concurrently on thread pool
                _ = Task.Run(() => HandleClientAsync(serverStream, ct), ct);
            }
            catch (OperationCanceledException)
            {
                serverStream?.Dispose();
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception)
            {
                serverStream?.Dispose();
                if (ct.IsCancellationRequested) break;
                await Task.Delay(200, ct); // Brief pause before retry on transient error
            }
        }
    }

    private async Task HandleClientAsync(NamedPipeServerStream stream, CancellationToken ct)
    {
        using (stream)
        {
            try
            {
                var encoding = new UTF8Encoding(false);
                using var reader = new StreamReader(stream, encoding, leaveOpen: true);
                using var writer = new StreamWriter(stream, encoding, leaveOpen: true);

                var jsonLine = await reader.ReadLineAsync(ct);
                if (string.IsNullOrWhiteSpace(jsonLine)) return;

                IpcMessage? message;
                try
                {
                    message = JsonSerializer.Deserialize<IpcMessage>(jsonLine);
                }
                catch (Exception ex)
                {
                    var errResponse = IpcResponse.Fail($"Invalid JSON payload: {ex.Message}");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(errResponse));
                    await writer.FlushAsync(ct);
                    return;
                }

                if (message == null)
                {
                    var nullResponse = IpcResponse.Fail("Message payload was null.");
                    await writer.WriteLineAsync(JsonSerializer.Serialize(nullResponse));
                    await writer.FlushAsync(ct);
                    return;
                }

                IpcResponse response;
                try
                {
                    response = await _messageHandler(message);
                }
                catch (Exception handlerEx)
                {
                    response = IpcResponse.Fail($"Handler exception: {handlerEx.Message}");
                }

                var responseJson = JsonSerializer.Serialize(response);
                await writer.WriteLineAsync(responseJson);
                await writer.FlushAsync(ct);
            }
            catch (OperationCanceledException) { }
            catch { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        await StopAsync();
        _cts?.Dispose();
        _started.Dispose();
    }
}
