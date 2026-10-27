using AMFormsCST.Core.Types.UserSettings;

namespace AMFormsCST.Core.Interfaces.UserSettings;

public interface IUserSettings : ISetting
{
    IOrgVariables Organization { get; set; }
    IAutomateFormsSettings FormsSettings { get; set; }

    // User Profile
    string Name { get; set; }
    string Email { get; set; }
    string Phone { get; set; }
    string Title { get; set; }
    UserRole Role { get; set; }

    // General Solera Support Contact
    string SupportEmail { get; set; }
    string SupportPhone { get; set; }

    // General App Settings
    string ApplicationFilesPath { get; set; }
    string ExtSeparator { get; set; }
    bool EnableExplorerContextMenu { get; set; }

    // Forms / FormGen Settings (delegated to FormsSettings)
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
