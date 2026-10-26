#if DEBUG
using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace AMFormsCST.Desktop.Services;

/// <summary>
/// Local-only diagnostic snapshot writer compiled strictly in DEBUG builds (#if DEBUG) and active only when a debugger is attached.
/// Opens zero network ports or listeners so corporate firewall/EDR tools are never triggered.
/// Periodically writes a combined diagnostic bundle (SCMT state + FormGenAgent log tail + SCMT log tail)
/// to Desktop\MouseWithoutBorders\scmt_debug_bundle.txt (if that folder exists) for easy 1-file copy across machines.
/// </summary>
public sealed class RemoteDebugBridgeService : IHostedService, IDisposable
{
    private readonly IpcService _ipcService;
    private readonly ILogService _logger;
    private CancellationTokenSource? _cts;

    public RemoteDebugBridgeService(IpcService ipcService, ILogService logger)
    {
        _ipcService = ipcService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!Debugger.IsAttached)
            return Task.CompletedTask;

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => SnapshotLoopAsync(_cts.Token));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        try { _cts?.Cancel(); } catch { }
        _cts?.Dispose();
    }

    private async Task SnapshotLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await WriteLocalDiagnosticBundleAsync();
            }
            catch
            {
                // Ignore transient file access errors during debug snapshot
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task WriteLocalDiagnosticBundleAsync()
    {
        if (!Debugger.IsAttached)
            return;

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var mwbDir = Path.Combine(desktop, "MouseWithoutBorders");
        var targetDir = Directory.Exists(mwbDir) ? mwbDir : Path.GetTempPath();
        var bundlePath = Path.Combine(targetDir, "scmt_debug_bundle.txt");

        var portFilePath = Path.Combine(Path.GetTempPath(), "formgen_agent.port");
        bool portFileExists = File.Exists(portFilePath);
        string portText = portFileExists ? File.ReadAllText(portFilePath).Trim() : "(missing)";

        object? formgenVmState = null;
        if (Application.Current?.Dispatcher != null)
        {
            formgenVmState = await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var vm = FormgenUtilitiesViewModel.ActiveInstance;
                if (vm == null) return null;
                return new
                {
                    filePath = vm.FilePath,
                    formTitle = vm.FormTitle,
                    uuid = vm.Uuid,
                    isFileLoaded = vm.IsFileLoaded,
                    hasChanged = vm.HasChanged,
                    selectedNodeHeader = vm.SelectedNode?.Header,
                    isSaving = vm.IsSaving,
                    saveButtonText = vm.SaveButtonText,
                    isPushing = vm.IsPushing,
                    pushButtonText = vm.PushButtonText
                };
            });
        }

        var stateObj = new
        {
            timestampUtc = DateTime.UtcNow.ToString("O"),
            machineName = Environment.MachineName,
            processId = Environment.ProcessId,
            debuggerAttached = Debugger.IsAttached,
            agentPortFileExists = portFileExists,
            agentPort = portText,
            formgenUtilities = formgenVmState,
            liveVariables = _ipcService.GetLiveVariables().ToDictionary()
        };

        var sb = new StringBuilder();
        sb.AppendLine("=== SCMT DEBUG SNAPSHOT ===");
        sb.AppendLine(JsonSerializer.Serialize(stateObj, new JsonSerializerOptions { WriteIndented = true }));
        sb.AppendLine();
        sb.AppendLine("=== FORMGEN AGENT LOG (LAST 200 LINES) ===");
        sb.AppendLine(ReadTailLines(Path.Combine(Path.GetTempPath(), "formgen_agent.log"), 200));
        sb.AppendLine();
        sb.AppendLine("=== SCMT APP LOG (LAST 100 LINES) ===");
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        var latestLog = Directory.Exists(logDir)
            ? Directory.GetFiles(logDir, "app*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()
            : null;
        sb.AppendLine(latestLog != null ? ReadTailLines(latestLog, 100) : "(no log file)");

        await File.WriteAllTextAsync(bundlePath, sb.ToString(), Encoding.UTF8);
    }

    private static string ReadTailLines(string filePath, int lineCount)
    {
        if (!File.Exists(filePath))
            return $"File not found: {filePath}";

        try
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8);
            var allLines = new List<string>();
            while (!reader.EndOfStream)
            {
                allLines.Add(reader.ReadLine() ?? string.Empty);
            }

            int skip = Math.Max(0, allLines.Count - lineCount);
            return string.Join(Environment.NewLine, allLines.Skip(skip));
        }
        catch (Exception ex)
        {
            return $"Error reading '{filePath}': {ex.Message}";
        }
    }
}
#endif

