namespace AMFormsCST.Core.Interfaces.UserSettings;

public interface IUserSettings : ISetting
{
    IOrgVariables Organization { get; set; }
    string Name { get; set; }
    string ApplicationFilesPath { get; set; }
    string ExtSeparator { get; set; }
    bool EnableExplorerContextMenu { get; set; }
    string FormGenExecutablePath { get; set; }
    bool IsFormGenPatched { get; set; }
    bool FormGenPreserveUdfTab { get; set; }
    bool FormGenCanvasPasteField { get; set; }
    bool FormGenAlreadyOpenToast { get; set; }
    bool FormGenContextMenus { get; set; }
    bool FormGenEditorUndoRedo { get; set; }
    bool FormGenDialogAutoFill { get; set; }
    bool FormGenAutoRestoreForms { get; set; }
    bool EnableShiftRightClickFlyout { get; set; }
}
