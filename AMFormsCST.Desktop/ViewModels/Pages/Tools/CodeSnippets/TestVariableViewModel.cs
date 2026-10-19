using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;

public partial class TestVariableViewModel : ObservableObject
{
    public static readonly (string AccentHex, string BackgroundHex, string BorderHex)[] ColorPalette =
    [
        ("#FF0284C7", "#1F0284C7", "#CC0284C7"), // Sky Blue
        ("#FF059669", "#1F059669", "#CC059669"), // Emerald Green
        ("#FFD97706", "#1FD97706", "#CCD97706"), // Amber
        ("#FFDB2777", "#1FDB2777", "#CCDB2777"), // Rose Pink
        ("#FF7C3AED", "#1F7C3AED", "#CC7C3AED"), // Violet
        ("#FFEA580C", "#1FEA580C", "#CCEA580C"), // Coral Orange
        ("#FF4F46E5", "#1F4F46E5", "#CC4F46E5"), // Indigo
        ("#FF0D9488", "#1F0D9488", "#CC0D9488")  // Teal
    ];

    private readonly Action _onValueChanged;

    public string Name { get; }
    public string Hint { get; }
    public string Tooltip { get; }
    public bool IsVmSafe { get; }

    public int ColorIndex { get; private set; }
    public string AccentColorHex { get; private set; } = "#FF0284C7";
    public string BackgroundColorHex { get; private set; } = "#1F0284C7";
    public string BorderColorHex { get; private set; } = "#CC0284C7";

    public Brush AccentBrush { get; private set; } = Brushes.DodgerBlue;
    public Brush BackgroundBrush { get; private set; } = Brushes.Transparent;
    public Brush BorderBrush { get; private set; } = Brushes.DodgerBlue;

    [ObservableProperty]
    private string _sampleValue;

    public TestVariableViewModel(
        string name,
        string initialSampleValue,
        string hint,
        Action onValueChanged,
        string? tooltip = null,
        bool isVmSafe = false,
        int colorIndex = 0)
    {
        Name = name;
        _sampleValue = initialSampleValue;
        Hint = hint;
        Tooltip = string.IsNullOrWhiteSpace(tooltip) ? $"{name}: {hint}" : tooltip;
        IsVmSafe = isVmSafe;
        _onValueChanged = onValueChanged;
        AssignColorIndex(colorIndex);
    }

    public void AssignColorIndex(int colorIndex)
    {
        ColorIndex = Math.Abs(colorIndex) % ColorPalette.Length;
        var (accentHex, bgHex, borderHex) = ColorPalette[ColorIndex];
        AccentColorHex = accentHex;
        BackgroundColorHex = bgHex;
        BorderColorHex = borderHex;
        AccentBrush = CreateFrozenBrush(accentHex);
        BackgroundBrush = CreateFrozenBrush(bgHex);
        BorderBrush = CreateFrozenBrush(borderHex);
        OnPropertyChanged(nameof(ColorIndex));
        OnPropertyChanged(nameof(AccentColorHex));
        OnPropertyChanged(nameof(BackgroundColorHex));
        OnPropertyChanged(nameof(BorderColorHex));
        OnPropertyChanged(nameof(AccentBrush));
        OnPropertyChanged(nameof(BackgroundBrush));
        OnPropertyChanged(nameof(BorderBrush));
    }

    internal static SolidColorBrush CreateFrozenBrush(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    partial void OnSampleValueChanged(string value)
    {
        _onValueChanged?.Invoke();
    }
}


