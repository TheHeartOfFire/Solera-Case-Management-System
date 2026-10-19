using System.Windows.Media;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;

public sealed class CodeTokenSegmentViewModel
{
    public string Text { get; }
    public bool IsVariable { get; }
    public TestVariableViewModel? Variable { get; }
    public string? VariableName => Variable?.Name;
    public Brush AccentBrush => Variable?.AccentBrush ?? Brushes.White;
    public Brush BackgroundBrush => Variable?.BackgroundBrush ?? Brushes.Transparent;
    public Brush BorderBrush => Variable?.BorderBrush ?? Brushes.Transparent;
    public string? Tooltip => Variable != null
        ? $"{Variable.Name} = \"{Variable.SampleValue}\" ({Variable.Hint})"
        : null;

    public CodeTokenSegmentViewModel(string text, TestVariableViewModel? variable = null)
    {
        Text = text;
        Variable = variable;
        IsVariable = variable is not null;
    }
}

