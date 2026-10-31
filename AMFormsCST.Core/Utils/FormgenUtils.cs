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
            OnFormgenFileChanged();
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
            if (promptToClone.PromptData != null)
            {
                newPrompt.PromptData = new PromptData(promptToClone.PromptData);
            }
            ParsedFormgenFile.CodeLines.Add(newPrompt);
            _logger?.LogInfo($"Cloned prompt: {promptToClone.Settings?.Variable} as {newName}");
        }

        ParsedFormgenFile.CodeLines = ParsedFormgenFile.CodeLines
            .OrderBy(cl => cl.Settings?.Type)
            .ThenBy(cl => cl.Settings?.Order)
            .ToList();

        OnFormgenFileChanged();
    }

    public void DeletePrompts(IEnumerable<CodeLine> prompts)
    {
        if (ParsedFormgenFile is null || prompts is null) return;
        var toRemove = new HashSet<CodeLine>(prompts);
        if (toRemove.Count == 0) return;

        ParsedFormgenFile.CodeLines.RemoveAll(cl => toRemove.Contains(cl));

        foreach (var group in ParsedFormgenFile.CodeLines.GroupBy(cl => cl.Settings?.Type))
        {
            int idx = 0;
            foreach (var cl in group.OrderBy(cl => cl.Settings?.Order ?? 0))
            {
                if (cl.Settings != null) cl.Settings.Order = idx++;
            }
        }

        OnFormgenFileChanged();
        _logger?.LogInfo($"Deleted {toRemove.Count} prompts/codelines.");
    }

    public void MovePrompt(CodeLine prompt, bool moveUp)
    {
        if (ParsedFormgenFile is null || prompt?.Settings is null) return;
        var type = prompt.Settings.Type;
        var siblings = ParsedFormgenFile.CodeLines
            .Where(cl => cl.Settings?.Type == type)
            .OrderBy(cl => cl.Settings?.Order ?? 0)
            .ToList();

        int currentIndex = siblings.IndexOf(prompt);
        if (currentIndex < 0) return;

        int targetIndex = moveUp ? currentIndex - 1 : currentIndex + 1;
        if (targetIndex < 0 || targetIndex >= siblings.Count) return;

        var other = siblings[targetIndex];
        siblings[targetIndex] = prompt;
        siblings[currentIndex] = other;

        for (int i = 0; i < siblings.Count; i++)
        {
            if (siblings[i].Settings != null)
                siblings[i].Settings.Order = i;
        }

        ParsedFormgenFile.CodeLines = ParsedFormgenFile.CodeLines
            .OrderBy(cl => cl.Settings?.Type)
            .ThenBy(cl => cl.Settings?.Order)
            .ToList();

        OnFormgenFileChanged();
        _logger?.LogInfo($"Moved prompt {prompt.Settings.Variable} {(moveUp ? "up" : "down")} to index {targetIndex}.");
    }

    public FormField CloneField(FormPage page, FormField field)
    {
        if (ParsedFormgenFile is null || page is null || field is null)
            throw new ArgumentNullException();

        int maxId = 0;
        foreach (var p in ParsedFormgenFile.Pages)
        {
            foreach (var f in p.Fields)
            {
                if (f.Settings != null && f.Settings.ID > maxId)
                    maxId = f.Settings.ID;
            }
        }

        int newId = maxId + 1;
        var newField = new FormField(field, newId);

        if (newField.Settings != null)
        {
            var rect = newField.Settings.LaserRect;
            rect.X += 10;
            rect.Y += 10;
            newField.Settings.LaserRect = rect;

            var pos = newField.Settings.ImpactPosition;
            pos.X += 1;
            pos.Y += 1;
            newField.Settings.ImpactPosition = pos;
        }

        page.Fields.Add(newField);
        OnFormgenFileChanged();
        _logger?.LogInfo($"Cloned field #{field.Settings?.ID} as #{newId} on page {page.Settings?.PageNumber}.");
        return newField;
    }

    public void DeleteFields(FormPage page, IEnumerable<FormField> fields)
    {
        if (page is null || fields is null) return;
        var toRemove = new HashSet<FormField>(fields);
        if (toRemove.Count == 0) return;

        page.Fields.RemoveAll(f => toRemove.Contains(f));
        OnFormgenFileChanged();
        _logger?.LogInfo($"Deleted {toRemove.Count} fields from page {page.Settings?.PageNumber}.");
    }

    public void AlignFields(IEnumerable<FormField> fields, FieldAlignmentMode mode)
    {
        var list = fields?.Where(f => f.Settings != null).ToList();
        if (list == null || list.Count < 2) return;

        switch (mode)
        {
            case FieldAlignmentMode.Left:
                int minX = list.Min(f => f.Settings!.LaserRect.X);
                int minCol = list.Min(f => f.Settings!.ImpactPosition.X);
                foreach (var f in list)
                {
                    var r = f.Settings!.LaserRect;
                    r.X = minX;
                    f.Settings.LaserRect = r;
                    var pos = f.Settings.ImpactPosition;
                    pos.X = minCol;
                    f.Settings.ImpactPosition = pos;
                }
                break;

            case FieldAlignmentMode.Right:
                int maxRight = list.Max(f => f.Settings!.LaserRect.Right);
                foreach (var f in list)
                {
                    var r = f.Settings!.LaserRect;
                    r.X = maxRight - r.Width;
                    f.Settings.LaserRect = r;
                }
                break;

            case FieldAlignmentMode.Top:
                int minY = list.Min(f => f.Settings!.LaserRect.Y);
                int minLine = list.Min(f => f.Settings!.ImpactPosition.Y);
                foreach (var f in list)
                {
                    var r = f.Settings!.LaserRect;
                    r.Y = minY;
                    f.Settings.LaserRect = r;
                    var pos = f.Settings.ImpactPosition;
                    pos.Y = minLine;
                    f.Settings.ImpactPosition = pos;
                }
                break;

            case FieldAlignmentMode.Bottom:
                int maxBottom = list.Max(f => f.Settings!.LaserRect.Bottom);
                foreach (var f in list)
                {
                    var r = f.Settings!.LaserRect;
                    r.Y = maxBottom - r.Height;
                    f.Settings.LaserRect = r;
                }
                break;

            case FieldAlignmentMode.CenterHorizontal:
                int avgCenterX = (int)list.Average(f => f.Settings!.LaserRect.X + f.Settings.LaserRect.Width / 2.0);
                foreach (var f in list)
                {
                    var r = f.Settings!.LaserRect;
                    r.X = avgCenterX - r.Width / 2;
                    f.Settings.LaserRect = r;
                }
                break;

            case FieldAlignmentMode.CenterVertical:
                int avgCenterY = (int)list.Average(f => f.Settings!.LaserRect.Y + f.Settings.LaserRect.Height / 2.0);
                foreach (var f in list)
                {
                    var r = f.Settings!.LaserRect;
                    r.Y = avgCenterY - r.Height / 2;
                    f.Settings.LaserRect = r;
                }
                break;

            case FieldAlignmentMode.MatchWidth:
                int targetWidth = list[0].Settings!.LaserRect.Width;
                foreach (var f in list)
                {
                    var r = f.Settings!.LaserRect;
                    r.Width = targetWidth;
                    f.Settings.LaserRect = r;
                }
                break;

            case FieldAlignmentMode.MatchHeight:
                int targetHeight = list[0].Settings!.LaserRect.Height;
                foreach (var f in list)
                {
                    var r = f.Settings!.LaserRect;
                    r.Height = targetHeight;
                    f.Settings.LaserRect = r;
                }
                break;
        }

        OnFormgenFileChanged();
        _logger?.LogInfo($"Aligned {list.Count} fields using mode {mode}.");
    }

    public void DistributeFields(IEnumerable<FormField> fields, FieldDistributionMode mode)
    {
        var list = fields?.Where(f => f.Settings != null).ToList();
        if (list == null || list.Count < 3) return;

        if (mode == FieldDistributionMode.Horizontal)
        {
            var ordered = list.OrderBy(f => f.Settings!.LaserRect.X).ToList();
            int minX = ordered.First().Settings!.LaserRect.X;
            int maxX = ordered.Last().Settings!.LaserRect.X;
            double step = (double)(maxX - minX) / (ordered.Count - 1);

            for (int i = 0; i < ordered.Count; i++)
            {
                var r = ordered[i].Settings!.LaserRect;
                r.X = (int)Math.Round(minX + i * step);
                ordered[i].Settings!.LaserRect = r;
            }
        }
        else
        {
            var ordered = list.OrderBy(f => f.Settings!.LaserRect.Y).ToList();
            int minY = ordered.First().Settings!.LaserRect.Y;
            int maxY = ordered.Last().Settings!.LaserRect.Y;
            double step = (double)(maxY - minY) / (ordered.Count - 1);

            for (int i = 0; i < ordered.Count; i++)
            {
                var r = ordered[i].Settings!.LaserRect;
                r.Y = (int)Math.Round(minY + i * step);
                ordered[i].Settings!.LaserRect = r;
            }
        }

        OnFormgenFileChanged();
        _logger?.LogInfo($"Distributed {list.Count} fields {mode}.");
    }

    public void BulkSetFieldProperties(IEnumerable<FormField> fields, bool? bold = null, int? fontSize = null, FormFieldSettings.Alignment? fontAlignment = null, FormField.FormatOption? formatOption = null)
    {
        var list = fields?.Where(f => f.Settings != null).ToList();
        if (list == null || list.Count == 0) return;

        foreach (var f in list)
        {
            if (bold.HasValue) f.Settings!.Bold = bold.Value;
            if (fontSize.HasValue) f.Settings!.FontSize = fontSize.Value;
            if (fontAlignment.HasValue) f.Settings!.FontAlignment = fontAlignment.Value;
            if (formatOption.HasValue) f.FormattingOption = formatOption.Value;
        }

        OnFormgenFileChanged();
        _logger?.LogInfo($"Applied bulk properties to {list.Count} fields.");
    }

    public int AlignFieldsAcrossPages(int sourcePageIndex, IEnumerable<int>? targetPageIndices = null)
    {
        if (ParsedFormgenFile == null || sourcePageIndex < 0 || sourcePageIndex >= ParsedFormgenFile.Pages.Count)
            return 0;

        var sourcePage = ParsedFormgenFile.Pages[sourcePageIndex];
        var targets = targetPageIndices != null
            ? ParsedFormgenFile.Pages.Where((p, idx) => idx != sourcePageIndex && targetPageIndices.Contains(idx)).ToList()
            : ParsedFormgenFile.Pages.Where((p, idx) => idx != sourcePageIndex).ToList();

        int matchedCount = 0;
        foreach (var sourceField in sourcePage.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Expression) && f.Settings != null))
        {
            foreach (var targetPage in targets)
            {
                var targetField = targetPage.Fields.FirstOrDefault(tf => 
                    string.Equals(tf.Expression, sourceField.Expression, StringComparison.Ordinal) && tf.Settings != null);

                if (targetField != null)
                {
                    targetField.Settings!.LaserRect = sourceField.Settings!.LaserRect;
                    targetField.Settings.ImpactPosition = sourceField.Settings.ImpactPosition;
                    targetField.Settings.FontSize = sourceField.Settings.FontSize;
                    targetField.Settings.FontAlignment = sourceField.Settings.FontAlignment;
                    targetField.Settings.Bold = sourceField.Settings.Bold;
                    matchedCount++;
                }
            }
        }

        if (matchedCount > 0)
        {
            OnFormgenFileChanged();
            _logger?.LogInfo($"Aligned {matchedCount} matching fields across pages from page {sourcePageIndex + 1}.");
        }

        return matchedCount;
    }

    public int ImportPromptsFrom(string fromFilePath, IEnumerable<string>? variableNames = null)
    {
        if (ParsedFormgenFile is null) return 0;

        var sourceXmlContent = _fileSystem.ReadAllText(fromFilePath);
        var sourceDoc = new XmlDocument();
        sourceDoc.LoadXml(sourceXmlContent);
        if (sourceDoc.DocumentElement is null) return 0;
        var sourceFormgen = new DotFormgen(sourceDoc.DocumentElement);

        var filter = variableNames != null ? new HashSet<string>(variableNames, StringComparer.OrdinalIgnoreCase) : null;
        var sourcePrompts = sourceFormgen.CodeLines
            .Where(cl => cl.Settings?.Type == CodeLineSettings.CodeType.PROMPT && (filter == null || (cl.Settings?.Variable != null && filter.Contains(cl.Settings.Variable))))
            .ToList();

        var existingVars = new HashSet<string?>(ParsedFormgenFile.CodeLines.Select(c => c.Settings?.Variable));
        int maxOrder = ParsedFormgenFile.CodeLines.Count > 0 ? ParsedFormgenFile.CodeLines.Max(c => c.Settings?.Order ?? 0) : 0;
        int imported = 0;

        foreach (var p in sourcePrompts)
        {
            maxOrder++;
            string varName = p.Settings?.Variable ?? $"Prompt_{maxOrder}";
            if (existingVars.Contains(varName))
            {
                varName = $"{varName}_Imported";
            }
            var cloned = new CodeLine(p, varName, maxOrder);
            ParsedFormgenFile.CodeLines.Add(cloned);
            existingVars.Add(varName);
            imported++;
        }

        ParsedFormgenFile.CodeLines = ParsedFormgenFile.CodeLines
            .OrderBy(cl => cl.Settings?.Type)
            .ThenBy(cl => cl.Settings?.Order)
            .ToList();

        OnFormgenFileChanged();
        _logger?.LogInfo($"Imported {imported} prompts from {fromFilePath}.");
        return imported;
    }

    public int ImportFieldsFrom(string fromFilePath, int sourcePageNumber, int targetPageNumber)
    {
        if (ParsedFormgenFile is null) return 0;

        var sourceXmlContent = _fileSystem.ReadAllText(fromFilePath);
        var sourceDoc = new XmlDocument();
        sourceDoc.LoadXml(sourceXmlContent);
        if (sourceDoc.DocumentElement is null) return 0;
        var sourceFormgen = new DotFormgen(sourceDoc.DocumentElement);

        var sourcePage = sourceFormgen.Pages.FirstOrDefault(p => (p.Settings?.PageNumber ?? 0) == sourcePageNumber) 
                         ?? (sourcePageNumber > 0 && sourcePageNumber <= sourceFormgen.Pages.Count ? sourceFormgen.Pages[sourcePageNumber - 1] : null);

        if (sourcePage == null) return 0;

        var targetPage = ParsedFormgenFile.Pages.FirstOrDefault(p => (p.Settings?.PageNumber ?? 0) == targetPageNumber)
                         ?? (targetPageNumber > 0 && targetPageNumber <= ParsedFormgenFile.Pages.Count ? ParsedFormgenFile.Pages[targetPageNumber - 1] : null);

        if (targetPage == null) return 0;

        int maxId = 0;
        foreach (var p in ParsedFormgenFile.Pages)
        {
            foreach (var f in p.Fields)
            {
                if (f.Settings != null && f.Settings.ID > maxId)
                    maxId = f.Settings.ID;
            }
        }

        int count = 0;
        foreach (var field in sourcePage.Fields)
        {
            maxId++;
            var cloned = new FormField(field, maxId);
            targetPage.Fields.Add(cloned);
            count++;
        }

        OnFormgenFileChanged();
        _logger?.LogInfo($"Imported {count} fields from {fromFilePath} (Page {sourcePageNumber}) to Page {targetPageNumber}.");
        return count;
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
