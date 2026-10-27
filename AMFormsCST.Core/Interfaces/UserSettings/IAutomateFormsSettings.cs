using AMFormsCST.Core.Types.UserSettings;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Interfaces.UserSettings;

[JsonDerivedType(typeof(AutomateFormsSettings), typeDiscriminator: "automateFormsSettings")]
public interface IAutomateFormsSettings : ISetting
{
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
    bool EnableExplorerContextMenu { get; set; }

    // Forms team mailing address
    string MailingName { get; set; }
    string StreetAddress { get; set; }
    string City { get; set; }
    string State { get; set; }
    string Zip { get; set; }
    string MailingAddress { get; }
}

