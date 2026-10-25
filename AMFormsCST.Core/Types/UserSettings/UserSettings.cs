using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Interfaces;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Types.UserSettings;

public class UserSettings : IUserSettings
{
    private readonly ILogService? _logger;

    private IOrgVariables _organization;
    private string _name = string.Empty;
    private string _applicationFilesPath = string.Empty;
    private string _extSeparator = "x";
    private bool _enableExplorerContextMenu = true;
    private string _formGenExecutablePath = string.Empty;
    private bool _isFormGenPatched = true;
    private bool _formGenPreserveUdfTab = true;
    private bool _formGenCanvasPasteField = true;
    private bool _formGenAlreadyOpenToast = true;
    private bool _formGenContextMenus = true;
    private bool _formGenEditorUndoRedo = true;
    private bool _formGenDialogAutoFill = true;
    private bool _formGenAutoRestoreForms = true;
    private bool _enableShiftRightClickFlyout = true;

    public bool IsFormGenPatched
    {
        get => _isFormGenPatched;
        set
        {
            if (_isFormGenPatched != value)
            {
                _isFormGenPatched = value;
                _logger?.LogInfo($"IsFormGenPatched property set to '{value}'.");
            }
        }
    }

    public bool FormGenPreserveUdfTab
    {
        get => _formGenPreserveUdfTab;
        set
        {
            if (_formGenPreserveUdfTab != value)
            {
                _formGenPreserveUdfTab = value;
                _logger?.LogInfo($"FormGenPreserveUdfTab property set to '{value}'.");
            }
        }
    }

    public bool FormGenCanvasPasteField
    {
        get => _formGenCanvasPasteField;
        set
        {
            if (_formGenCanvasPasteField != value)
            {
                _formGenCanvasPasteField = value;
                _logger?.LogInfo($"FormGenCanvasPasteField property set to '{value}'.");
            }
        }
    }

    public bool FormGenAlreadyOpenToast
    {
        get => _formGenAlreadyOpenToast;
        set
        {
            if (_formGenAlreadyOpenToast != value)
            {
                _formGenAlreadyOpenToast = value;
                _logger?.LogInfo($"FormGenAlreadyOpenToast property set to '{value}'.");
            }
        }
    }

    public bool FormGenContextMenus
    {
        get => _formGenContextMenus;
        set
        {
            if (_formGenContextMenus != value)
            {
                _formGenContextMenus = value;
                _logger?.LogInfo($"FormGenContextMenus property set to '{value}'.");
            }
        }
    }

    public bool FormGenEditorUndoRedo
    {
        get => _formGenEditorUndoRedo;
        set
        {
            if (_formGenEditorUndoRedo != value)
            {
                _formGenEditorUndoRedo = value;
                _logger?.LogInfo($"FormGenEditorUndoRedo property set to '{value}'.");
            }
        }
    }

    public bool FormGenDialogAutoFill
    {
        get => _formGenDialogAutoFill;
        set
        {
            if (_formGenDialogAutoFill != value)
            {
                _formGenDialogAutoFill = value;
                _logger?.LogInfo($"FormGenDialogAutoFill property set to '{value}'.");
            }
        }
    }

    public bool FormGenAutoRestoreForms
    {
        get => _formGenAutoRestoreForms;
        set
        {
            if (_formGenAutoRestoreForms != value)
            {
                _formGenAutoRestoreForms = value;
                _logger?.LogInfo($"FormGenAutoRestoreForms property set to '{value}'.");
            }
        }
    }

    public bool EnableShiftRightClickFlyout
    {
        get => _enableShiftRightClickFlyout;
        set
        {
            if (_enableShiftRightClickFlyout != value)
            {
                _enableShiftRightClickFlyout = value;
                _logger?.LogInfo($"EnableShiftRightClickFlyout property set to '{value}'.");
            }
        }
    }

    public bool EnableExplorerContextMenu
    {
        get => _enableExplorerContextMenu;
        set
        {
            if (_enableExplorerContextMenu != value)
            {
                _enableExplorerContextMenu = value;
                _logger?.LogInfo($"EnableExplorerContextMenu property set to '{value}'.");
            }
        }
    }

    public string FormGenExecutablePath
    {
        get => _formGenExecutablePath;
        set
        {
            value ??= string.Empty;
            if (_formGenExecutablePath != value)
            {
                _formGenExecutablePath = value;
                _logger?.LogInfo($"FormGenExecutablePath property set to '{value}'.");
            }
        }
    }

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

    public string Name
    {
        get => _name;
        set
        {
            if (value is null)
            {
                var ex = new ArgumentNullException(nameof(value), "Name cannot be null.");
                _logger?.LogError("Attempted to set Name to null.", ex);
                throw ex;
            }
            if (_name != value)
            {
                _name = value;
                _logger?.LogInfo($"Name property set to '{value}'.");
            }
        }
    }

    public string ApplicationFilesPath
    {
        get => _applicationFilesPath;
        set
        {
            if (value is null)
            {
                var ex = new ArgumentNullException(nameof(value), "ApplicationFilesPath cannot be null.");
                _logger?.LogError("Attempted to set ApplicationFilesPath to null.", ex);
                throw ex;
            }
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
            if (value is null)
            {
                var ex = new ArgumentNullException(nameof(value), "ExtSeparator cannot be null.");
                _logger?.LogError("Attempted to set ExtSeparator to null.", ex);
                throw ex;
            }
            if (_extSeparator != value)
            {
                _extSeparator = value;
                _logger?.LogInfo($"ExtSeparator property set to '{value}'.");
            }
        }
    }

    [JsonConstructor]
    public UserSettings(IOrgVariables organization)
        : this(organization, null) { }

    public UserSettings(IOrgVariables organization, ILogService? logger = null)
    {
        _logger = logger;
        if (organization is null)
        {
            var ex = new ArgumentNullException(nameof(organization), "Organization cannot be null.");
            _logger?.LogError("Constructor received null for organization.", ex);
            throw ex;
        }
        _organization = organization;
        _logger?.LogInfo("UserSettings initialized.");
    }
}
