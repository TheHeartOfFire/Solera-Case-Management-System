using AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure;

namespace AMFormsCST.Core.Interfaces.Utils;

public enum FieldAlignmentMode
{
    Left,
    Right,
    Top,
    Bottom,
    CenterHorizontal,
    CenterVertical,
    MatchWidth,
    MatchHeight
}

public enum FieldDistributionMode
{
    Horizontal,
    Vertical
}

public interface IFormgenUtils
{
    string? FileName { get; }
    DotFormgen? ParsedFormgenFile { get; }

    void ClonePrompt(CodeLine[] selected);
    void DeletePrompts(IEnumerable<CodeLine> prompts);
    void MovePrompt(CodeLine prompt, bool moveUp);
    FormField CloneField(FormPage page, FormField field);
    void DeleteFields(FormPage page, IEnumerable<FormField> fields);
    void AlignFields(IEnumerable<FormField> fields, FieldAlignmentMode mode);
    void DistributeFields(IEnumerable<FormField> fields, FieldDistributionMode mode);
    void BulkSetFieldProperties(IEnumerable<FormField> fields, bool? bold = null, int? fontSize = null, FormFieldSettings.Alignment? fontAlignment = null, FormField.FormatOption? formatOption = null);
    int AlignFieldsAcrossPages(int sourcePageIndex, IEnumerable<int>? targetPageIndices = null);
    int ImportPromptsFrom(string fromFilePath, IEnumerable<string>? variableNames = null);
    int ImportFieldsFrom(string fromFilePath, int sourcePageNumber, int targetPageNumber);

    void CloseFile();
    void CopyPromptsTo(string fromFilePath);
    void CreateBackup();
    void EditPrompts(CodeLine[] prompts);
    CodeLine[] GetCodeLines(CodeLineSettings.CodeType type);
    CodeLine[] GetPrompts();
    void LoadBackup(string backupPath);
    void OpenFile(string filePath);
    void RenameFile(string newName, bool renameImage);
    void SaveFile(string filePath, DotFormgen? fileToSave = null);
    void RegenerateUUID();
    bool HasChanged { get; }
    event EventHandler? FormgenFileChanged;
}