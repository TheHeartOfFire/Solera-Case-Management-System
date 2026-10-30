using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure;
using AMFormsCST.Core.Utils;
using AMFormsCST.Desktop.Interfaces;
using AMFormsCST.Desktop.Models.FormgenUtilities;
using AMFormsCST.Desktop.Models.FormgenUtilities.Grouping;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.ViewModels.Pages.Tools.FormgenUtils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Xml;
using static AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.DotFormgen;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools;

public partial class FormgenUtilitiesViewModel : ViewModel
{
    public static FormgenUtilitiesViewModel? ActiveInstance { get; private set; }

    private readonly ILogService? _logger;

    [ObservableProperty]
    private ObservableCollection<TreeItemNodeViewModel> _treeViewNodes = [];

    private TreeItemNodeViewModel? _selectedNode;
    public virtual TreeItemNodeViewModel? SelectedNode
    {
        get => _selectedNode;
        set
        {
            ActiveInstance = this;
            if (SetProperty(ref _selectedNode, value))
            {
                UpdateSelectedNodeProperties();
                SyncSelectionWithTables(value?.Data);
                _logger?.LogInfo($"Selected node changed: {value?.Header}");
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanged))]
    private ObservableCollection<DisplayProperty>? _selectedNodeProperties;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFileLoaded))]
    private string? _filePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanged))]
    private string _formTitle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasChanged))]
    private string _uuid = string.Empty;

    [ObservableProperty]
    private bool? _isImageFound;

    [ObservableProperty]
    private bool _shouldRenameImage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isSidebarVisible = true;

    [ObservableProperty]
    private int _selectedWorkspaceTabIndex = 0;

    // Center Workspace Collections
    public ObservableCollection<PromptItemViewModel> PromptsList { get; } = [];
    public ObservableCollection<FieldItemViewModel> FieldsList { get; } = [];
    public ObservableCollection<FieldItemViewModel> FilteredFieldsList { get; } = [];
    public ObservableCollection<string> AvailablePageFilters { get; } = [];

    [ObservableProperty]
    private string _selectedPageFilter = "All Pages";

    private PromptItemViewModel? _selectedPromptItem;
    public PromptItemViewModel? SelectedPromptItem
    {
        get => _selectedPromptItem;
        set
        {
            if (SetProperty(ref _selectedPromptItem, value))
            {
                if (value?.CodeLine != null)
                {
                    SelectTreeNodeForData(value.CodeLine);
                }
            }
        }
    }

    private FieldItemViewModel? _selectedFieldItem;
    public FieldItemViewModel? SelectedFieldItem
    {
        get => _selectedFieldItem;
        set
        {
            if (SetProperty(ref _selectedFieldItem, value))
            {
                if (value?.Field != null)
                {
                    SelectTreeNodeForData(value.Field);
                }
            }
        }
    }

    private bool? _areAllPromptsSelected = false;
    public bool? AreAllPromptsSelected
    {
        get => _areAllPromptsSelected;
        set
        {
            if (SetProperty(ref _areAllPromptsSelected, value))
            {
                if (value.HasValue)
                {
                    foreach (var p in PromptsList)
                    {
                        p.IsSelected = value.Value;
                    }
                    OnPropertyChanged(nameof(PromptsSelectedCountText));
                }
            }
        }
    }

    public string PromptsSelectedCountText
    {
        get
        {
            int selected = PromptsList.Count(p => p.IsSelected);
            return selected > 0
                ? $"{selected} of {PromptsList.Count} Selected"
                : $"{PromptsList.Count} Prompts & Code Lines";
        }
    }

    public void UpdateAreAllPromptsSelected()
    {
        if (PromptsList.Count == 0)
        {
            _areAllPromptsSelected = false;
        }
        else
        {
            int count = PromptsList.Count(p => p.IsSelected);
            if (count == 0)
                _areAllPromptsSelected = false;
            else if (count == PromptsList.Count)
                _areAllPromptsSelected = true;
            else
                _areAllPromptsSelected = null;
        }
        OnPropertyChanged(nameof(AreAllPromptsSelected));
        OnPropertyChanged(nameof(PromptsSelectedCountText));
    }

    private bool? _areAllFieldsSelected = false;
    public bool? AreAllFieldsSelected
    {
        get => _areAllFieldsSelected;
        set
        {
            if (SetProperty(ref _areAllFieldsSelected, value))
            {
                if (value.HasValue)
                {
                    foreach (var f in FilteredFieldsList)
                    {
                        f.IsSelected = value.Value;
                    }
                    OnPropertyChanged(nameof(FieldsSelectedCountText));
                }
            }
        }
    }

    public string FieldsSelectedCountText
    {
        get
        {
            int selected = FilteredFieldsList.Count(f => f.IsSelected);
            return selected > 0
                ? $"{selected} of {FilteredFieldsList.Count} Selected"
                : $"{FilteredFieldsList.Count} Fields";
        }
    }

    public void UpdateAreAllFieldsSelected()
    {
        if (FilteredFieldsList.Count == 0)
        {
            _areAllFieldsSelected = false;
        }
        else
        {
            int count = FilteredFieldsList.Count(f => f.IsSelected);
            if (count == 0)
                _areAllFieldsSelected = false;
            else if (count == FilteredFieldsList.Count)
                _areAllFieldsSelected = true;
            else
                _areAllFieldsSelected = null;
        }
        OnPropertyChanged(nameof(AreAllFieldsSelected));
        OnPropertyChanged(nameof(FieldsSelectedCountText));
    }

    private bool? _areAllTransferPromptsSelected = false;
    public bool? AreAllTransferPromptsSelected
    {
        get => _areAllTransferPromptsSelected;
        set
        {
            if (SetProperty(ref _areAllTransferPromptsSelected, value))
            {
                if (value.HasValue)
                {
                    foreach (var p in TransferSourcePrompts)
                    {
                        p.IsSelected = value.Value;
                    }
                }
            }
        }
    }

    public void UpdateAreAllTransferPromptsSelected()
    {
        if (TransferSourcePrompts.Count == 0)
        {
            _areAllTransferPromptsSelected = false;
        }
        else
        {
            int count = TransferSourcePrompts.Count(p => p.IsSelected);
            if (count == 0)
                _areAllTransferPromptsSelected = false;
            else if (count == TransferSourcePrompts.Count)
                _areAllTransferPromptsSelected = true;
            else
                _areAllTransferPromptsSelected = null;
        }
        OnPropertyChanged(nameof(AreAllTransferPromptsSelected));
    }

    // Code Parser & Diagnostics
    public ObservableCollection<FormCodeValidationItem> CodeValidationIssues { get; } = [];

    [ObservableProperty]
    private string _codeValidationSummary = "No scan performed.";

    [ObservableProperty]
    private bool _isCodeScanning;

    [ObservableProperty]
    private string _scratchpadExpression = string.Empty;

    [ObservableProperty]
    private FormgenParseResult? _scratchpadResult;

    // Cross-Form Transfer
    [ObservableProperty]
    private string? _transferSourceFilePath;

    [ObservableProperty]
    private string _transferSourceTitle = string.Empty;

    public ObservableCollection<PromptItemViewModel> TransferSourcePrompts { get; } = [];
    public ObservableCollection<int> TransferSourcePages { get; } = [];

    [ObservableProperty]
    private int _selectedTransferSourcePage = 1;

    [ObservableProperty]
    private int _selectedTransferTargetPage = 1;

    public bool HasChanged
    {
        get
        {
            if (_backupLoaded) return true;
            if (_supportTool?.FormgenUtils.HasChanged == true) return true;
            var parsed = _supportTool?.FormgenUtils.ParsedFormgenFile;
            if (parsed is null || !IsFileLoaded) return false;

            var originalTitle = parsed.Title ?? (FilePath is not null ? _fileSystem.GetFileNameWithoutExtension(FilePath) : string.Empty) ?? string.Empty;
            if (!string.Equals(originalTitle, FormTitle ?? string.Empty, StringComparison.Ordinal)) return true;
            if (!string.Equals(parsed.Settings?.UUID ?? string.Empty, Uuid ?? string.Empty, StringComparison.Ordinal)) return true;
            return false;
        }
    }

    public bool IsFileLoaded => !string.IsNullOrEmpty(FilePath);
    private bool _backupLoaded = false;

    private readonly ISupportTool? _supportTool;
    private readonly IDialogService _dialogService;
    private readonly IFileSystem _fileSystem;

    #region Design Time Constructor
    public FormgenUtilitiesViewModel()
    {
        _dialogService = new DesignTimeDialogService();
        _fileSystem = new DesignTimeFileSystem();
        IsImageFound = true;
        ShouldRenameImage = true;

        var xmlDoc = new XmlDocument();
        string xmlString;

        try
        {
            var resourceName = "AMFormsCST.Desktop.SampleData.Formgen_Sample_Data.Pdf_Sample.Sample Pdf.formgen";
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);

            if (stream == null)
                throw new FileNotFoundException("Design-time resource not found. Verify the file's 'Build Action' is 'Embedded resource' and the resource name is correct.", resourceName);

            using var reader = new StreamReader(stream);
            xmlString = reader.ReadToEnd();
            xmlDoc.LoadXml(xmlString);
        }
        catch (Exception ex)
        {
            var fallbackUuid = Guid.NewGuid().ToString();
            string errorMessage = ex is FileNotFoundException
                ? "Sample resource file not found. Check Build Action and resource name."
                : $"Error loading sample: {ex.Message}";

            xmlString = $@"
<formDef version=""4"" publishedUUID=""{fallbackUuid}"" legacyImport=""false"" totalPages=""1"" defaultPoints=""10"" missingSourceJpeg=""false"" duplex=""false"" maxAccessoryLines=""3"" prePrintedLaserForm=""false"">
  <pages pageNumber=""1"" defaultPoints=""10"" leftPrinterMargin=""18"" rightPrinterMargin=""18"" topPrinterMargin=""18"" bottomPrinterMargin=""18"">
    <fields>
      <entry>
        <key>1</key>
        <value uniqueId=""1"" formFieldType=""TEXT"" legacyCol=""0"" legacyLine=""0"" x=""10"" y=""10"" w=""100"" h=""20"" manualSize=""false"" fontPoints=""10"" boldFont=""false"" shrinkFontToFit=""false"" pictureLeft=""10"" pictureRight=""0"" displayPartialField=""false"" startChar=""0"" endChar=""0"" perCharDeltaPts=""0"" alignment=""Left"">
          <expression>'SampleField'</expression>
          <sampleData>Sample</sampleData>
          <formatOption>None</formatOption>
        </value>
      </entry>
    </fields>
  </pages>
  <title>{errorMessage}</title>
  <formPrintType>Pdf</formPrintType>
  <codeLines order=""0"" type=""PROMPT"" destVariable=""F0"">
    <promptData type=""InstructionLine"" promptIsExpression=""false"" required=""false"" leftSize=""0"" rightSize=""0"" choicesDelimiter=""0"" allowNegatives=""false"" forceUpperCase=""false"" makeBuyerVars=""false"" includeNoneAsOption=""false"">
      <promptMessage>Could not load sample file.</promptMessage>
    </promptData>
  </codeLines>
</formDef>";
            xmlDoc.LoadXml(xmlString);
        }

        var xmlElement = xmlDoc.DocumentElement!;
        var sampleFormgen = new DotFormgen(xmlElement);

        FilePath = @"C:\SampleData\Sample Pdf.formgen";
        FormTitle = sampleFormgen.Title ?? "Sample Form";
        Uuid = sampleFormgen.Settings.UUID;

        var rootNode = new TreeItemNodeViewModel(sampleFormgen, this, null);
        TreeViewNodes.Add(rootNode);

        rootNode.IsSelected = true;
        rootNode.IsExpanded = true;

        PopulateInteractiveLists(sampleFormgen);
    }
    #endregion

    public FormgenUtilitiesViewModel(ISupportTool supportTool, IDialogService dialogService, IFileSystem fileSystem, ILogService? logger = null)
    {
        ActiveInstance = this;
        _supportTool = supportTool ?? throw new ArgumentNullException(nameof(supportTool));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        _logger = logger;
        _supportTool.FormgenUtils.FormgenFileChanged += (s, e) => OnPropertyChanged(nameof(HasChanged));
        _logger?.LogInfo("FormgenUtilitiesViewModel initialized.");
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    [RelayCommand]
    private void OpenFormgenFile()
    {
        var filter = "Formgen Files (*.formgen)|*.formgen|All files (*.*)|*.*";
        var selectedFile = _dialogService.ShowOpenFileDialog(filter);

        if (!string.IsNullOrEmpty(selectedFile))
        {
            FilePath = selectedFile;
            _backupLoaded = false;
            _logger?.LogInfo($"Formgen file opened: {selectedFile}");
            LoadFileContent();
        }
    }

    private const string SaveIdleText = "Save";
    private const string SaveSavingText = "Saving...";
    private const string SaveSavedText = "Saved ✓";
    private static readonly TimeSpan SaveSavedDisplayTime = TimeSpan.FromMilliseconds(1500);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveFormgenFileCommand))]
    private bool _isSaving;

    [ObservableProperty]
    private string _saveButtonText = SaveIdleText;

    private bool CanSaveFormgenFile() => !IsSaving;

    private void SaveCurrentFileCore()
    {
        if (!IsFileLoaded || string.IsNullOrWhiteSpace(FilePath) || _supportTool?.FormgenUtils.ParsedFormgenFile is null)
            return;

        var originalTitle = _supportTool.FormgenUtils.ParsedFormgenFile.Title ?? _fileSystem.GetFileNameWithoutExtension(FilePath);
        bool titleHasChanged = !string.Equals(originalTitle, FormTitle, StringComparison.Ordinal);

        _supportTool.FormgenUtils.ParsedFormgenFile.Title = FormTitle;
        _supportTool.FormgenUtils.ParsedFormgenFile.Settings.UUID = Uuid;

        _supportTool.FormgenUtils.SaveFile(FilePath);

        if (titleHasChanged)
        {
            _supportTool.FormgenUtils.RenameFile(FormTitle, ShouldRenameImage);
            var directory = _fileSystem.GetDirectoryName(FilePath);
            FilePath = _fileSystem.CombinePath(directory!, FormTitle + ".formgen");
        }

        _backupLoaded = false;
        if (TreeViewNodes.Count > 0)
        {
            TreeViewNodes[0].RefreshHeader();
        }
        SelectedNode?.RefreshHeader();
        OnPropertyChanged(nameof(HasChanged));
    }

    [RelayCommand(CanExecute = nameof(CanSaveFormgenFile))]
    private async Task SaveFormgenFile()
    {
        if (!IsFileLoaded || _supportTool?.FormgenUtils.ParsedFormgenFile is null || IsSaving) return;

        IsSaving = true;
        SaveButtonText = SaveSavingText;
        IsBusy = true;
        try
        {
            SaveCurrentFileCore();
            _logger?.LogInfo($"Formgen file saved: {FilePath}");
            IsBusy = false;
            SaveButtonText = SaveSavedText;
            await Task.Delay(SaveSavedDisplayTime);
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessageBox($"Failed to save file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _logger?.LogError("Failed to save Formgen file.", ex);
        }
        finally
        {
            SaveButtonText = SaveIdleText;
            IsSaving = false;
            IsBusy = false;
            OnPropertyChanged(nameof(HasChanged));
        }
    }

    private const string PushIdleText = "Push to FormGen";
    private const string PushSendingText = "Sending...";
    private const string PushSentText = "Sent ✓";
    private static readonly TimeSpan PushSentDisplayTime = TimeSpan.FromMilliseconds(1500);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PushToFormGenCommand))]
    private bool _isPushing;

    [ObservableProperty]
    private string _pushButtonText = PushIdleText;

    private bool CanPushToFormGen() => !IsPushing;

    [RelayCommand(CanExecute = nameof(CanPushToFormGen))]
    private async Task PushToFormGen()
    {
        if (!IsFileLoaded || string.IsNullOrWhiteSpace(FilePath) || IsPushing) return;

        IsPushing = true;
        PushButtonText = PushSendingText;
        IsBusy = true;
        try
        {
            if (_supportTool?.FormgenUtils.ParsedFormgenFile is not null)
            {
                SaveCurrentFileCore();
                _logger?.LogInfo($"Saved Formgen file and created backup before pushing to FormGen: {FilePath}");
            }

            var formgenBatPath = _supportTool?.Settings?.UserSettings?.FormGenExecutablePath;
            var path = FilePath;
            var uuid = Uuid;
            var logger = _logger;
            var success = await Task.Run(() => FormGenLauncherHelper.PushFormToFormGen(path, uuid, formgenBatPath, logger));

            if (success == true)
            {
                _logger?.LogInfo($"Form successfully pushed to FormGen: {FilePath}");
                PushButtonText = PushSentText;
                await Task.Delay(PushSentDisplayTime);
            }
            else
            {
                _dialogService.ShowMessageBox("Failed to push form to FormGen. Could not communicate with FormGen or find NewFormGen.bat.", "Push Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessageBox($"Error pushing form to FormGen: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _logger?.LogError("Error pushing form to FormGen.", ex);
        }
        finally
        {
            PushButtonText = PushIdleText;
            IsPushing = false;
            IsBusy = false;
            OnPropertyChanged(nameof(HasChanged));
        }
    }

    [RelayCommand]
    private void LoadBackup()
    {
        if (!IsFileLoaded || _supportTool?.FormgenUtils.ParsedFormgenFile?.Settings.UUID is null) return;

        var backupDir = _fileSystem.CombinePath(AMFormsCST.Core.IO.BackupPath, _supportTool.FormgenUtils.ParsedFormgenFile.Settings.UUID);
        var filter = "Backup Files (*.bak)|*.bak|All files (*.*)|*.*";
        var selectedFile = _dialogService.ShowOpenFileDialog(filter, backupDir);

        if (!string.IsNullOrEmpty(selectedFile))
        {
            _supportTool.FormgenUtils.LoadBackup(selectedFile);
            _backupLoaded = true;
            _logger?.LogInfo($"Backup loaded: {selectedFile}");
            LoadFileContent(selectedFile);
        }
    }

    [RelayCommand]
    private void ClearFile()
    {
        FilePath = null;
        FormTitle = string.Empty;
        Uuid = string.Empty;
        IsImageFound = null;
        ShouldRenameImage = false;
        TreeViewNodes.Clear();
        SelectedNode = null;
        SelectedNodeProperties = null;
        PromptsList.Clear();
        FieldsList.Clear();
        FilteredFieldsList.Clear();
        AvailablePageFilters.Clear();
        CodeValidationIssues.Clear();
        CodeValidationSummary = "No scan performed.";
        SelectedPromptItem = null;
        SelectedFieldItem = null;
        _supportTool?.FormgenUtils.CloseFile();
        _logger?.LogInfo("Formgen file cleared.");
    }

    [RelayCommand]
    private void RegenerateUuid()
    {
        if (!IsFileLoaded) return;
        Uuid = Guid.NewGuid().ToString();
        _logger?.LogInfo($"UUID regenerated: {Uuid}");
    }

    public void LoadFormgenFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !_fileSystem.FileExists(path)) return;
        FilePath = path;
        _backupLoaded = false;
        _logger?.LogInfo($"Formgen file loaded from external path: {path}");
        LoadFileContent();
    }

    private void LoadFileContent(string? filePath = null)
    {
        if (!IsFileLoaded || _supportTool is null) return;

        IsBusy = true;
        TreeViewNodes.Clear();
        SelectedNode = null;

        var path = filePath ?? FilePath;

        try
        {
            _supportTool.FormgenUtils.OpenFile(path!);
            var fileData = _supportTool.FormgenUtils.ParsedFormgenFile ?? throw new InvalidOperationException("Failed to parse the .formgen file.");
            FormTitle = fileData.Title ?? _fileSystem.GetFileNameWithoutExtension(FilePath) ?? string.Empty;
            Uuid = fileData.Settings.UUID;

            var fileDir = _fileSystem.GetDirectoryName(FilePath);
            var fileNameNoExt = _fileSystem.GetFileNameWithoutExtension(FilePath);
            var pdfPath = _fileSystem.CombinePath(fileDir!, fileNameNoExt + ".pdf");
            var jpgPath = _fileSystem.CombinePath(fileDir!, fileNameNoExt + ".jpg");

            IsImageFound = fileData.FormType == Format.Pdf ? _fileSystem.FileExists(pdfPath) : _fileSystem.FileExists(jpgPath);
            ShouldRenameImage = IsImageFound ?? false;

            var rootNode = new TreeItemNodeViewModel(fileData, this, _logger);
            TreeViewNodes.Add(rootNode);

            if (TreeViewNodes.Count > 0)
            {
                rootNode.IsSelected = true;
                rootNode.IsExpanded = true;
            }

            PopulateInteractiveLists(fileData);

            _logger?.LogInfo($"Formgen file content loaded: {path}");
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessageBox($"Failed to load file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            FilePath = null;
            _logger?.LogError("Failed to load Formgen file content.", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void PopulateInteractiveLists(DotFormgen fileData)
    {
        PromptsList.Clear();
        foreach (var cl in fileData.CodeLines)
        {
            PromptsList.Add(new PromptItemViewModel(
                cl,
                () =>
                {
                    OnPropertyChanged(nameof(HasChanged));
                    SelectedNode?.RefreshHeader();
                },
                UpdateAreAllPromptsSelected));
        }
        UpdateAreAllPromptsSelected();

        FieldsList.Clear();
        AvailablePageFilters.Clear();
        AvailablePageFilters.Add("All Pages");

        for (int pIdx = 0; pIdx < fileData.Pages.Count; pIdx++)
        {
            var page = fileData.Pages[pIdx];
            int pageNum = page.Settings?.PageNumber ?? (pIdx + 1);
            AvailablePageFilters.Add($"Page {pageNum}");
            foreach (var f in page.Fields)
            {
                FieldsList.Add(new FieldItemViewModel(
                    f,
                    page,
                    () =>
                    {
                        OnPropertyChanged(nameof(HasChanged));
                        SelectedNode?.RefreshHeader();
                    },
                    UpdateAreAllFieldsSelected));
            }
        }

        SelectedPageFilter = "All Pages";
        ApplyPageFilter();
    }

    partial void OnSelectedPageFilterChanged(string value)
    {
        ApplyPageFilter();
    }

    private void ApplyPageFilter()
    {
        FilteredFieldsList.Clear();
        if (string.IsNullOrEmpty(SelectedPageFilter) || SelectedPageFilter == "All Pages")
        {
            foreach (var f in FieldsList) FilteredFieldsList.Add(f);
        }
        else if (SelectedPageFilter.StartsWith("Page "))
        {
            if (int.TryParse(SelectedPageFilter.Substring(5).Trim(), out int pageNum))
            {
                foreach (var f in FieldsList.Where(field => field.PageNumber == pageNum))
                    FilteredFieldsList.Add(f);
            }
        }
        UpdateAreAllFieldsSelected();
    }

    private void SyncSelectionWithTables(object? data)
    {
        if (data is FormField field)
        {
            var match = FieldsList.FirstOrDefault(f => f.Field == field);
            if (match != null && _selectedFieldItem != match)
            {
                _selectedFieldItem = match;
                OnPropertyChanged(nameof(SelectedFieldItem));
            }
            _selectedPromptItem = null;
            OnPropertyChanged(nameof(SelectedPromptItem));
        }
        else if (data is CodeLine line)
        {
            var match = PromptsList.FirstOrDefault(p => p.CodeLine == line);
            if (match != null && _selectedPromptItem != match)
            {
                _selectedPromptItem = match;
                OnPropertyChanged(nameof(SelectedPromptItem));
            }
            _selectedFieldItem = null;
            OnPropertyChanged(nameof(SelectedFieldItem));
        }
    }

    private void SelectTreeNodeForData(object targetData)
    {
        var node = FindTreeNode(TreeViewNodes, targetData);
        if (node != null && node != SelectedNode)
        {
            node.IsSelected = true;
        }
    }

    private static TreeItemNodeViewModel? FindTreeNode(IEnumerable<TreeItemNodeViewModel> nodes, object targetData)
    {
        foreach (var node in nodes)
        {
            if (node.Data == targetData) return node;
            var found = FindTreeNode(node.Children, targetData);
            if (found != null) return found;
        }
        return null;
    }

    #region Prompt Manipulation Commands
    [RelayCommand]
    private void CloneSelectedPrompts(object? commandParameter = null)
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;

        var selectedFromCheckboxes = PromptsList.Where(p => p.IsSelected).Select(p => p.CodeLine);
        var selectedFromGrid = (commandParameter as System.Collections.IList)?
            .OfType<PromptItemViewModel>()
            .Select(p => p.CodeLine) ?? Enumerable.Empty<CodeLine>();

        var selected = selectedFromCheckboxes.Concat(selectedFromGrid).Distinct().ToArray();
        if (selected.Length == 0 && SelectedPromptItem != null)
        {
            selected = [SelectedPromptItem.CodeLine];
        }

        if (selected.Length == 0) return;

        _supportTool.FormgenUtils.ClonePrompt(selected);
        RefreshTreeAndLists();
        _logger?.LogInfo($"Cloned {selected.Length} prompts.");
    }

    [RelayCommand]
    private void DeleteSelectedPrompts(object? commandParameter = null)
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;

        var selectedFromCheckboxes = PromptsList.Where(p => p.IsSelected).Select(p => p.CodeLine);
        var selectedFromGrid = (commandParameter as System.Collections.IList)?
            .OfType<PromptItemViewModel>()
            .Select(p => p.CodeLine) ?? Enumerable.Empty<CodeLine>();

        var selected = selectedFromCheckboxes.Concat(selectedFromGrid).Distinct().ToList();
        if (selected.Count == 0 && SelectedPromptItem != null)
        {
            selected.Add(SelectedPromptItem.CodeLine);
        }

        if (selected.Count == 0) return;

        _supportTool.FormgenUtils.DeletePrompts(selected);
        RefreshTreeAndLists();
        _logger?.LogInfo($"Deleted {selected.Count} prompts.");
    }

    [RelayCommand]
    private void MoveSelectedPromptUp()
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null || SelectedPromptItem == null) return;

        _supportTool.FormgenUtils.MovePrompt(SelectedPromptItem.CodeLine, moveUp: true);
        RefreshTreeAndLists();
    }

    [RelayCommand]
    private void MoveSelectedPromptDown()
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null || SelectedPromptItem == null) return;

        _supportTool.FormgenUtils.MovePrompt(SelectedPromptItem.CodeLine, moveUp: false);
        RefreshTreeAndLists();
    }
    #endregion

    #region Field Manipulation Commands
    [RelayCommand]
    private void CloneSelectedFields(object? commandParameter = null)
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;

        var selectedFromCheckboxes = FilteredFieldsList.Where(f => f.IsSelected);
        var selectedFromGrid = (commandParameter as System.Collections.IList)?
            .OfType<FieldItemViewModel>() ?? Enumerable.Empty<FieldItemViewModel>();

        var selected = selectedFromCheckboxes.Concat(selectedFromGrid).Distinct().ToList();
        if (selected.Count == 0 && SelectedFieldItem != null)
        {
            selected.Add(SelectedFieldItem);
        }

        if (selected.Count == 0) return;

        foreach (var item in selected)
        {
            _supportTool.FormgenUtils.CloneField(item.Page, item.Field);
        }

        RefreshTreeAndLists();
        _logger?.LogInfo($"Cloned {selected.Count} fields.");
    }

    [RelayCommand]
    private void DeleteSelectedFields(object? commandParameter = null)
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;

        var selectedFromCheckboxes = FilteredFieldsList.Where(f => f.IsSelected);
        var selectedFromGrid = (commandParameter as System.Collections.IList)?
            .OfType<FieldItemViewModel>() ?? Enumerable.Empty<FieldItemViewModel>();

        var selected = selectedFromCheckboxes.Concat(selectedFromGrid).Distinct().ToList();
        if (selected.Count == 0 && SelectedFieldItem != null)
        {
            selected.Add(SelectedFieldItem);
        }

        if (selected.Count == 0) return;

        foreach (var group in selected.GroupBy(item => item.Page))
        {
            _supportTool.FormgenUtils.DeleteFields(group.Key, group.Select(i => i.Field));
        }

        RefreshTreeAndLists();
        _logger?.LogInfo($"Deleted {selected.Count} fields.");
    }

    [RelayCommand]
    private void AlignFields(string modeStr)
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;
        if (!Enum.TryParse<FieldAlignmentMode>(modeStr, true, out var mode)) return;

        var selected = FilteredFieldsList.Where(f => f.IsSelected).Select(f => f.Field).ToList();
        if (selected.Count < 2) return;

        _supportTool.FormgenUtils.AlignFields(selected, mode);
        foreach (var f in FilteredFieldsList.Where(f => f.IsSelected))
        {
            f.RefreshProperties();
        }
        OnPropertyChanged(nameof(HasChanged));
        UpdateSelectedNodeProperties();
    }

    [RelayCommand]
    private void DistributeFields(string modeStr)
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;
        if (!Enum.TryParse<FieldDistributionMode>(modeStr, true, out var mode)) return;

        var selected = FilteredFieldsList.Where(f => f.IsSelected).Select(f => f.Field).ToList();
        if (selected.Count < 3) return;

        _supportTool.FormgenUtils.DistributeFields(selected, mode);
        foreach (var f in FilteredFieldsList.Where(f => f.IsSelected))
        {
            f.RefreshProperties();
        }
        OnPropertyChanged(nameof(HasChanged));
        UpdateSelectedNodeProperties();
    }

    [RelayCommand]
    private void BulkToggleBold()
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;

        var targets = FilteredFieldsList.Where(f => f.IsSelected).ToList();
        if (targets.Count == 0) targets = FilteredFieldsList.ToList();
        if (targets.Count == 0) return;

        bool anyBold = targets.Any(f => f.Bold);
        bool targetBold = !anyBold;

        _supportTool.FormgenUtils.BulkSetFieldProperties(targets.Select(f => f.Field), bold: targetBold);
        foreach (var f in targets) f.RefreshProperties();
        OnPropertyChanged(nameof(HasChanged));
        UpdateSelectedNodeProperties();
    }

    [RelayCommand]
    private void BulkSetFontSize(object sizeParam)
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;
        int size = sizeParam is int s ? s : (int.TryParse(sizeParam?.ToString(), out int parsed) ? parsed : 10);

        var targets = FilteredFieldsList.Where(f => f.IsSelected).ToList();
        if (targets.Count == 0) targets = FilteredFieldsList.ToList();
        if (targets.Count == 0) return;

        _supportTool.FormgenUtils.BulkSetFieldProperties(targets.Select(f => f.Field), fontSize: size);
        foreach (var f in targets) f.RefreshProperties();
        OnPropertyChanged(nameof(HasChanged));
        UpdateSelectedNodeProperties();
    }

    [RelayCommand]
    private void BulkSetFontAlignment(string alignStr)
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;
        if (!Enum.TryParse<FormFieldSettings.Alignment>(alignStr, true, out var align)) return;

        var targets = FilteredFieldsList.Where(f => f.IsSelected).ToList();
        if (targets.Count == 0) targets = FilteredFieldsList.ToList();
        if (targets.Count == 0) return;

        _supportTool.FormgenUtils.BulkSetFieldProperties(targets.Select(f => f.Field), fontAlignment: align);
        foreach (var f in targets) f.RefreshProperties();
        OnPropertyChanged(nameof(HasChanged));
        UpdateSelectedNodeProperties();
    }

    [RelayCommand]
    private void AlignAcrossPages()
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;

        int alignedCount = _supportTool.FormgenUtils.AlignFieldsAcrossPages(sourcePageIndex: 0);
        foreach (var f in FieldsList) f.RefreshProperties();
        OnPropertyChanged(nameof(HasChanged));
        _dialogService.ShowMessageBox($"Synchronized {alignedCount} matching field coordinates across pages.", "Align Across Pages", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    #endregion

    #region Code Parsing & Diagnostic Commands
    [RelayCommand]
    private void ScanAllCode()
    {
        if (_supportTool?.FormgenUtils.ParsedFormgenFile == null) return;

        IsCodeScanning = true;
        CodeValidationIssues.Clear();

        int totalInspected = 0;
        int issuesFound = 0;

        var file = _supportTool.FormgenUtils.ParsedFormgenFile;

        // Inspect Code Lines
        foreach (var cl in file.CodeLines)
        {
            var expr = cl.Expression;
            if (!string.IsNullOrWhiteSpace(expr))
            {
                totalInspected++;
                var result = FormgenCodeParser.Parse(expr);
                if (result.Status != FormgenParseStatus.Valid)
                {
                    issuesFound++;
                    var loc = $"{cl.Settings?.Type} Line (Var: {cl.Settings?.Variable ?? "none"})";
                    CodeValidationIssues.Add(new FormCodeValidationItem(loc, "CodeLine", expr, cl, result));
                }
            }
        }

        // Inspect Page Fields
        for (int p = 0; p < file.Pages.Count; p++)
        {
            var page = file.Pages[p];
            foreach (var f in page.Fields)
            {
                var expr = f.Expression;
                if (!string.IsNullOrWhiteSpace(expr))
                {
                    totalInspected++;
                    var result = FormgenCodeParser.Parse(expr);
                    if (result.Status != FormgenParseStatus.Valid)
                    {
                        issuesFound++;
                        var loc = $"Page {p + 1} - Field #{f.Settings?.ID}";
                        CodeValidationIssues.Add(new FormCodeValidationItem(loc, "Field", expr, f, result));
                    }
                }
            }
        }

        IsCodeScanning = false;
        CodeValidationSummary = issuesFound == 0
            ? $"Scan complete: Inspected {totalInspected} expressions. All code is valid FormGen syntax ✓"
            : $"Scan complete: Inspected {totalInspected} expressions. Found {issuesFound} issue(s) needing attention.";
    }

    [RelayCommand]
    private void ApplyCodeFix(FormCodeValidationItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.SuggestedFix)) return;

        if (item.TargetObject is FormField f)
        {
            f.Expression = item.SuggestedFix;
            var vm = FieldsList.FirstOrDefault(field => field.Field == f);
            vm?.RefreshProperties();
        }
        else if (item.TargetObject is CodeLine cl)
        {
            cl.Expression = item.SuggestedFix;
            var vm = PromptsList.FirstOrDefault(prompt => prompt.CodeLine == cl);
            vm?.RefreshProperties();
        }

        var recheck = FormgenCodeParser.Parse(item.SuggestedFix);
        item.Expression = item.SuggestedFix;
        item.UpdateResult(recheck);

        OnPropertyChanged(nameof(HasChanged));
        UpdateSelectedNodeProperties();
    }

    [RelayCommand]
    private void ValidateScratchpad()
    {
        if (string.IsNullOrWhiteSpace(ScratchpadExpression))
        {
            ScratchpadResult = null;
            return;
        }

        ScratchpadResult = FormgenCodeParser.Parse(ScratchpadExpression);
    }
    #endregion

    #region Cross-Form Transfer Commands
    [RelayCommand]
    private void BrowseTransferSource()
    {
        var filter = "Formgen Files (*.formgen)|*.formgen|All files (*.*)|*.*";
        var selectedFile = _dialogService.ShowOpenFileDialog(filter);
        if (string.IsNullOrEmpty(selectedFile) || !_fileSystem.FileExists(selectedFile)) return;

        TransferSourceFilePath = selectedFile;
        try
        {
            var content = _fileSystem.ReadAllText(selectedFile);
            var doc = new XmlDocument();
            doc.LoadXml(content);
            if (doc.DocumentElement == null) return;
            var sourceFormgen = new DotFormgen(doc.DocumentElement);

            TransferSourceTitle = sourceFormgen.Title ?? _fileSystem.GetFileNameWithoutExtension(selectedFile);
            TransferSourcePrompts.Clear();
            foreach (var p in sourceFormgen.CodeLines.Where(cl => cl.Settings?.Type == AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLineSettings.CodeType.PROMPT))
            {
                TransferSourcePrompts.Add(new PromptItemViewModel(p, null, UpdateAreAllTransferPromptsSelected));
            }
            UpdateAreAllTransferPromptsSelected();

            TransferSourcePages.Clear();
            for (int i = 1; i <= sourceFormgen.Pages.Count; i++)
            {
                TransferSourcePages.Add(i);
            }
            if (TransferSourcePages.Count > 0) SelectedTransferSourcePage = 1;
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessageBox($"Failed to read source form: {ex.Message}", "Transfer Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private void ExecutePromptTransfer()
    {
        if (string.IsNullOrEmpty(TransferSourceFilePath) || _supportTool?.FormgenUtils.ParsedFormgenFile == null) return;

        var selectedVars = TransferSourcePrompts.Where(p => p.IsSelected).Select(p => p.Variable).ToList();
        int imported = _supportTool.FormgenUtils.ImportPromptsFrom(TransferSourceFilePath, selectedVars.Count > 0 ? selectedVars : null);

        RefreshTreeAndLists();
        _dialogService.ShowMessageBox($"Imported {imported} prompts from source form.", "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    [RelayCommand]
    private void ExecuteFieldTransfer()
    {
        if (string.IsNullOrEmpty(TransferSourceFilePath) || _supportTool?.FormgenUtils.ParsedFormgenFile == null) return;

        int imported = _supportTool.FormgenUtils.ImportFieldsFrom(TransferSourceFilePath, SelectedTransferSourcePage, SelectedTransferTargetPage);
        RefreshTreeAndLists();
        _dialogService.ShowMessageBox($"Imported {imported} fields from Page {SelectedTransferSourcePage} to Page {SelectedTransferTargetPage}.", "Transfer Complete", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    #endregion

    private void RefreshTreeAndLists()
    {
        var file = _supportTool?.FormgenUtils.ParsedFormgenFile;
        if (file == null) return;

        TreeViewNodes.Clear();
        var rootNode = new TreeItemNodeViewModel(file, this, _logger);
        TreeViewNodes.Add(rootNode);
        rootNode.IsExpanded = true;

        PopulateInteractiveLists(file);
        OnPropertyChanged(nameof(HasChanged));
    }

    private void UpdateSelectedNodeProperties()
    {
        if (SelectedNode is null)
        {
            SelectedNodeProperties = null;
            return;
        }

        IFormgenFileProperties? properties = SelectedNode?.Data switch
        {
            DotFormgen formgenFile => new FormProperties(formgenFile, _logger),
            PageGroup pageGroup => new PageGroupProperties(pageGroup, _logger),
            CodeLineCollection codeLineCollection => new CodeLineCollectionProperties(codeLineCollection, _logger),
            CodeLineGroup codeLineGroup => new CodeLineGroupProperties(codeLineGroup, _logger),
            FormPage page => new PageProperties(page, _logger),
            FormField field => new FieldProperties(field, _logger),
            CodeLine codeLine => new CodeLineProperties(codeLine, _logger),
            _ => null
        };

        if (properties is null)
        {
            SelectedNodeProperties = null;
            _logger?.LogDebug("Selected node has no properties to display.");
            return;
        }

        var readOnlyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ID", "UUID", "PublishedUUID", "TotalPages", "PageNumber", "Type", "Title"
        };

        var displayProps = new List<DisplayProperty>();

        if (properties is FieldProperties fieldProps)
        {
            displayProps.AddRange(fieldProps.GetDisplayProperties());
        }
        else
        {
            foreach (var p in properties.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetMethod != null))
            {
                if (p.Name.Equals("Settings") || p.Name.Equals("PromptData")) continue;

                bool isReadOnly = readOnlyNames.Contains(p.Name) || !p.CanWrite;
                displayProps.Add(new DisplayProperty(properties, p, isReadOnly, _logger));
            }

            if (properties.Settings != null)
            {
                foreach (var p in properties.Settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetMethod != null))
                {
                    if (p.Name == "LaserRect" || p.Name == "ImpactPosition") continue;

                    bool isReadOnly = readOnlyNames.Contains(p.Name) || !p.CanWrite;
                    displayProps.Add(new DisplayProperty(properties.Settings, p, isReadOnly, _logger));
                }
            }

            if (properties is CodeLineProperties props && props.PromptData is not null)
            {
                foreach (var p in props.PromptData.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetMethod != null))
                {
                    if (p.Name.Equals("Settings")) continue;
                    displayProps.Add(new DisplayProperty(props.PromptData, p, false, _logger));
                }

                if (props.PromptData.Settings != null)
                {
                    foreach (var p in props.PromptData.Settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetMethod != null))
                    {
                        displayProps.Add(new DisplayProperty(props.PromptData.Settings, p, false, _logger));
                    }
                }
            }
        }

        foreach (var dp in displayProps)
        {
            dp.PropertyChanged += (_, _) =>
            {
                SelectedNode?.RefreshHeader();
                OnPropertyChanged(nameof(HasChanged));
            };
        }

        SelectedNodeProperties = new ObservableCollection<DisplayProperty>(displayProps);
        _logger?.LogDebug("Selected node properties updated.");
    }
}