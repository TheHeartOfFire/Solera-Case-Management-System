using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure;
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
            var resources = Assembly.GetExecutingAssembly().GetManifestResourceNames();

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

        // The button stays disabled while sending and briefly after "Sent", which limits rapid repeat pushes
        // (each push saves to disk and triggers a full form reload in FormGen).
        IsPushing = true;
        PushButtonText = PushSendingText;
        IsBusy = true;
        bool? success = null;
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
            success = await Task.Run(() => FormGenLauncherHelper.PushFormToFormGen(path, uuid, formgenBatPath, logger));

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

    /// <summary>
    /// Loads a .formgen file directly from an external file path (e.g. from Windows Explorer or IPC).
    /// </summary>
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

        var path = filePath is not null ? filePath : FilePath;

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
                if (p.Name == "LaserRect") continue;

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