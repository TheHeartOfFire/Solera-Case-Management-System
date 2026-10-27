using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Interfaces;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Types.UserSettings;

public class UserSettings : IUserSettings
{
    private readonly ILogService? _logger;

    private IOrgVariables _organization;
    private IAutomateFormsSettings _formsSettings;

    private string _name = string.Empty;
    private string _email = string.Empty;
    private string _phone = string.Empty;
    private string _title = string.Empty;
    private UserRole _role = UserRole.AutomateForms;

    private string _supportEmail = "automate-support@solera.com";
    private string _supportPhone = "800-xxx-xxxx";

    private string _applicationFilesPath = string.Empty;
    private string _extSeparator = "x";

    [JsonInclude]
    public IAutomateFormsSettings FormsSettings
    {
        get => _formsSettings;
        set
        {
            _formsSettings = value ?? new AutomateFormsSettings(_logger);
            _logger?.LogInfo("FormsSettings property updated.");
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            value ??= string.Empty;
            if (_name != value)
            {
                _name = value;
                _logger?.LogInfo($"Name property set to '{value}'.");
            }
        }
    }

    public string Email
    {
        get => _email;
        set
        {
            value ??= string.Empty;
            if (_email != value)
            {
                _email = value;
                _logger?.LogInfo($"Email property set to '{value}'.");
            }
        }
    }

    public string Phone
    {
        get => _phone;
        set
        {
            value ??= string.Empty;
            if (_phone != value)
            {
                _phone = value;
                _logger?.LogInfo($"Phone property set to '{value}'.");
            }
        }
    }

    public string Title
    {
        get => _title;
        set
        {
            value ??= string.Empty;
            if (_title != value)
            {
                _title = value;
                _logger?.LogInfo($"Title property set to '{value}'.");
            }
        }
    }

    public UserRole Role
    {
        get => _role;
        set
        {
            if (_role != value)
            {
                _role = value;
                _logger?.LogInfo($"Role property set to '{value}'.");
            }
        }
    }

    public string SupportEmail
    {
        get => _supportEmail;
        set
        {
            value ??= string.Empty;
            if (_supportEmail != value)
            {
                _supportEmail = value;
                _logger?.LogInfo($"SupportEmail property set to '{value}'.");
            }
        }
    }

    public string SupportPhone
    {
        get => _supportPhone;
        set
        {
            value ??= string.Empty;
            if (_supportPhone != value)
            {
                _supportPhone = value;
                _logger?.LogInfo($"SupportPhone property set to '{value}'.");
            }
        }
    }

    public string ApplicationFilesPath
    {
        get => _applicationFilesPath;
        set
        {
            value ??= string.Empty;
            if (_applicationFilesPath != value)
            {
                _applicationFilesPath = value;
                _logger?.LogInfo($"ApplicationFilesPath property set to '{value}'.");
            }
        }
    }

    public string ExtSeparator
    {
        get => _extSeparator;
        set
        {
            value ??= string.Empty;
            if (_extSeparator != value)
            {
                _extSeparator = value;
                _logger?.LogInfo($"ExtSeparator property set to '{value}'.");
            }
        }
    }

    public bool EnableExplorerContextMenu
    {
        get => _formsSettings.EnableExplorerContextMenu;
        set => _formsSettings.EnableExplorerContextMenu = value;
    }

    #region Delegated Forms / FormGen Settings (Stored in FormsSettings)

    public bool IsFormGenPatched
    {
        get => _formsSettings.IsFormGenPatched;
        set => _formsSettings.IsFormGenPatched = value;
    }

    public bool FormGenPreserveUdfTab
    {
        get => _formsSettings.FormGenPreserveUdfTab;
        set => _formsSettings.FormGenPreserveUdfTab = value;
    }

    public bool FormGenCanvasPasteField
    {
        get => _formsSettings.FormGenCanvasPasteField;
        set => _formsSettings.FormGenCanvasPasteField = value;
    }

    public bool FormGenAlreadyOpenToast
    {
        get => _formsSettings.FormGenAlreadyOpenToast;
        set => _formsSettings.FormGenAlreadyOpenToast = value;
    }

    public bool FormGenContextMenus
    {
        get => _formsSettings.FormGenContextMenus;
        set => _formsSettings.FormGenContextMenus = value;
    }

    public bool FormGenEditorUndoRedo
    {
        get => _formsSettings.FormGenEditorUndoRedo;
        set => _formsSettings.FormGenEditorUndoRedo = value;
    }

    public bool FormGenDialogAutoFill
    {
        get => _formsSettings.FormGenDialogAutoFill;
        set => _formsSettings.FormGenDialogAutoFill = value;
    }

    public bool FormGenAutoRestoreForms
    {
        get => _formsSettings.FormGenAutoRestoreForms;
        set => _formsSettings.FormGenAutoRestoreForms = value;
    }

    public bool EnableShiftRightClickFlyout
    {
        get => _formsSettings.EnableShiftRightClickFlyout;
        set => _formsSettings.EnableShiftRightClickFlyout = value;
    }

    public string FormGenExecutablePath
    {
        get => _formsSettings.FormGenExecutablePath;
        set => _formsSettings.FormGenExecutablePath = value;
    }

    #endregion

    public IOrgVariables Organization
    {
        get => _organization;
        set
        {
            if (value is null)
            {
                var ex = new ArgumentNullException(nameof(value), "Organization cannot be null.");
                _logger?.LogError("Attempted to set Organization to null.", ex);
                throw ex;
            }
            if (_organization != value)
            {
                _organization = value;
                _logger?.LogInfo("Organization property set.");
            }
        }
    }

    [JsonConstructor]
    public UserSettings(IOrgVariables organization)
        : this(organization, (ILogService?)null) { }

    public UserSettings(IOrgVariables organization, ILogService? logger = null)
        : this(organization, new AutomateFormsSettings(logger), logger) { }

    public UserSettings(IOrgVariables organization, IAutomateFormsSettings formsSettings, ILogService? logger = null)
    {
        _logger = logger;
        if (organization is null)
        {
            var ex = new ArgumentNullException(nameof(organization), "Organization cannot be null.");
            _logger?.LogError("Constructor received null for organization.", ex);
            throw ex;
        }
        _organization = organization;
        _formsSettings = formsSettings ?? new AutomateFormsSettings(logger);
        _logger?.LogInfo("UserSettings initialized.");
    }
}

