using AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure;
using CommunityToolkit.Mvvm.ComponentModel;
using static AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLineSettings;
using static AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.PromptDataSettings;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.FormgenUtils;

public partial class PromptItemViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action? _onSelectionChanged;

    public CodeLine CodeLine { get; }

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        _onSelectionChanged?.Invoke();
    }

    public PromptItemViewModel(CodeLine codeLine, Action? onChanged = null, Action? onSelectionChanged = null)
    {
        CodeLine = codeLine ?? throw new ArgumentNullException(nameof(codeLine));
        _onChanged = onChanged;
        _onSelectionChanged = onSelectionChanged;
    }

    public int Order
    {
        get => CodeLine.Settings?.Order ?? 0;
        set
        {
            if (CodeLine.Settings != null && CodeLine.Settings.Order != value)
            {
                CodeLine.Settings.Order = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public string Variable
    {
        get => CodeLine.Settings?.Variable ?? string.Empty;
        set
        {
            if (CodeLine.Settings != null && CodeLine.Settings.Variable != value)
            {
                CodeLine.Settings.Variable = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public CodeType Type
    {
        get => CodeLine.Settings?.Type ?? CodeType.PROMPT;
        set
        {
            if (CodeLine.Settings != null && CodeLine.Settings.Type != value)
            {
                CodeLine.Settings.Type = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public PromptType? PromptKind
    {
        get => CodeLine.PromptData?.Settings?.Type;
        set
        {
            if (CodeLine.PromptData?.Settings != null && value.HasValue && CodeLine.PromptData.Settings.Type != value.Value)
            {
                CodeLine.PromptData.Settings.Type = value.Value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public string Message
    {
        get => CodeLine.PromptData?.Message ?? string.Empty;
        set
        {
            if (CodeLine.PromptData != null && CodeLine.PromptData.Message != value)
            {
                CodeLine.PromptData.Message = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public string Expression
    {
        get => CodeLine.Expression ?? string.Empty;
        set
        {
            if (CodeLine.Expression != value)
            {
                CodeLine.Expression = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public bool Required
    {
        get => CodeLine.PromptData?.Settings?.Required ?? false;
        set
        {
            if (CodeLine.PromptData?.Settings != null && CodeLine.PromptData.Settings.Required != value)
            {
                CodeLine.PromptData.Settings.Required = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public string ChoicesText
    {
        get => CodeLine.PromptData != null ? string.Join(", ", CodeLine.PromptData.Choices) : string.Empty;
        set
        {
            if (CodeLine.PromptData != null)
            {
                var list = value?.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList() ?? [];

                CodeLine.PromptData.Choices = list;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public void RefreshProperties()
    {
        OnPropertyChanged(nameof(Order));
        OnPropertyChanged(nameof(Variable));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(PromptKind));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(Expression));
        OnPropertyChanged(nameof(Required));
        OnPropertyChanged(nameof(ChoicesText));
    }
}

