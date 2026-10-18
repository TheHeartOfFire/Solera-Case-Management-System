using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.CodeBlocks;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.CodeBlocks;
using AMFormsCST.Core.Types.CodeBlocks.Formulae;
using AMFormsCST.Core.Types.CodeBlocks.Functions;
using AMFormsCST.Core.Utils;
using AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools;

public partial class CodeSnippetsViewModel : ViewModel
{
    private readonly ILogService? _logger;
    private readonly ICodeBlocks _codeBlocks;

    [ObservableProperty]
    private ObservableCollection<CodeSnippetItemViewModel> _codeSnippets = [];

    [ObservableProperty]
    private ObservableCollection<object> _nodes = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SelectCodeSnippetCommand))]
    private CodeSnippetItemViewModel? _selectedCodeSnippet;

    [ObservableProperty]
    private ObservableCollection<ICodeBase> _availableBlocks = [];

    [ObservableProperty]
    private ObservableCollection<ICodeBase> _availableSnippets = [];

    [ObservableProperty]
    private ObservableCollection<ICodeBase> _customSnippets = [];

    [ObservableProperty]
    private INodeViewModel? _activeRootNode;

    [ObservableProperty]
    private string _activeOutputCode = string.Empty;

    [ObservableProperty]
    private string _newCustomSnippetName = string.Empty;

    [ObservableProperty]
    private string _newCustomSnippetDescription = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TestVariableViewModel> _testVariables = [];

    [ObservableProperty]
    private ObservableCollection<CodeDiagnostic> _activeDiagnostics = [];

    [ObservableProperty]
    private string _testEvaluatedResult = string.Empty;

    [ObservableProperty]
    private string _testEvaluationExplanation = string.Empty;

    [ObservableProperty]
    private bool _testEvaluationHasError;

    [ObservableProperty]
    private string _coachMessage = "Select or snap blocks together to build a FormGen expression.";

    public CodeSnippetsViewModel(ISupportTool supportTool, ILogService? logger = null)
    {
        _logger = logger;
        _codeBlocks = supportTool.CodeBlocks ?? throw new ArgumentNullException(nameof(_codeBlocks));

        InitializeCollections();
    }

    #region Design Time Constructor
    /// <summary>
    /// Parameterless constructor for design-time data.
    /// Do not use in production code.
    /// </summary>
    public CodeSnippetsViewModel()
    {
        _codeBlocks = new CodeBlocks();
        InitializeCollections();
    }
    #endregion

    private void InitializeCollections()
    {
        var allBlocks = _codeBlocks.GetBlocks();

        var snippets = allBlocks.Where(b => b.GetType().Namespace?.Contains("Formulae") == true).ToList();
        var functions = allBlocks.Where(b => b.GetType().Namespace?.Contains("Functions") == true).ToList();

        // Fallback when mock ICodeBase instances are injected in unit tests (which lack Formulae/Functions namespaces)
        if (snippets.Count == 0 && functions.Count == 0 && allBlocks.Count > 0)
        {
            snippets = allBlocks.ToList();
        }

        AvailableSnippets = new ObservableCollection<ICodeBase>(snippets.OrderBy(x => x.Name));
        AvailableBlocks = new ObservableCollection<ICodeBase>(functions.OrderBy(x => x.Name));
        CustomSnippets = _codeBlocks.CustomBlocks != null
            ? new ObservableCollection<ICodeBase>(_codeBlocks.CustomBlocks)
            : [];

        CodeSnippets = new ObservableCollection<CodeSnippetItemViewModel>(
            snippets.OrderBy(x => x.Name)
                    .Select(x => new CodeSnippetItemViewModel(x))
        );

        if (CodeSnippets.Any())
        {
            SelectedCodeSnippet = CodeSnippets.First();
            SelectedCodeSnippet.IsSelected = true;
            LoadGraph(SelectedCodeSnippet.CodeBase);
            _logger?.LogInfo($"CodeSnippetsViewModel initialized with {CodeSnippets.Count} snippets. Selected: {SelectedCodeSnippet.Name}");
        }
        else
        {
            _logger?.LogInfo("CodeSnippetsViewModel initialized with no snippets.");
        }
    }

    private bool _isSyncingOutput;

    partial void OnSelectedCodeSnippetChanged(CodeSnippetItemViewModel? oldValue, CodeSnippetItemViewModel? newValue)
    {
        if (oldValue != null)
        {
            oldValue.IsSelected = false;
            oldValue.PropertyChanged -= SelectedCodeSnippet_PropertyChanged;
        }

        if (newValue != null)
        {
            newValue.IsSelected = true;
            newValue.PropertyChanged += SelectedCodeSnippet_PropertyChanged;
            LoadGraph(newValue.CodeBase);
        }
    }

    private void SelectedCodeSnippet_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!_isSyncingOutput && e.PropertyName == nameof(CodeSnippetItemViewModel.Output) && sender is CodeSnippetItemViewModel item)
        {
            ActiveOutputCode = item.Output;
        }
    }

    [RelayCommand]
    private void SelectCodeSnippet(CodeSnippetItemViewModel? item)
    {
        if (item == null || item == SelectedCodeSnippet)
        {
            return;
        }

        SelectedCodeSnippet = item;
        _logger?.LogInfo($"Code snippet selected: {item.Name}");
    }

    [RelayCommand]
    public void CopyOutput()
    {
        var outputToCopy = !string.IsNullOrEmpty(ActiveOutputCode)
            ? ActiveOutputCode
            : SelectedCodeSnippet?.Output;

        if (string.IsNullOrEmpty(outputToCopy))
        {
            _logger?.LogWarning("CopyOutput called but no snippet is selected or output is empty.");
            return;
        }

        try
        {
            Clipboard.SetText(outputToCopy);
            _logger?.LogInfo($"Copied output: {outputToCopy}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error copying output to clipboard.", ex);
        }
    }

    private static ICodeBase CloneTemplate(ICodeBase template)
    {
        if (template is CodeBase cb)
        {
            return cb.Clone();
        }
        return template;
    }

    private Point GetNextCanvasLocation()
    {
        var rootNodes = Nodes.OfType<INodeViewModel>().ToList();
        double yOffset = rootNodes.Count > 0 ? rootNodes.Max(n => n.Location.Y) + 160 : 60;
        return new Point(80, yOffset);
    }

    [RelayCommand]
    public void AddNode(ICodeBase? snippet)
    {
        if (snippet == null) return;
        AddBlockToCanvas(snippet, GetNextCanvasLocation());
    }

    [RelayCommand]
    public void AddBlock(ICodeBase? block)
    {
        if (block == null) return;
        AddBlockToCanvas(block, GetNextCanvasLocation());
    }

    public CodeSnippetViewModel AddBlockToCanvas(ICodeBase template, Point location)
    {
        var cloned = CloneTemplate(template);
        var vm = new CodeSnippetViewModel(cloned, this)
        {
            Location = location
        };
        Nodes.Add(vm);
        SetActiveRoot(vm);
        return vm;
    }

    [RelayCommand]
    public void AddUserInput()
    {
        AddUserInputAt(GetNextCanvasLocation());
    }

    public UserInputViewModel AddUserInputAt(Point location, string initialType = UserInputViewModel.TypeFieldRaw)
    {
        var userInput = new UserInputViewModel
        {
            Workspace = this,
            Location = location,
            SelectedType = initialType
        };
        Nodes.Add(userInput);
        SetActiveRoot(userInput);
        return userInput;
    }

    [RelayCommand]
    public void AddOperator(string? op)
    {
        var operatorBlock = new OperatorCode(string.IsNullOrWhiteSpace(op) ? "+" : op);
        AddBlockToCanvas(operatorBlock, GetNextCanvasLocation());
    }

    [RelayCommand]
    public void ClearCanvas()
    {
        Nodes.Clear();
        ActiveRootNode = null;
        ActiveOutputCode = string.Empty;
    }

    [RelayCommand]
    public void DeleteNode()
    {
        var selectedNodes = Nodes.OfType<INodeViewModel>().Where(n => n.IsSelected).ToList();
        foreach (var node in selectedNodes)
        {
            RemoveSpecificNode(node);
        }
    }

    public void RemoveSpecificNode(INodeViewModel node)
    {
        if (node.ParentSlot != null)
        {
            node.ParentSlot.ConnectedNode = null;
        }
        Nodes.Remove(node);

        if (ReferenceEquals(ActiveRootNode, node))
        {
            ActiveRootNode = Nodes.OfType<INodeViewModel>().FirstOrDefault();
        }
        RefreshActiveOutput();
    }

    public void LoadGraph(ICodeBase? root)
    {
        Nodes.Clear();
        if (root == null)
        {
            ActiveRootNode = null;
            ActiveOutputCode = string.Empty;
            return;
        }

        var cloned = CloneTemplate(root);
        var rootVm = new CodeSnippetViewModel(cloned, this)
        {
            Location = new Point(80, 60),
            IsSelected = true
        };
        Nodes.Add(rootVm);
        SetActiveRoot(rootVm);
    }

    public bool DockBlockIntoSlot(INodeViewModel block, CodeInputViewModel targetSlot)
    {
        if (block == null || targetSlot == null) return false;

        // Prevent docking a block into one of its own descendant slots (cycle prevention)
        if (IsSlotDescendantOfNode(targetSlot, block))
        {
            CoachMessage = $"Cannot dock '{block.Title}' into '{targetSlot.Title}' because '{targetSlot.Title}' is inside '{block.Title}' (blocks cannot contain themselves).";
            return false;
        }

        // If already docked in this exact slot, nothing to do
        if (ReferenceEquals(targetSlot.ConnectedNode, block))
        {
            return true;
        }

        // Detach block from its previous parent slot or root canvas
        if (block.ParentSlot != null)
        {
            var oldSlot = block.ParentSlot;
            oldSlot.ConnectedNode = null;
        }
        else
        {
            Nodes.Remove(block);
        }

        // If the target slot already has an existing block, eject it to the canvas next to the root parent
        if (targetSlot.ConnectedNode != null)
        {
            var existingBlock = targetSlot.ConnectedNode;
            targetSlot.ConnectedNode = null;
            EjectNodeNearTarget(existingBlock, targetSlot);
            CoachMessage = $"Replaced '{existingBlock.Title}' in slot '{targetSlot.Title}' with '{block.Title}' and moved '{existingBlock.Title}' to the canvas so you don't lose it.";
        }
        else
        {
            CoachMessage = $"Snapped '{block.Title}' into slot '{targetSlot.Title}'.";
        }

        targetSlot.ConnectedNode = block;

        if (targetSlot.Parent is CodeSnippetViewModel parentBlock)
        {
            SetActiveRoot(GetRootNode(parentBlock));
        }
        else
        {
            RefreshActiveOutput();
        }

        return true;
    }

    public void UndockBlockToCanvas(INodeViewModel block, Point? dropLocation = null)
    {
        if (block == null) return;

        var oldSlot = block.ParentSlot;
        INodeViewModel? rootParent = oldSlot?.Parent is INodeViewModel parentNode ? GetRootNode(parentNode) : null;

        if (oldSlot != null)
        {
            oldSlot.ConnectedNode = null;
        }

        if (dropLocation.HasValue)
        {
            block.Location = dropLocation.Value;
        }
        else if (rootParent != null)
        {
            block.Location = new Point(rootParent.Location.X + 40, rootParent.Location.Y + 140);
        }
        else
        {
            block.Location = GetNextCanvasLocation();
        }

        if (!Nodes.Contains(block))
        {
            Nodes.Add(block);
        }

        CoachMessage = $"Undocked '{block.Title}' back onto the workspace canvas.";
        RefreshActiveOutput();
    }

    private void EjectNodeNearTarget(INodeViewModel ejectedNode, CodeInputViewModel fromSlot)
    {
        var rootParent = fromSlot.Parent is INodeViewModel parentNode ? GetRootNode(parentNode) : null;
        ejectedNode.Location = rootParent != null
            ? new Point(rootParent.Location.X + 40, rootParent.Location.Y + 140)
            : GetNextCanvasLocation();

        if (!Nodes.Contains(ejectedNode))
        {
            Nodes.Add(ejectedNode);
        }
    }

    public void InsertBlockTemplateIntoSlot(ICodeBase template, CodeInputViewModel targetSlot)
    {
        if (template == null || targetSlot == null) return;
        var cloned = CloneTemplate(template);
        var newBlock = new CodeSnippetViewModel(cloned, this);
        DockBlockIntoSlot(newBlock, targetSlot);
    }

    public void InsertValueBlockIntoSlot(CodeInputViewModel targetSlot, string initialType = UserInputViewModel.TypeFieldRaw)
    {
        if (targetSlot == null) return;
        var valueBlock = new UserInputViewModel
        {
            Workspace = this,
            SelectedType = initialType,
            Value = targetSlot.BindableValue ?? string.Empty
        };
        DockBlockIntoSlot(valueBlock, targetSlot);
    }

    public void DeconstructBlock(CodeSnippetViewModel block)
    {
        if (block == null || !block.CanDeconstruct) return;

        var structure = block.CodeBase.GetStructure();
        if (structure == null) return;

        structure.WasDeconstructed = true;
        structure.ReconstructName = block.CodeBase is CodeBase origCb && !string.IsNullOrWhiteSpace(origCb.ReconstructName)
            ? origCb.ReconstructName
            : block.Title;
        structure.ReconstructDescription = block.CodeBase is CodeBase origDescCb && !string.IsNullOrWhiteSpace(origDescCb.ReconstructDescription)
            ? origDescCb.ReconstructDescription
            : block.Description;

        var expandedVm = new CodeSnippetViewModel(structure, this)
        {
            Location = block.Location
        };

        ReplaceBlockInPlace(block, expandedVm);

        CoachMessage = $"Deconstructed '{block.Title}' into its underlying primitive Function blocks so you can see and edit how it works!";
    }

    public void ReconstructBlock(CodeSnippetViewModel block)
    {
        if (block == null || !block.CanReconstruct || block.CodeBase is not CodeBase rootCodeBase) return;

        var name = !string.IsNullOrWhiteSpace(rootCodeBase.ReconstructName)
            ? rootCodeBase.ReconstructName!
            : (block.Title ?? "Collapsed Snippet");

        var description = !string.IsNullOrWhiteSpace(rootCodeBase.ReconstructDescription)
            ? rootCodeBase.ReconstructDescription!
            : block.Description;

        bool exposeVars = !rootCodeBase.WasDeconstructed;
        var collapsedSnippet = new CustomSnippetCode(name, description, rootCodeBase, exposeVariableIdentifiers: exposeVars);

        var collapsedVm = new CodeSnippetViewModel(collapsedSnippet, this)
        {
            Location = block.Location
        };

        ReplaceBlockInPlace(block, collapsedVm);

        CoachMessage = $"Reconstructed '{collapsedVm.Title}' into a single block with {collapsedVm.Inputs.Count} input slot(s). Click Deconstruct anytime to expand it again!";
    }

    private void ReplaceBlockInPlace(CodeSnippetViewModel oldBlock, CodeSnippetViewModel newBlock)
    {
        var parentSlot = oldBlock.ParentSlot;
        if (parentSlot != null)
        {
            parentSlot.ConnectedNode = newBlock;
            SetActiveRoot(GetRootNode(newBlock));
        }
        else
        {
            int idx = Nodes.IndexOf(oldBlock);
            if (idx >= 0)
            {
                Nodes[idx] = newBlock;
            }
            else
            {
                Nodes.Add(newBlock);
            }
            SetActiveRoot(newBlock);
        }
    }

    [RelayCommand]
    public void SaveCustomSnippet()
    {
        var targetRoot = ActiveRootNode as CodeSnippetViewModel
            ?? Nodes.OfType<CodeSnippetViewModel>().FirstOrDefault();

        if (targetRoot == null || targetRoot.CodeBase is not CodeBase rootCodeBase)
        {
            _logger?.LogWarning("SaveCustomSnippet called with no valid root CodeSnippetViewModel.");
            return;
        }

        var name = string.IsNullOrWhiteSpace(NewCustomSnippetName)
            ? $"Custom {targetRoot.Title} ({CustomSnippets.Count + 1})"
            : NewCustomSnippetName.Trim();

        var desc = string.IsNullOrWhiteSpace(NewCustomSnippetDescription)
            ? $"Custom snippet based on {targetRoot.Title}"
            : NewCustomSnippetDescription.Trim();

        var customSnippet = new CustomSnippetCode(name, desc, rootCodeBase);
        _codeBlocks.CustomBlocks?.Add(customSnippet);
        CustomSnippets.Add(customSnippet);
        AvailableSnippets.Add(customSnippet);
        CodeSnippets.Add(new CodeSnippetItemViewModel(customSnippet));

        NewCustomSnippetName = string.Empty;
        NewCustomSnippetDescription = string.Empty;
        CoachMessage = $"Saved '{customSnippet.Name}' to Custom Snippets with {customSnippet.Inputs.Count} exposed input slot(s).";

        _logger?.LogInfo($"Saved custom snippet: {customSnippet.Name}");
    }

    public void OnNodeSelected(INodeViewModel node)
    {
        foreach (var root in Nodes.OfType<INodeViewModel>())
        {
            if (!ReferenceEquals(root, node))
            {
                root.IsSelected = false;
            }
        }
        CoachMessage = !string.IsNullOrWhiteSpace(node.Description)
            ? $"{node.Title}: {node.Description}"
            : $"Selected {node.Title}.";
        SetActiveRoot(GetRootNode(node));
    }

    public void NotifyGraphUpdated(INodeViewModel changedNode)
    {
        var root = GetRootNode(changedNode);
        if (ActiveRootNode == null || ReferenceEquals(root, ActiveRootNode))
        {
            SetActiveRoot(root);
        }
        else
        {
            RefreshActiveOutput();
        }
    }

    private void SetActiveRoot(INodeViewModel? root)
    {
        ActiveRootNode = root ?? Nodes.OfType<INodeViewModel>().FirstOrDefault();
        RefreshActiveOutput();
    }

    public void RefreshActiveOutput()
    {
        var target = ActiveRootNode ?? Nodes.OfType<INodeViewModel>().FirstOrDefault();
        ActiveOutputCode = target?.GetCode() ?? SelectedCodeSnippet?.Output ?? string.Empty;

        if (SelectedCodeSnippet != null)
        {
            _isSyncingOutput = true;
            try
            {
                SelectedCodeSnippet.Output = ActiveOutputCode;
            }
            finally
            {
                _isSyncingOutput = false;
            }
        }

        SyncTestVariablesAndEvaluate(target);
    }

    private void SyncTestVariablesAndEvaluate(INodeViewModel? rootNode)
    {
        if (rootNode is CodeSnippetViewModel snippetRoot && snippetRoot.CodeBase is CodeBase cb)
        {
            var discoveredVars = cb.GetVariables();
            var existingByName = TestVariables.ToDictionary(v => v.Name, v => v, StringComparer.OrdinalIgnoreCase);

            var updatedList = new List<TestVariableViewModel>();
            foreach (var varName in discoveredVars)
            {
                if (existingByName.TryGetValue(varName, out var existingVm))
                {
                    updatedList.Add(existingVm);
                }
                else
                {
                    var hint = CodeBase.IsLikelyFieldReference(varName) ? "FormGen Field" : "Variable / Slot";
                    updatedList.Add(new TestVariableViewModel(
                        varName,
                        GetDefaultSampleValue(varName),
                        hint,
                        EvaluateActiveFormulaOnly));
                }
            }

            TestVariables = new ObservableCollection<TestVariableViewModel>(updatedList);
            EvaluateActiveFormulaOnly();
        }
        else if (rootNode is UserInputViewModel userRoot)
        {
            TestVariables.Clear();
            ActiveDiagnostics.Clear();
            TestEvaluatedResult = userRoot.GetCode();
            TestEvaluationExplanation = $"Value Block ({userRoot.SelectedType}): outputs {TestEvaluatedResult}";
            TestEvaluationHasError = false;
        }
        else
        {
            TestVariables.Clear();
            ActiveDiagnostics.Clear();
            TestEvaluatedResult = string.Empty;
            TestEvaluationExplanation = string.Empty;
            TestEvaluationHasError = false;
        }
    }

    public void EvaluateActiveFormulaOnly()
    {
        var target = ActiveRootNode ?? Nodes.OfType<INodeViewModel>().FirstOrDefault();
        if (target is CodeSnippetViewModel snippetRoot && snippetRoot.CodeBase is CodeBase cb)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tv in TestVariables)
            {
                dict[tv.Name] = tv.SampleValue;
            }

            var evalResult = cb.Evaluate(dict);
            TestEvaluatedResult = evalResult.Value;
            TestEvaluationExplanation = evalResult.Explanation;
            TestEvaluationHasError = !evalResult.IsSuccess || evalResult.Diagnostics.Any(d => d.Severity == CodeDiagnosticSeverity.Error);
            ActiveDiagnostics = new ObservableCollection<CodeDiagnostic>(evalResult.Diagnostics);
        }
    }

    private static string GetDefaultSampleValue(string varName)
    {
        if (varName.Contains("Date", StringComparison.OrdinalIgnoreCase)) return "04/15/2026";
        if (string.Equals(varName, "City", StringComparison.OrdinalIgnoreCase)) return "Austin";
        if (string.Equals(varName, "State", StringComparison.OrdinalIgnoreCase)) return "TX";
        if (string.Equals(varName, "ZIP", StringComparison.OrdinalIgnoreCase)) return "78701";
        if (varName.Contains("Day", StringComparison.OrdinalIgnoreCase)) return "21";
        if (varName.Contains("Decimal", StringComparison.OrdinalIgnoreCase)) return "2";
        if (varName.Contains("Number", StringComparison.OrdinalIgnoreCase) || varName.Contains("Numeric", StringComparison.OrdinalIgnoreCase)) return "123.456";
        if (string.Equals(varName, "F776", StringComparison.OrdinalIgnoreCase)) return "GAS";
        if (CodeBase.IsLikelyFieldReference(varName)) return "100";
        return "Sample";
    }

    public static INodeViewModel GetRootNode(INodeViewModel node)
    {
        var current = node;
        while (current.ParentSlot?.Parent is INodeViewModel parent)
        {
            current = parent;
        }
        return current;
    }

    private static bool IsSlotDescendantOfNode(CodeInputViewModel slot, INodeViewModel candidateAncestor)
    {
        object? currentParent = slot.Parent;
        while (currentParent is INodeViewModel parentNode)
        {
            if (ReferenceEquals(parentNode, candidateAncestor))
            {
                return true;
            }
            currentParent = parentNode.ParentSlot?.Parent;
        }
        return false;
    }
}
