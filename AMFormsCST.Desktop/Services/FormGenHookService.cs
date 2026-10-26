using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.Views.Dialogs;
using Microsoft.Extensions.Hosting;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace AMFormsCST.Desktop.Services;

/// <summary>
/// Monitors right-click interactions inside the FormGen application using a low-level mouse hook.
/// Supports 3 interaction tiers:
/// 1. Optimistic integration.
/// 2. Companion Anchor Flyout on regular RMB (docked adjacent to native FormGen menu).
/// 3. Shift + RMB hotkey override (suppresses FormGen popup and directly shows Solera Quick-Paste).
/// </summary>
public class FormGenHookService : IHostedService, IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_RBUTTONDOWN = 0x0204;
    private const int VK_SHIFT = 0x10;
    private const uint GA_ROOT = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(POINT pt);

    [DllImport("user32.dll", ExactSpelling = true)]
    private static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private readonly IpcService _ipcService;
    private readonly ILogService _logger;
    private readonly ISupportTool? _supportTool;
    private readonly Dispatcher _dispatcher;
    private LowLevelMouseProc? _hookCallback;
    private IntPtr _hookId = IntPtr.Zero;
    private FormgenQuickPasteWindow? _quickPasteWindow;

    public FormGenHookService(IpcService ipcService, ILogService logger, ISupportTool? supportTool = null)
    {
        _ipcService = ipcService;
        _logger = logger;
        _supportTool = supportTool;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _hookCallback = HookProcedure;
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            var modHandle = GetModuleHandle(curModule?.ModuleName);

            _hookId = SetWindowsHookEx(WH_MOUSE_LL, _hookCallback, modHandle, 0);
            if (_hookId == IntPtr.Zero)
            {
                _logger.LogWarning("Failed to install FormGen low-level mouse hook.");
            }
            else
            {
                _logger.LogInfo("FormGen low-level mouse hook installed successfully.");
            }

            _ = Task.Run(() =>
            {
                try
                {
                    bool isPatched = _supportTool?.Settings?.UserSettings?.IsFormGenPatched ?? FormGenLauncherHelper.IsPatchingAllowed;
                    if (isPatched)
                    {
                        var vars = _ipcService.GetLiveVariables();
                        FormGenLauncherHelper.SyncStateToAgent(vars, Environment.ProcessPath, _logger);
                    }
                }
                catch {}
            });
        }
        catch (Exception ex)
        {
            _logger.LogError("Exception while installing FormGen mouse hook.", ex);
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    private IntPtr HookProcedure(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_RBUTTONDOWN)
        {
            try
            {
                var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                // 0-footprint check: If FormGen is unpatched, never intercept mouse events on FormGen
                bool isFormGen = IsFormGenWindowAt(hookStruct.pt, out _);
                bool isPatched = _supportTool?.Settings?.UserSettings?.IsFormGenPatched ?? FormGenLauncherHelper.IsPatchingAllowed;
                if (isFormGen && !isPatched)
                {
                    return CallNextHookEx(_hookId, nCode, wParam, lParam);
                }

                bool isShift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

                if (isShift)
                {
                    var isFlyoutEnabled = App.GetService<ISupportTool>()?.Settings?.UserSettings?.EnableShiftRightClickFlyout ?? true;
                    if (!isFlyoutEnabled)
                    {
                        return CallNextHookEx(_hookId, nCode, wParam, lParam);
                    }

                    // Layer 3: Shift + RMB hotkey override across all external apps (Notepad, FormGen, VS Code, etc.)
                    // Directly display Solera Quick-Paste and Parse flyout at cursor
                    if (IsValidTargetWindowAt(hookStruct.pt, out IntPtr rootHwnd))
                    {
                        _dispatcher.BeginInvoke(() => ShowQuickPaste(hookStruct.pt, rootHwnd, offsetForCompanion: false));
                        return (IntPtr)1; // Suppress native RMB event
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error in FormGen mouse hook callback.", ex);
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool IsValidTargetWindowAt(POINT pt, out IntPtr rootHwnd)
    {
        rootHwnd = IntPtr.Zero;
        var hwnd = WindowFromPoint(pt);
        if (hwnd == IntPtr.Zero) return false;

        rootHwnd = GetAncestor(hwnd, GA_ROOT);
        if (rootHwnd == IntPtr.Zero) rootHwnd = hwnd;

        GetWindowThreadProcessId(rootHwnd, out uint processId);
        if (processId == 0) return false;

        try
        {
            if (processId == Environment.ProcessId) return false;

            using var proc = Process.GetProcessById((int)processId);
            var procName = proc.ProcessName;

            // Never intercept Windows Explorer or desktop shell
            if (procName.Equals("explorer", StringComparison.OrdinalIgnoreCase) ||
                procName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
                procName.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsFormGenWindowAt(POINT pt, out IntPtr rootHwnd)
    {
        if (!IsValidTargetWindowAt(pt, out rootHwnd)) return false;

        GetWindowThreadProcessId(rootHwnd, out uint processId);
        if (processId == 0) return false;

        try
        {
            using var proc = Process.GetProcessById((int)processId);
            var procName = proc.ProcessName;

            var classBuilder = new StringBuilder(256);
            GetClassName(rootHwnd, classBuilder, classBuilder.Capacity);
            var className = classBuilder.ToString();

            var titleBuilder = new StringBuilder(256);
            GetWindowText(rootHwnd, titleBuilder, titleBuilder.Capacity);
            var title = titleBuilder.ToString();

            bool isJavaProcess = procName.Contains("java", StringComparison.OrdinalIgnoreCase);
            bool isSunAwtClass = className.StartsWith("SunAwt", StringComparison.OrdinalIgnoreCase);
            bool isFormgenTitle = title.Contains("Formgen", StringComparison.OrdinalIgnoreCase) ||
                                  title.Contains("Form Generator", StringComparison.OrdinalIgnoreCase);

            // Must be a Java process or SunAwt window AND relate to FormGen
            return (isJavaProcess || isSunAwtClass) && (isFormgenTitle || isSunAwtClass);
        }
        catch
        {
            return false;
        }
    }

    private void ShowQuickPaste(POINT pt, IntPtr targetHwnd, bool offsetForCompanion)
    {
        try
        {
            _quickPasteWindow ??= new FormgenQuickPasteWindow();

            var vars = _ipcService.GetLiveVariables();
            _quickPasteWindow.ShowAt(new Point(pt.x, pt.y), targetHwnd, vars, offsetForCompanion);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to show FormGen quick paste window.", ex);
        }
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
            _logger.LogInfo("FormGen mouse hook removed.");
        }

        if (_quickPasteWindow != null)
        {
            _dispatcher.Invoke(() =>
            {
                _quickPasteWindow.Close();
                _quickPasteWindow = null;
            });
        }
    }
}

