using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.UserSettings;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Types.UserSettings;

public class AutomateFormsSettings : IAutomateFormsSettings
{
    private readonly ILogService? _logger;

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

    private string _mailingName = "Attn: A/M Forms (Sue)";
    private string _streetAddress = "131 Griffis Rd";
    private string _city = "Gloversville";
    private string _state = "NY";
    private string _zip = "12078";

    public string FormGenExecutablePath
    {
        get => _formGenExecutablePath;
        set
        {
            value ??= string.Empty;
            if (_formGenExecutablePath != value)
            {
                _formGenExecutablePath = value;
                _logger?.LogInfo($"FormGenExecutablePath set to '{value}'.");
            }
        }
    }

    public bool IsFormGenPatched
    {
        get => _isFormGenPatched;
        set
        {
            if (_isFormGenPatched != value)
            {
                _isFormGenPatched = value;
                _logger?.LogInfo($"IsFormGenPatched set to '{value}'.");
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
                _logger?.LogInfo($"FormGenPreserveUdfTab set to '{value}'.");
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
                _logger?.LogInfo($"FormGenCanvasPasteField set to '{value}'.");
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
                _logger?.LogInfo($"FormGenAlreadyOpenToast set to '{value}'.");
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
                _logger?.LogInfo($"FormGenContextMenus set to '{value}'.");
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
                _logger?.LogInfo($"FormGenEditorUndoRedo set to '{value}'.");
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
                _logger?.LogInfo($"FormGenDialogAutoFill set to '{value}'.");
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
                _logger?.LogInfo($"FormGenAutoRestoreForms set to '{value}'.");
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
                _logger?.LogInfo($"EnableShiftRightClickFlyout set to '{value}'.");
            }
        }
    }

    public string MailingName
    {
        get => _mailingName;
        set
        {
            value ??= string.Empty;
            if (_mailingName != value)
            {
                _mailingName = value;
                _logger?.LogInfo($"MailingName set to '{value}'.");
            }
        }
    }

    public string StreetAddress
    {
        get => _streetAddress;
        set
        {
            value ??= string.Empty;
            if (_streetAddress != value)
            {
                _streetAddress = value;
                _logger?.LogInfo($"StreetAddress set to '{value}'.");
            }
        }
    }

    public string City
    {
        get => _city;
        set
        {
            value ??= string.Empty;
            if (_city != value)
            {
                _city = value;
                _logger?.LogInfo($"City set to '{value}'.");
            }
        }
    }

    public string State
    {
        get => _state;
        set
        {
            value ??= string.Empty;
            if (_state != value)
            {
                _state = value;
                _logger?.LogInfo($"State set to '{value}'.");
            }
        }
    }

    public string Zip
    {
        get => _zip;
        set
        {
            value ??= string.Empty;
            if (_zip != value)
            {
                _zip = value;
                _logger?.LogInfo($"Zip set to '{value}'.");
            }
        }
    }

    [JsonIgnore]
    public string MailingAddress =>
        string.IsNullOrWhiteSpace(StreetAddress)
            ? MailingName
            : $"{MailingName}\n{StreetAddress}\n{City}, {State} {Zip}".Trim();

    [JsonConstructor]
    public AutomateFormsSettings() : this(null) { }

    public AutomateFormsSettings(ILogService? logger)
    {
        _logger = logger;
    }
}

