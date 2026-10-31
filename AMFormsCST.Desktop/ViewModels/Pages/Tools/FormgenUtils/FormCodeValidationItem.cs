using AMFormsCST.Core.Utils;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.FormgenUtils;

public partial class FormCodeValidationItem : ObservableObject
{
    public string Location { get; }
    public string ItemType { get; }
    public object TargetObject { get; }

    [ObservableProperty]
    private string _expression;

    [ObservableProperty]
    private FormgenParseStatus _status;

    [ObservableProperty]
    private string _statusTitle;

    [ObservableProperty]
    private string _diagnosticMessage;

    [ObservableProperty]
    private string? _suggestedFix;

    public bool HasSuggestedFix => !string.IsNullOrWhiteSpace(SuggestedFix);
    public bool IsError => Status == FormgenParseStatus.MalformedSimilar;
    public bool IsWarning => Status == FormgenParseStatus.Unrecognized;
    public bool IsValid => Status == FormgenParseStatus.Valid;

    public FormCodeValidationItem(
        string location,
        string itemType,
        string expression,
        object targetObject,
        FormgenParseResult parseResult)
    {
        Location = location;
        ItemType = itemType;
        TargetObject = targetObject;
        _expression = expression;
        _status = parseResult.Status;
        _statusTitle = parseResult.StatusTitle;
        _diagnosticMessage = parseResult.DiagnosticMessage;
        _suggestedFix = parseResult.SuggestedFix;
    }

    public void UpdateResult(FormgenParseResult result)
    {
        Status = result.Status;
        StatusTitle = result.StatusTitle;
        DiagnosticMessage = result.DiagnosticMessage;
        SuggestedFix = result.SuggestedFix;
        OnPropertyChanged(nameof(HasSuggestedFix));
        OnPropertyChanged(nameof(IsError));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsValid));
    }
}

