using AMFormsCST.Core.Types.IPC;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AMFormsCST.Desktop.Views.Dialogs;

public class QuickPasteItemModel
{
    public string BadgeText { get; set; } = string.Empty;
    public Brush BadgeBackground { get; set; } = Brushes.Transparent;
    public Brush BadgeForeground { get; set; } = Brushes.White;
    public string DisplayText { get; set; } = string.Empty;
    public string ValueToPaste { get; set; } = string.Empty;
    public string ShortcutKey { get; set; } = string.Empty;
}

public partial class FormgenQuickPasteWindow : Window
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private IntPtr _targetHwnd;
    private readonly List<QuickPasteItemModel> _currentItems = [];

    private static readonly Brush ServerBg = new SolidColorBrush(Color.FromRgb(0x1B, 0x3A, 0x57));
    private static readonly Brush ServerFg = new SolidColorBrush(Color.FromRgb(0x64, 0xB5, 0xF6));

    private static readonly Brush CompanyBg = new SolidColorBrush(Color.FromRgb(0x1B, 0x45, 0x27));
    private static readonly Brush CompanyFg = new SolidColorBrush(Color.FromRgb(0x81, 0xC7, 0x84));

    private static readonly Brush ContactBg = new SolidColorBrush(Color.FromRgb(0x4A, 0x35, 0x15));
    private static readonly Brush ContactFg = new SolidColorBrush(Color.FromRgb(0xFF, 0xB7, 0x4D));

    private static readonly Brush TemplateBg = new SolidColorBrush(Color.FromRgb(0x3D, 0x23, 0x52));
    private static readonly Brush TemplateFg = new SolidColorBrush(Color.FromRgb(0xBA, 0x68, 0xC8));

    public FormgenQuickPasteWindow()
    {
        InitializeComponent();
    }

    public void ShowAt(Point screenPoint, IntPtr targetHwnd, IpcLiveVariables vars, bool offsetForCompanion = false)
    {
        _targetHwnd = targetHwnd;
        _currentItems.Clear();

        int shortcutIndex = 1;

        if (!string.IsNullOrWhiteSpace(vars.ServerId))
        {
            _currentItems.Add(new QuickPasteItemModel
            {
                BadgeText = "SERVER",
                BadgeBackground = ServerBg,
                BadgeForeground = ServerFg,
                DisplayText = vars.ServerId,
                ValueToPaste = vars.ServerId,
                ShortcutKey = $"{shortcutIndex++}"
            });
        }

        if (!string.IsNullOrWhiteSpace(vars.PrimaryCompanyNumber))
        {
            _currentItems.Add(new QuickPasteItemModel
            {
                BadgeText = "CO #",
                BadgeBackground = CompanyBg,
                BadgeForeground = CompanyFg,
                DisplayText = $"Company {vars.PrimaryCompanyNumber}",
                ValueToPaste = vars.PrimaryCompanyNumber,
                ShortcutKey = $"{shortcutIndex++}"
            });
        }

        foreach (var co in vars.CompanyNumbers)
        {
            if (co != vars.PrimaryCompanyNumber && !string.IsNullOrWhiteSpace(co))
            {
                _currentItems.Add(new QuickPasteItemModel
                {
                    BadgeText = "CO #",
                    BadgeBackground = CompanyBg,
                    BadgeForeground = CompanyFg,
                    DisplayText = $"Company {co}",
                    ValueToPaste = co,
                    ShortcutKey = shortcutIndex <= 9 ? $"{shortcutIndex++}" : string.Empty
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(vars.ContactName))
        {
            _currentItems.Add(new QuickPasteItemModel
            {
                BadgeText = "CONTACT",
                BadgeBackground = ContactBg,
                BadgeForeground = ContactFg,
                DisplayText = vars.ContactName,
                ValueToPaste = vars.ContactName,
                ShortcutKey = shortcutIndex <= 9 ? $"{shortcutIndex++}" : string.Empty
            });
        }

        if (!string.IsNullOrWhiteSpace(vars.ActiveTemplateText))
        {
            _currentItems.Add(new QuickPasteItemModel
            {
                BadgeText = "TEMPLATE",
                BadgeBackground = TemplateBg,
                BadgeForeground = TemplateFg,
                DisplayText = string.IsNullOrWhiteSpace(vars.ActiveTemplateName) ? "Active Template" : vars.ActiveTemplateName,
                ValueToPaste = vars.ActiveTemplateText,
                ShortcutKey = shortcutIndex <= 9 ? $"{shortcutIndex++}" : string.Empty
            });
        }

        if (_currentItems.Count == 0)
        {
            _currentItems.Add(new QuickPasteItemModel
            {
                BadgeText = "INFO",
                BadgeBackground = ServerBg,
                BadgeForeground = ServerFg,
                DisplayText = "No active dashboard data",
                ValueToPaste = string.Empty,
                ShortcutKey = string.Empty
            });
        }

        ItemsList.ItemsSource = null;
        ItemsList.ItemsSource = _currentItems;

        // Position window taking DPI into account
        var dpi = VisualTreeHelper.GetDpi(this);
        double left = screenPoint.X / dpi.DpiScaleX;
        double top = screenPoint.Y / dpi.DpiScaleY;

        if (offsetForCompanion)
        {
            // Position to the right and slightly down so it anchors alongside native FormGen menu
            left += 160;
            top += 20;
        }

        // Clamp inside virtual screen
        double maxWidth = SystemParameters.VirtualScreenWidth;
        double maxHeight = SystemParameters.VirtualScreenHeight;

        if (left + Width > maxWidth) left = Math.Max(0, maxWidth - Width - 10);
        if (top + 200 > maxHeight) top = Math.Max(0, maxHeight - 220);

        Left = left;
        Top = top;

        Show();
        Activate();
        Focus();
    }

    private void OnItemClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is QuickPasteItemModel item)
        {
            PasteValue(item.ValueToPaste);
        }
    }

    public async void PasteValue(string value)
    {
        Hide();
        if (string.IsNullOrEmpty(value)) return;

        try
        {
            Clipboard.SetDataObject(value, true);
        }
        catch { }

        if (_targetHwnd != IntPtr.Zero)
        {
            SetForegroundWindow(_targetHwnd);
            await Task.Delay(50);

            // Synthesize Ctrl+V
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        Hide();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            Hide();
            e.Handled = true;
        }
        else if (e.Key >= Key.D1 && e.Key <= Key.D9)
        {
            int index = e.Key - Key.D1;
            if (index < _currentItems.Count && !string.IsNullOrEmpty(_currentItems[index].ValueToPaste))
            {
                PasteValue(_currentItems[index].ValueToPaste);
                e.Handled = true;
            }
        }
    }
}

