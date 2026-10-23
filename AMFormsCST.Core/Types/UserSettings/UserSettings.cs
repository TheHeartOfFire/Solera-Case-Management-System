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
