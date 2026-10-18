using CommunityToolkit.Mvvm.ComponentModel;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;

public partial class TestVariableViewModel : ObservableObject
{
    private readonly Action _onValueChanged;

    public string Name { get; }
    public string Hint { get; }

    [ObservableProperty]
    private string _sampleValue;

    public TestVariableViewModel(string name, string initialSampleValue, string hint, Action onValueChanged)
    {
        Name = name;
        _sampleValue = initialSampleValue;
        Hint = hint;
        _onValueChanged = onValueChanged;
    }

    partial void OnSampleValueChanged(string value)
    {
        _onValueChanged?.Invoke();
    }
}

