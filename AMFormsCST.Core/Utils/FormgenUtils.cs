using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure;
using AMFormsCST.Core.Interfaces;
using System.Xml;
using static AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.DotFormgen;

namespace AMFormsCST.Core.Utils;

public class FormgenUtils : IFormgenUtils
{
    private readonly IFileSystem _fileSystem;
    private readonly ILogService? _logger;
    private string? _filePath;

    public string? FileName => _filePath is null ? null : _fileSystem.GetFileName(_filePath);
    public DotFormgen? ParsedFormgenFile { get; set; }
    public FormgenUtilsProperties Properties { get; }

    public bool HasChanged => !(_backupCopy is null || ParsedFormgenFile is null || _backupCopy.Equals(ParsedFormgenFile));
    public event EventHandler? FormgenFileChanged;
    private void OnFormgenFileChanged() => FormgenFileChanged?.Invoke(this, EventArgs.Empty);

    private DotFormgen? _backupCopy;

    public FormgenUtils(IFileSystem fileSystem, FormgenUtilsProperties properties, ILogService? logger = null)
    {
        _fileSystem = fileSystem;
        Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        _logger = logger;
        _logger?.LogInfo("FormgenUtils initialized.");
    }

    public void OpenFile(string filePath)
    {
        try
        {
            if (!filePath.Contains("FormgenBackup"))
                _filePath = filePath;

            var xmlContent = _fileSystem.ReadAllText(filePath);
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlContent);
            if (xmlDoc.DocumentElement is null) throw new XmlException("The XML file is empty or missing a root element.");
            ParsedFormgenFile = new DotFormgen(xmlDoc.DocumentElement);
            _backupCopy = ParsedFormgenFile.Clone(); 

            ParsedFormgenFile.PropertyChanged += (s, e) => OnFormgenFileChanged();

            _logger?.LogInfo($"Opened Formgen file: {filePath}");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to open Formgen file: {filePath}", ex);
            throw;
        }
    }

    public void SaveFile(string filePath, DotFormgen? fileToSave = null)
    {
        try
        {
            var file = fileToSave is null ? ParsedFormgenFile : fileToSave;

            if (file is null)
            {
                _logger?.LogWarning($"SaveFile called but file is null: {filePath}");
                return;
            }

            var xmlContent = file.GenerateXML();
            _fileSystem.WriteAllText(filePath, xmlContent);

            _logger?.LogInfo($"Saved Formgen file: {filePath}");

            if (fileToSave is not null) return;

            CreateBackup();
            // Snapshot, not alias: HasChanged compares against this copy, so it must not be the live object.
            _backupCopy = ParsedFormgenFile?.Clone();
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to save Formgen file: {filePath}", ex);
            throw;
        }
    }

    public void CloseFile()
    {
        if (ParsedFormgenFile is not null) ParsedFormgenFile.PropertyChanged -= (s, e) => OnFormgenFileChanged();
        ParsedFormgenFile = null;
        _filePath = null;
        _backupCopy = null;
        _logger?.LogInfo("Closed Formgen file.");
    }

    public void RenameFile(string newName, bool renameImage)
    {
        try
        {
            if (string.IsNullOrEmpty(_filePath) || ParsedFormgenFile is null)
            {
                _logger?.LogWarning("RenameFile called but file path or ParsedFormgenFile is null.");
                return;
            }

            var directory = _fileSystem.GetDirectoryName(_filePath);
            if (string.IsNullOrEmpty(directory))
            {
                _logger?.LogWarning("RenameFile called but directory is null.");
                return;
            }

            var originalFileNameWithoutExt = _fileSystem.GetFileNameWithoutExtension(_filePath);
            var newFormgenPath = _fileSystem.CombinePath(directory, newName + ".formgen");

            _fileSystem.MoveFile(_filePath, newFormgenPath);
            _logger?.LogInfo($"Renamed Formgen file from {_filePath} to {newFormgenPath}");
            _filePath = newFormgenPath;

            if (renameImage)
            {
                var imageExtension = ParsedFormgenFile.FormType == Format.Pdf ? ".pdf" : ".jpg";
                var originalImagePath = _fileSystem.CombinePath(directory, originalFileNameWithoutExt + imageExtension);
                var newImagePath = _fileSystem.CombinePath(directory, newName + imageExtension);

                if (_fileSystem.FileExists(originalImagePath))
                {
                    _fileSystem.MoveFile(originalImagePath, newImagePath);
                    _logger?.LogInfo($"Renamed image file from {originalImagePath} to {newImagePath}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to rename Formgen file or image.", ex);
            throw;
        }
    }

    public void RegenerateUUID()
    {
        if (ParsedFormgenFile is null) return;
        ParsedFormgenFile.Settings.UUID = Guid.NewGuid().ToString();
        OnFormgenFileChanged();
        _logger?.LogInfo("Regenerated UUID for Formgen file.");
    }

    public CodeLine[] GetCodeLines(CodeLineSettings.CodeType type)
    {
        var result = ParsedFormgenFile?.CodeLines.Where(cl => cl.Settings?.Type == type).ToArray() ?? [];
        _logger?.LogDebug($"GetCodeLines called for type {type}. Returned {result.Length} lines.");
        return result;
    }

    public CodeLine[] GetPrompts()
    {
        var result = GetCodeLines(CodeLineSettings.CodeType.PROMPT);
        _logger?.LogDebug($"GetPrompts called. Returned {result.Length} prompts.");
        return result;
    }

    public void EditPrompts(CodeLine[] prompts)
    {
        if (ParsedFormgenFile is null || prompts is null) return;

        var originalPrompts = GetPrompts().ToDictionary(p => p.Settings?.Variable ?? string.Empty);

        foreach (var editedPrompt in prompts)
        {
            if (editedPrompt.Settings?.Variable is not null && originalPrompts.TryGetValue(editedPrompt.Settings.Variable, out var originalPrompt))
            {
                originalPrompt.Expression = editedPrompt.Expression;
                if (originalPrompt.PromptData is not null && editedPrompt.PromptData is not null)
                {
                    originalPrompt.PromptData.Choices = editedPrompt.PromptData.Choices;
                    originalPrompt.PromptData.Message = editedPrompt.PromptData.Message;
                }
                _logger?.LogInfo($"Edited prompt: {editedPrompt.Settings.Variable}");
            }
        }
    }

    public void ClonePrompt(CodeLine[] selected)
    {
        if (ParsedFormgenFile is null || selected is null || selected.Length == 0)
        {
            _logger?.LogWarning("ClonePrompt called but no prompts selected or ParsedFormgenFile is null.");
            return;
        }

        var allPrompts = GetPrompts().ToList();
        int maxIndex = allPrompts.Count != 0 ? allPrompts.Max(p => p.Settings?.Order ?? 0) : 0;

        foreach (var promptToClone in selected)
        {
            maxIndex++;
            string? newName = $"Copy of {promptToClone.Settings?.Variable}";
            var newPrompt = new CodeLine(promptToClone, newName, maxIndex);
            ParsedFormgenFile.CodeLines.Add(newPrompt);
            _logger?.LogInfo($"Cloned prompt: {promptToClone.Settings?.Variable} as {newName}");
        }

        ParsedFormgenFile.CodeLines = ParsedFormgenFile.CodeLines
            .OrderBy(cl => cl.Settings?.Type)
            .ThenBy(cl => cl.Settings?.Order)
            .ToList();
    }

    public void CopyPromptsTo(string fromFilePath)
    {
        if (ParsedFormgenFile is null) return;

        try
        {
            var sourceXmlContent = _fileSystem.ReadAllText(fromFilePath);
            var sourceDoc = new XmlDocument();
            sourceDoc.LoadXml(sourceXmlContent);
            if (sourceDoc.DocumentElement is null) return; 
            var sourceFormgen = new DotFormgen(sourceDoc.DocumentElement);
            var sourcePrompts = sourceFormgen.CodeLines.Where(cl => cl.Settings?.Type == CodeLineSettings.CodeType.PROMPT);

            var targetPrompts = GetPrompts().ToList();
            int maxIndex = targetPrompts.Any() ? targetPrompts.Max(p => p.Settings?.Order ?? 0) : 0;
            var targetPromptNames = new HashSet<string?>(targetPrompts.Select(p => p.Settings?.Variable));

            foreach (var sourcePrompt in sourcePrompts)
            {
                if (sourcePrompt.Settings?.Variable is not null && !targetPromptNames.Contains(sourcePrompt.Settings.Variable))
                {
                    maxIndex++;
                    var newPrompt = new CodeLine(sourcePrompt, sourcePrompt.Settings.Variable, maxIndex);
                    ParsedFormgenFile.CodeLines.Add(newPrompt);
                    targetPromptNames.Add(newPrompt.Settings?.Variable);
                    _logger?.LogInfo($"Copied prompt: {sourcePrompt.Settings.Variable}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to copy prompts from {fromFilePath}", ex);
            throw;
        }
    }

    public void CreateBackup()
    {
        try
        {
            if (string.IsNullOrEmpty(_filePath) || _backupCopy is null) return;

            var backupDir = _fileSystem.CombinePath(IO.BackupPath, $"{_fileSystem.GetFileNameWithoutExtension(_filePath)}_{_backupCopy.Settings.UUID}");
            _fileSystem.CreateDirectory(backupDir);

            var retentionQty = Properties.BackupRetentionQty;
            if (retentionQty > 0)
            {
                var existingBackups = _fileSystem.GetFiles(backupDir)
                                                 .OrderByDescending(f => _fileSystem.GetLastWriteTime(f))
                                                 .ToList();

                if (existingBackups.Count >= retentionQty)
                {
                    var backupsToDelete = existingBackups.Skip((int)retentionQty - 1);
                    foreach (var oldBackup in backupsToDelete)
                    {
                        _fileSystem.DeleteFile(oldBackup);
                        _logger?.LogInfo($"Deleted old backup: {oldBackup}");
                    }
                }
            }

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var backupFileName = $"{{{timestamp}}}.bak";
            var backupPath = _fileSystem.CombinePath(backupDir, backupFileName);

            SaveFile(backupPath, _backupCopy);
            _logger?.LogInfo($"Created backup: {backupPath}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to create backup.", ex);
            throw;
        }
    }

    public void LoadBackup(string backupPath)
    {
        try
        {
            if (_fileSystem.FileExists(backupPath))
            {
                OpenFile(backupPath);
                OnFormgenFileChanged();
                _logger?.LogInfo($"Loaded backup: {backupPath}");
            }
            else
            {
                _logger?.LogWarning($"Backup file does not exist: {backupPath}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to load backup: {backupPath}", ex);
            throw;
        }
    }
}
