using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.CodeBlocks;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.CodeBlocks;
using AMFormsCST.Core.Types.CodeBlocks.Analysis;
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
    private ObservableCollection<FunctionCategoryViewModel> _functionCategories = [];

    [ObservableProperty]
    private ObservableCollection<ICodeBase> _availableSnippets = [];

    [ObservableProperty]
    private ObservableCollection<ICodeBase> _customSnippets = [];

    [ObservableProperty]
    private INodeViewModel? _activeRootNode;

    [ObservableProperty]
    private string _activeOutputCode = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CodeTokenSegmentViewModel> _generatedCodeSegments = [];

    [ObservableProperty]
    private string _newCustomSnippetName = string.Empty;

    [ObservableProperty]
    private string _newCustomSnippetDescription = string.Empty;

    [ObservableProperty]
    private ObservableCollection<TestVariableViewModel> _testVariables = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveDiagnostics))]
    private ObservableCollection<CodeDiagnostic> _activeDiagnostics = [];

    [ObservableProperty]
    private string _testEvaluatedResult = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTestEvaluationExplanation))]
    private string _testEvaluationExplanation = string.Empty;

    [ObservableProperty]
    private bool _testEvaluationHasError;

    [ObservableProperty]
    private string _coachMessage = "Select or snap blocks together to build a FormGen expression.";

    [ObservableProperty]
    private bool _isExplainerExpanded = true;

    [ObservableProperty]
    private string _explainerTitle = "Formula & Block Guide";

    [ObservableProperty]
    private string _explainerCategoryBadge = "GUIDE";

    [ObservableProperty]
    private string _explainerSummary = "Select or snap blocks together to build a FormGen expression.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExplainerSyntax))]
    private string _explainerSyntax = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExplainerExample))]
    private string _explainerExample = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExplainerNotes))]
    private string _explainerNotes = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExplainerPseudoCode))]
    private string _explainerPseudoCode = string.Empty;

    [ObservableProperty]
    private bool _isPasteAssistantVisible;

    [ObservableProperty]
    private string _pasteEditorText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasteStatusUnrecognized))]
    [NotifyPropertyChangedFor(nameof(IsPasteStatusMalformed))]
    [NotifyPropertyChangedFor(nameof(IsPasteStatusValid))]
    private FormgenParseStatus _pasteParseStatus = FormgenParseStatus.Unrecognized;

    [ObservableProperty]
    private string _pasteStatusTitle = string.Empty;

    [ObservableProperty]
    private string _pasteDiagnosticMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPasteSuggestedFix))]
    private string _pasteSuggestedFix = string.Empty;

    [ObservableProperty]
    private bool _isPasteRecognizedAlert;

    private bool _isUpdatingPasteTextInternally;

    public bool HasExplainerSyntax => !string.IsNullOrWhiteSpace(ExplainerSyntax);
    public bool HasExplainerExample => !string.IsNullOrWhiteSpace(ExplainerExample);
    public bool HasExplainerNotes => !string.IsNullOrWhiteSpace(ExplainerNotes);
    public bool HasExplainerPseudoCode => !string.IsNullOrWhiteSpace(ExplainerPseudoCode);
    public bool HasTestEvaluationExplanation => !string.IsNullOrWhiteSpace(TestEvaluationExplanation);
    public bool HasActiveDiagnostics => ActiveDiagnostics.Count > 0;
    public bool HasGeneratedCode => GeneratedCodeSegments.Count > 0;

    public bool HasPasteSuggestedFix => !string.IsNullOrWhiteSpace(PasteSuggestedFix);
    public bool IsPasteStatusUnrecognized => PasteParseStatus == FormgenParseStatus.Unrecognized;
    public bool IsPasteStatusMalformed => PasteParseStatus == FormgenParseStatus.MalformedSimilar;
    public bool IsPasteStatusValid => PasteParseStatus == FormgenParseStatus.Valid;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCodePathTree))]
    [NotifyPropertyChangedFor(nameof(HasCodePathForks))]
    private CodePathAnalysisResult? _codePathResult;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCodePathTree))]
    private ObservableCollection<CodePathTreeNode> _codePathTreeNodes = [];

    [ObservableProperty]
    private ObservableCollection<CodePathOutcomeSummary> _codePathOutcomes = [];

    [ObservableProperty]
    private int _codePathOutcomeCount;

    [ObservableProperty]
    private int _codePathForkCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGuideTabSelected))]
    [NotifyPropertyChangedFor(nameof(IsCodePathTabSelected))]
    private int _selectedExplainerTabIndex;

    public bool HasCodePathTree => CodePathTreeNodes.Count > 0;
    public bool HasCodePathForks => CodePathForkCount > 0;
    public bool IsGuideTabSelected => SelectedExplainerTabIndex == 0;
    public bool IsCodePathTabSelected => SelectedExplainerTabIndex == 1;

    [RelayCommand]
    public void SwitchToGuideTab()
    {
        SelectedExplainerTabIndex = 0;
        if (!IsExplainerExpanded)
        {
            IsExplainerExpanded = true;
        }
    }

    [RelayCommand]
    public void SwitchToCodePathTab()
    {
        SelectedExplainerTabIndex = 1;
        if (!IsExplainerExpanded)
        {
            IsExplainerExpanded = true;
        }
    }

    [RelayCommand]
    public void ApplyPathToTester(CodePathOutcomeSummary? outcome)
    {
        if (outcome == null) return;

        foreach (var (varName, val) in outcome.SuggestedVariableValues)
        {
            var existing = TestVariables.FirstOrDefault(tv => string.Equals(tv.Name, varName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.SampleValue = val;
            }
        }

        EvaluateActiveFormulaOnly();
        CoachMessage = $"Applied sample values for Path #{outcome.PathNumber} to Formula Tester ({outcome.PathSummaryText}).";
    }

    [ObservableProperty]
    private Point _viewportLocation = new(0, 0);

    [ObservableProperty]
    private double _viewportZoom = 1.0;

    public event Action? RecenterRequested;

    [RelayCommand]
    public void RecenterCanvas()
    {
        ViewportZoom = 1.0;
        var target = ActiveRootNode ?? Nodes.OfType<INodeViewModel>().FirstOrDefault();
        if (target != null)
        {
            ViewportLocation = new Point(target.Location.X - 80, target.Location.Y - 60);
        }
        else
        {
            ViewportLocation = new Point(0, 0);
        }
        RecenterRequested?.Invoke();
    }

    [RelayCommand]
    public void ToggleExplainer()
    {
        IsExplainerExpanded = !IsExplainerExpanded;
    }

    [RelayCommand]
    public void PasteFromClipboard()
    {
        try
        {
            var clipText = Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
            PasteFormulaText(clipText);
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error reading text from clipboard.", ex);
        }
    }

    public FormgenParseResult PasteFormulaText(string? rawText)
    {
        var result = FormgenCodeParser.Parse(rawText);

        if (result.Status == FormgenParseStatus.Valid && result.CondensedBlock != null)
        {
            // Succeed quietly and produce a fully formed condensed block ready for expansion and examination
            IsPasteAssistantVisible = false;
            IsPasteRecognizedAlert = false;
            PasteSuggestedFix = string.Empty;

            var addedVm = AddBlockToCanvas(result.CondensedBlock, GetNextCanvasLocation());
            OnNodeSelected(addedVm);
            RecenterCanvas();
            CoachMessage = $"Parsed '{addedVm.Title}' into a condensed block. Click '⤢ Deconstruct' to expand and examine all of its branches!";
            return result;
        }

        // Either MalformedSimilar (inform what is wrong & suggest corrections)
        // or Unrecognized (fail loudly and present pasted text in an editable box that monitors changes)
        _isUpdatingPasteTextInternally = true;
        try
        {
            PasteEditorText = rawText ?? string.Empty;
        }
        finally
        {
            _isUpdatingPasteTextInternally = false;
        }

        PasteParseStatus = result.Status;
        PasteStatusTitle = result.StatusTitle;
        PasteDiagnosticMessage = result.DiagnosticMessage;
        PasteSuggestedFix = result.SuggestedFix ?? string.Empty;
        IsPasteRecognizedAlert = false;
        IsPasteAssistantVisible = true;
        CoachMessage = result.DiagnosticMessage;
        return result;
    }

    partial void OnPasteEditorTextChanged(string value)
    {
        if (_isUpdatingPasteTextInternally) return;

        var liveResult = FormgenCodeParser.Parse(value);
        PasteParseStatus = liveResult.Status;
        PasteSuggestedFix = liveResult.SuggestedFix ?? string.Empty;

        if (liveResult.Status == FormgenParseStatus.Valid && liveResult.CondensedBlock != null)
        {
            IsPasteRecognizedAlert = true;
            PasteStatusTitle = "✓ Valid FormGen Function Recognized!";
            PasteDiagnosticMessage = $"Recognized as '{liveResult.CondensedBlock.Name}' ({liveResult.ParsedTree?.GetCode()}). Click 'Load Condensed Block' to place it on the canvas!";
            CoachMessage = PasteDiagnosticMessage;
        }
        else
        {
            IsPasteRecognizedAlert = false;
            PasteStatusTitle = liveResult.StatusTitle;
            PasteDiagnosticMessage = liveResult.DiagnosticMessage;
        }
    }

    [RelayCommand]
    public void ApplyPasteSuggestion()
    {
        if (string.IsNullOrWhiteSpace(PasteSuggestedFix)) return;
        PasteEditorText = PasteSuggestedFix;
    }

    [RelayCommand]
    public void CommitParsedPaste()
    {
        var result = FormgenCodeParser.Parse(PasteEditorText);
        if (result.Status == FormgenParseStatus.Valid && result.CondensedBlock != null)
        {
            IsPasteAssistantVisible = false;
            IsPasteRecognizedAlert = false;
            PasteSuggestedFix = string.Empty;

            var addedVm = AddBlockToCanvas(result.CondensedBlock, GetNextCanvasLocation());
            OnNodeSelected(addedVm);
            RecenterCanvas();
            CoachMessage = $"Loaded '{addedVm.Title}' onto the canvas as a condensed block ready for expansion and examination.";
        }
    }

    [RelayCommand]
    public void DismissPasteAssistant()
    {
        IsPasteAssistantVisible = false;
        IsPasteRecognizedAlert = false;
    }

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
        BuildFunctionCategories(functions);

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

    private void BuildFunctionCategories(IReadOnlyCollection<ICodeBase> functions)
    {
        static string GetFunctionGroupKey(ICodeBase block)
        {
            if (block is OperatorCode) return "Math";
            return block.Prefix switch
            {
                "IF" or "XIF" or "XXIF" or "CASE" or "CASELTE" or "MATCHRANGE" or "CONTAINS" or "NOT" or "AND" or "OR" or "BLANK" or "NOTBLANK" => "Logic",
                "TODAY" or "MONTH" or "DAY" or "YEAR" or "DATE" or "ADDDAYS" or "ADDWEEKS" or "ADDMONTHS" or "ADDYEARS" => "Date",
                "SIN" or "COS" or "TAN" or "ASIN" or "ACOS" or "ATAN" or "SINH" or "COSH" or "TANH" or "RAD" or "DEG" => "Trig",
                "ROUND" or "NUM" or "FLOOR" or "CEILING" or "ABS" or "MAX" or "MIN" or "SQRT" or "LOG" or "LOG10" or "RANDOM" or "PI" => "Math",
                _ => "Text"
            };
        }

        var grouped = functions.GroupBy(GetFunctionGroupKey)
                               .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Name).ToList());

        var categories = new List<FunctionCategoryViewModel>();

        void AddCategoryIfAny(string key, string displayName, string bgHex, string borderHex, string badgeHex, bool expanded)
        {
            if (grouped.TryGetValue(key, out var list) && list.Count > 0)
            {
                categories.Add(new FunctionCategoryViewModel(displayName, list, bgHex, borderHex, badgeHex, expanded));
            }
        }

        AddCategoryIfAny("Logic", "Logic & Conditionals", "#1FD97706", "#CCD97706", "#FFD97706", expanded: true);
        AddCategoryIfAny("Text", "Text & Formatting", "#1F0D9488", "#CC0D9488", "#FF0D9488", expanded: true);
        AddCategoryIfAny("Date", "Date & Time", "#1F0284C7", "#CC0284C7", "#FF0284C7", expanded: false);
        AddCategoryIfAny("Math", "Math & Numbers", "#1F2563EB", "#CC2563EB", "#FF2563EB", expanded: false);
        AddCategoryIfAny("Trig", "Trigonometry", "#1F4F46E5", "#CC4F46E5", "#FF4F46E5", expanded: false);

        FunctionCategories = new ObservableCollection<FunctionCategoryViewModel>(categories);
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
        bool wasEmpty = Nodes.Count == 0;
        var cloned = CloneTemplate(template);
        var vm = new CodeSnippetViewModel(cloned, this)
        {
            Location = location
        };
        Nodes.Add(vm);
        SetActiveRoot(vm);
        if (wasEmpty)
        {
            RecenterCanvas();
        }
        return vm;
    }

    [RelayCommand]
    public void AddUserInput()
    {
        AddUserInputAt(GetNextCanvasLocation());
    }

    public UserInputViewModel AddUserInputAt(Point location, string initialType = UserInputViewModel.TypeFieldRaw)
    {
        bool wasEmpty = Nodes.Count == 0;
        var userInput = new UserInputViewModel
        {
            Workspace = this,
            Location = location,
            SelectedType = initialType
        };
        Nodes.Add(userInput);
        SetActiveRoot(userInput);
        if (wasEmpty)
        {
            RecenterCanvas();
        }
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
        GeneratedCodeSegments = [];
        OnPropertyChanged(nameof(HasGeneratedCode));
        TestVariables = [];
        ActiveDiagnostics = [];
        TestEvaluatedResult = string.Empty;
        TestEvaluationExplanation = string.Empty;
        TestEvaluationHasError = false;
        CoachMessage = "Select or snap blocks together to build a FormGen expression.";
        UpdateStructuredExplainer(null);
        RecenterCanvas();
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
        if (root == null)
        {
            ClearCanvas();
            return;
        }

        Nodes.Clear();

        var cloned = CloneTemplate(root);
        var rootVm = new CodeSnippetViewModel(cloned, this)
        {
            Location = new Point(80, 60),
            IsSelected = true
        };
        Nodes.Add(rootVm);
        SetActiveRoot(rootVm);
        UpdateStructuredExplainer(rootVm);
        RecenterCanvas();
    }

    public bool DockBlockIntoSlot(INodeViewModel block, CodeInputViewModel targetSlot)
    {
        if (block == null || targetSlot == null) return false;

        // Prevent docking a block into one of its own descendant slots (cycle prevention)
        if (IsSlotDescendantOfNode(targetSlot, block))
        {
            CoachMessage = $"Cannot dock '{block.Title}' into '{targetSlot.Title}' because '{targetSlot.Title}' is inside '{block.Title}' (blocks cannot contain themselves).";
            ExplainerSummary = CoachMessage;
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

        UpdateStructuredExplainer(block);
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
        UpdateStructuredExplainer(expandedVm);
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
        UpdateStructuredExplainer(collapsedVm);
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
        _codeBlocks.SaveCustomBlocks();

        CustomSnippets.Add(customSnippet);
        AvailableSnippets.Add(customSnippet);
        CodeSnippets.Add(new CodeSnippetItemViewModel(customSnippet));

        NewCustomSnippetName = string.Empty;
        NewCustomSnippetDescription = string.Empty;
        CoachMessage = $"Saved '{customSnippet.Name}' to Custom Snippets with {customSnippet.Inputs.Count} exposed input slot(s).";

        _logger?.LogInfo($"Saved custom snippet: {customSnippet.Name}");
    }

    [RelayCommand]
    public void DeleteCustomSnippet(ICodeBase? snippet)
    {
        if (snippet == null) return;

        _codeBlocks.CustomBlocks?.Remove(snippet);
        _codeBlocks.SaveCustomBlocks();

        CustomSnippets.Remove(snippet);
        AvailableSnippets.Remove(snippet);
        var itemVm = CodeSnippets.FirstOrDefault(x => ReferenceEquals(x.CodeBase, snippet));
        if (itemVm != null)
        {
            CodeSnippets.Remove(itemVm);
        }

        CoachMessage = $"Removed custom snippet '{snippet.Name}'.";
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
        if (node is CodeSnippetViewModel snippetNode)
        {
            CoachMessage = snippetNode.DetailedExplanation;
        }
        else
        {
            CoachMessage = !string.IsNullOrWhiteSpace(node.Description)
                ? $"{node.Title}: {node.Description}"
                : $"Selected {node.Title}.";
        }
        UpdateStructuredExplainer(node);
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
        ActiveOutputCode = target?.GetCode() ?? string.Empty;

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
        if (target != null && string.Equals(ExplainerTitle, "Formula & Block Guide", StringComparison.Ordinal))
        {
            UpdateStructuredExplainer(target);
        }
        else if (target is CodeSnippetViewModel snippetTarget)
        {
            ExplainerPseudoCode = snippetTarget.CodeBase.GetPseudoCode();
        }
    }

    private void SyncTestVariablesAndEvaluate(INodeViewModel? rootNode)
    {
        if (rootNode is CodeSnippetViewModel snippetRoot && snippetRoot.CodeBase is CodeBase cb)
        {
            var discoveredVars = cb.GetVariables();
            var existingByName = TestVariables.ToDictionary(v => v.Name, v => v, StringComparer.OrdinalIgnoreCase);

            var updatedList = new List<TestVariableViewModel>();
            for (int i = 0; i < discoveredVars.Count; i++)
            {
                var varName = discoveredVars[i];
                if (existingByName.TryGetValue(varName, out var existingVm))
                {
                    existingVm.AssignColorIndex(i);
                    updatedList.Add(existingVm);
                }
                else
                {
                    var fieldInfo = FormgenReferenceCatalog.TryGetField(varName);
                    string hint;
                    if (fieldInfo != null)
                    {
                        hint = $"{fieldInfo.Description} • {fieldInfo.VmStatusBadge}";
                    }
                    else if (CodeBase.IsLikelyFieldReference(varName))
                    {
                        hint = "⚠ Unknown F-Field (Not in fields.json)";
                    }
                    else
                    {
                        hint = "Variable / Slot";
                    }

                    updatedList.Add(new TestVariableViewModel(
                        varName,
                        GetDefaultSampleValue(varName, fieldInfo),
                        hint,
                        EvaluateActiveFormulaOnly,
                        fieldInfo?.VerboseTooltip,
                        fieldInfo?.IsVehicleMerchandisingSafe ?? false,
                        colorIndex: i));
                }
            }

            TestVariables = new ObservableCollection<TestVariableViewModel>(updatedList);
            EvaluateActiveFormulaOnly();
            RebuildGeneratedCodeSegments();
        }
        else if (rootNode is UserInputViewModel userRoot)
        {
            TestVariables = [];
            ActiveDiagnostics = [];
            TestEvaluatedResult = userRoot.GetCode();
            TestEvaluationExplanation = $"Value Block ({userRoot.SelectedType}): outputs {TestEvaluatedResult}";
            TestEvaluationHasError = false;
            ClearCodePathAnalysis();
            RebuildGeneratedCodeSegments();
        }
        else
        {
            TestVariables = [];
            ActiveDiagnostics = [];
            TestEvaluatedResult = string.Empty;
            TestEvaluationExplanation = string.Empty;
            TestEvaluationHasError = false;
            CoachMessage = "Select or snap blocks together to build a FormGen expression.";
            ClearCodePathAnalysis();
            RebuildGeneratedCodeSegments();
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
            RebuildGeneratedCodeSegments();

            UpdateCodePathAnalysis(cb, dict);
        }
    }

    private void UpdateCodePathAnalysis(CodeBase cb, IReadOnlyDictionary<string, string> dict)
    {
        var analysis = CodePathAnalyzer.Analyze(cb, dict);
        CodePathResult = analysis;
        CodePathTreeNodes = analysis.RootNode != null
            ? [analysis.RootNode]
            : [];
        CodePathOutcomes = new ObservableCollection<CodePathOutcomeSummary>(analysis.Outcomes);
        CodePathOutcomeCount = analysis.TotalOutcomesCount;
        CodePathForkCount = analysis.TotalForksCount;
    }

    private void ClearCodePathAnalysis()
    {
        CodePathResult = null;
        CodePathTreeNodes = [];
        CodePathOutcomes = [];
        CodePathOutcomeCount = 0;
        CodePathForkCount = 0;
    }

    public void RebuildGeneratedCodeSegments()
    {
        var code = ActiveOutputCode ?? string.Empty;
        if (string.IsNullOrEmpty(code))
        {
            GeneratedCodeSegments = [];
            OnPropertyChanged(nameof(HasGeneratedCode));
            return;
        }

        if (TestVariables.Count == 0)
        {
            GeneratedCodeSegments = [new CodeTokenSegmentViewModel(code)];
            OnPropertyChanged(nameof(HasGeneratedCode));
            return;
        }

        var varLookup = TestVariables.ToDictionary(v => v.Name, v => v, StringComparer.OrdinalIgnoreCase);
        var segments = new List<CodeTokenSegmentViewModel>();
        var plainBuffer = new System.Text.StringBuilder();

        void FlushPlain()
        {
            if (plainBuffer.Length > 0)
            {
                segments.Add(new CodeTokenSegmentViewModel(plainBuffer.ToString()));
                plainBuffer.Clear();
            }
        }

        int i = 0;
        while (i < code.Length)
        {
            char c = code[i];

            // Keep single-quoted string literals intact so text inside quotes is never highlighted as a variable
            if (c == '\'')
            {
                int quoteStart = i++;
                while (i < code.Length && code[i] != '\'')
                {
                    i++;
                }
                if (i < code.Length && code[i] == '\'')
                {
                    i++;
                }
                plainBuffer.Append(code, quoteStart, i - quoteStart);
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                int start = i++;
                while (i < code.Length && (char.IsLetterOrDigit(code[i]) || code[i] == '_'))
                {
                    i++;
                }

                string token = code[start..i];

                // Check if followed by '(' (which means it is a function call like DAY(...) or MONTH(...), not a variable slot)
                int peek = i;
                while (peek < code.Length && char.IsWhiteSpace(code[peek]))
                {
                    peek++;
                }
                bool isFunctionCall = peek < code.Length && code[peek] == '(';

                if (!isFunctionCall && varLookup.TryGetValue(token, out var matchedVar))
                {
                    FlushPlain();
                    segments.Add(new CodeTokenSegmentViewModel(token, matchedVar));
                }
                else
                {
                    plainBuffer.Append(token);
                }
                continue;
            }

            plainBuffer.Append(c);
            i++;
        }

        FlushPlain();
        GeneratedCodeSegments = new ObservableCollection<CodeTokenSegmentViewModel>(segments);
        OnPropertyChanged(nameof(HasGeneratedCode));
    }

    private void UpdateStructuredExplainer(INodeViewModel? node)
    {
        if (node is CodeSnippetViewModel snippetNode)
        {
            var cb = snippetNode.CodeBase;
            ExplainerCategoryBadge = snippetNode.PrefixBadge;
            ExplainerPseudoCode = cb.GetPseudoCode();

            if (!string.IsNullOrWhiteSpace(cb.Prefix))
            {
                var funcDoc = FormgenReferenceCatalog.TryGetFunctionDoc(cb.Prefix);
                if (funcDoc != null)
                {
                    ExplainerTitle = funcDoc.FullName;
                    ExplainerSummary = funcDoc.HowItWorks;
                    ExplainerSyntax = funcDoc.Syntax;
                    ExplainerExample = funcDoc.Example;
                    ExplainerNotes = funcDoc.FormgenNotes ?? string.Empty;
                    return;
                }

                var opDoc = FormgenReferenceCatalog.TryGetOperatorDoc(cb.Prefix);
                if (opDoc != null)
                {
                    ExplainerTitle = opDoc.FullName;
                    ExplainerSummary = opDoc.HowItWorks;
                    ExplainerSyntax = opDoc.Syntax;
                    ExplainerExample = opDoc.Example;
                    ExplainerNotes = opDoc.FormgenNotes ?? string.Empty;
                    return;
                }
            }

            ExplainerTitle = cb.Name ?? cb.Prefix ?? "Formula Snippet";
            ExplainerSummary = !string.IsNullOrWhiteSpace(cb.Description)
                ? cb.Description!
                : "Composite FormGen formula block.";

            var structure = cb.GetStructure();
            ExplainerSyntax = structure != null ? structure.GetCode() : cb.GetCode();
            ExplainerExample = cb.Inputs.Count > 0
                ? "Inputs: " + string.Join(", ", cb.Inputs.Select(inp => inp.Description))
                : string.Empty;
            ExplainerNotes = structure != null
                ? "Click 'Deconstruct' on the block header to expand it into primitive Function blocks, or 'Reconstruct' to collapse it back."
                : string.Empty;
        }
        else if (node is UserInputViewModel userNode)
        {
            ExplainerCategoryBadge = "VALUE";
            ExplainerTitle = $"Value / Field ({userNode.SelectedType})";
            ExplainerSummary = "Represents a literal value or FormGen F-field reference that can be snapped into any function or operator slot.";
            ExplainerSyntax = userNode.GetCode();
            ExplainerExample = "F776 (Field), 'GAS' (Quoted String), 100 (Number)";
            ExplainerNotes = string.Empty;
            ExplainerPseudoCode = $"OUTPUT LITERAL OR FIELD VALUE:\n  ➜ {userNode.GetCode()}";
        }
        else
        {
            ExplainerCategoryBadge = "GUIDE";
            ExplainerTitle = "Formula & Block Guide";
            ExplainerSummary = "Select any block on the canvas to inspect its syntax, examples, and step-by-step evaluation.";
            ExplainerSyntax = string.Empty;
            ExplainerExample = string.Empty;
            ExplainerNotes = string.Empty;
            ExplainerPseudoCode = string.Empty;
        }
    }

    private static string GetDefaultSampleValue(string varName, FormgenFieldInfo? fieldInfo = null)
    {
        if (string.Equals(varName, "F776", StringComparison.OrdinalIgnoreCase)) return "GAS";
        if (fieldInfo != null)
        {
            if (fieldInfo.Description.Contains("City", StringComparison.OrdinalIgnoreCase)) return "Austin";
            if (fieldInfo.Description.Contains("State", StringComparison.OrdinalIgnoreCase)) return "TX";
            if (fieldInfo.Description.Contains("Zip", StringComparison.OrdinalIgnoreCase)) return "78701";
            if (fieldInfo.IsDateField) return "04/15/2026";
            if (fieldInfo.IsNumeric) return "100";
        }
        if (varName.Contains("Date", StringComparison.OrdinalIgnoreCase)) return "04/15/2026";
        if (string.Equals(varName, "City", StringComparison.OrdinalIgnoreCase)) return "Austin";
        if (string.Equals(varName, "State", StringComparison.OrdinalIgnoreCase)) return "TX";
        if (string.Equals(varName, "ZIP", StringComparison.OrdinalIgnoreCase)) return "78701";
        if (varName.Contains("Day", StringComparison.OrdinalIgnoreCase)) return "21";
        if (varName.Contains("Decimal", StringComparison.OrdinalIgnoreCase)) return "2";
        if (varName.Contains("Number", StringComparison.OrdinalIgnoreCase) || varName.Contains("Numeric", StringComparison.OrdinalIgnoreCase)) return "123.456";
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
