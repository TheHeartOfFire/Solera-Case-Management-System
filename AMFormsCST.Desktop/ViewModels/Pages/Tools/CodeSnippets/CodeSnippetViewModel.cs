using AMFormsCST.Core.Interfaces.CodeBlocks;
using AMFormsCST.Core.Interfaces.FormgenUtils;
using AMFormsCST.Core.Types.CodeBlocks;
using AMFormsCST.Core.Types.CodeBlocks.Functions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;

public partial class CodeSnippetViewModel : ObservableObject, INodeViewModel
{
    [ObservableProperty]
    private Point _location;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private ObservableCollection<CodeInputViewModel> _inputs = [];

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDocked))]
    private CodeInputViewModel? _parentSlot;

    public bool IsDocked => ParentSlot is not null;

    public CodeSnippetsViewModel? Workspace { get; set; }

    public string Description => CodeBase.Description ?? Title ?? string.Empty;
    public int Index => 0;

    public ICodeBase CodeBase { get; }
    public Guid Id { get; } = Guid.NewGuid();

    [ObservableProperty]
    private string _codeRepresentation = string.Empty;

    public string Category
    {
        get
        {
            if (CodeBase is OperatorCode) return "Operator";
            if (CodeBase.GetType().Namespace?.Contains("Formulae") == true) return "Snippet";
            return CodeBase.Prefix switch
            {
                "IF" or "CASE" or "CONTAINS" => "Logic",
                "MONTH" or "DAY" or "YEAR" => "Date",
                "ROUND" => "Math",
                _ => "Text"
            };
        }
    }

    public string PrefixBadge
    {
        get
        {
            if (CodeBase is OperatorCode op) return op.Operator;
            if (!string.IsNullOrWhiteSpace(CodeBase.Prefix)) return CodeBase.Prefix!;
            return CodeBase.GetType().Namespace?.Contains("Formulae") == true ? "SNIPPET" : "BLOCK";
        }
    }

    public bool IsOperator => CodeBase is OperatorCode;
    public IReadOnlyList<string> AvailableOperators => OperatorCode.SupportedOperators;

    public string SelectedOperator
    {
        get => CodeBase is OperatorCode op ? op.Operator : "+";
        set
        {
            if (CodeBase is OperatorCode op && op.Operator != value)
            {
                op.Operator = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrefixBadge));
                InputChanged();
            }
        }
    }

    public bool IsExtendable => CodeBase is IExtendableCode;

    public bool CanRemoveExtraInput =>
        CodeBase is IExtendableCode ext && CodeBase.InputCount() > ext.DefaultArgCount;

    public bool CanDeconstruct => CodeBase.HasStructure;

    public bool CanReconstruct =>
        (CodeBase is CodeBase cb && cb.WasDeconstructed) ||
        Inputs.Any(i => i.ConnectedNode is CodeSnippetViewModel);

    public CodeSnippetViewModel(ICodeBase codeBase, CodeSnippetsViewModel? workspace = null)
    {
        CodeBase = codeBase ?? throw new ArgumentNullException(nameof(codeBase));
        Workspace = workspace;
        Title = codeBase.Name;

        BuildInputViewModels();
        UpdateCodeRepresentation();
    }

    private void BuildInputViewModels()
    {
        Inputs.Clear();
        foreach (var input in CodeBase.Inputs)
        {
            var inputVm = new CodeInputViewModel(input, this);
            if (input.Value is CodeBase childCodeBase)
            {
                var childNode = new CodeSnippetViewModel(childCodeBase, Workspace);
                inputVm.ConnectedNode = childNode;
            }
            Inputs.Add(inputVm);
        }
        OnPropertyChanged(nameof(CanReconstruct));
        OnPropertyChanged(nameof(CanDeconstruct));
    }

    private void SyncInputsFromCodeBase()
    {
        var currentByModel = Inputs.ToDictionary(vm => vm.Model, vm => vm);
        var updatedSet = CodeBase.Inputs.ToHashSet();

        // Eject any connected nodes from slots that were removed
        foreach (var oldVm in Inputs.ToList())
        {
            if (!updatedSet.Contains(oldVm.Model) && oldVm.ConnectedNode != null)
            {
                var orphanedNode = oldVm.ConnectedNode;
                oldVm.ConnectedNode = null;
                Workspace?.UndockBlockToCanvas(orphanedNode);
            }
        }

        var newInputsList = new List<CodeInputViewModel>();
        foreach (var model in CodeBase.Inputs)
        {
            if (currentByModel.TryGetValue(model, out var existingVm))
            {
                newInputsList.Add(existingVm);
            }
            else
            {
                newInputsList.Add(new CodeInputViewModel(model, this));
            }
        }

        Inputs = new ObservableCollection<CodeInputViewModel>(newInputsList);
        RemoveExtraInputCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanRemoveExtraInput));
        OnPropertyChanged(nameof(CanReconstruct));
        InputChanged();
    }

    [RelayCommand]
    public void AddExtraInput()
    {
        if (CodeBase is IExtendableCode ext)
        {
            ext.AddExtraInputs(1);
            SyncInputsFromCodeBase();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveExtraInput))]
    public void RemoveExtraInput()
    {
        if (CodeBase is IExtendableCode ext && CanRemoveExtraInput)
        {
            ext.RemoveExtraInputs(1);
            SyncInputsFromCodeBase();
        }
    }

    [RelayCommand]
    public void Deconstruct()
    {
        Workspace?.DeconstructBlock(this);
    }

    [RelayCommand]
    public void Reconstruct()
    {
        Workspace?.ReconstructBlock(this);
    }

    [RelayCommand]
    public void Undock()
    {
        Workspace?.UndockBlockToCanvas(this);
    }

    [RelayCommand]
    public void Remove()
    {
        Workspace?.RemoveSpecificNode(this);
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            Workspace?.OnNodeSelected(this);
        }
    }

    [ObservableProperty]
    private bool _hasDiagnostics;

    [ObservableProperty]
    private string _blockDiagnosticSummary = string.Empty;

    public string GetCode() => CodeBase.GetCode();

    public void InputChanged()
    {
        OnPropertyChanged(nameof(CanReconstruct));
        OnPropertyChanged(nameof(CanDeconstruct));
        UpdateCodeRepresentation();
        ParentSlot?.OnConnectedNodeValueChanged();
        Workspace?.NotifyGraphUpdated(this);
    }

    private void UpdateCodeRepresentation()
    {
        CodeRepresentation = CodeBase.GetCode();
        UpdateDiagnostics();
    }

    public void UpdateDiagnostics()
    {
        var diags = CodeBase.Validate() ?? Array.Empty<CodeDiagnostic>();
        foreach (var inputVm in Inputs)
        {
            var slotDiag = diags.FirstOrDefault(d => d.InputIndex == inputVm.Index);
            inputVm.HasWarning = slotDiag != null;
            inputVm.WarningMessage = slotDiag?.Message;
        }

        HasDiagnostics = diags.Count > 0;
        BlockDiagnosticSummary = diags.Count > 0
            ? string.Join("\n", diags.Select(d => $"• {d.Message}"))
            : (CodeBase.Description ?? "All inputs look valid.");
    }
}

