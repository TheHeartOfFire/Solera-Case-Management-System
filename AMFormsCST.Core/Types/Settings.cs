using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Interfaces;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Types;
public class Settings : ISettings
{
    private readonly ILogService? _logger;

    [JsonIgnore]
    public List<ISetting> AllSettings => [UserSettings, UiSettings];

    [JsonInclude]
    public IUserSettings UserSettings
    {
        get => _userSettings;
        set
        {
            if (value is null)
            {
                var ex = new ArgumentNullException(nameof(value), "User settings cannot be null.");
                _logger?.LogError("Attempted to set UserSettings to null.", ex);
                throw ex;
            }
            if (_userSettings != value)
            {
                _userSettings = value;
                _logger?.LogInfo("UserSettings property set.");
            }
        }
    }

    [JsonInclude]
    public IUiSettings UiSettings
    {
        get => _uiSettings;
        set
        {
            if (value is null)
            {
                var ex = new ArgumentNullException(nameof(value), "UI settings cannot be null.");
                _logger?.LogError("Attempted to set UiSettings to null.", ex);
                throw ex;
            }
            if (_uiSettings != value)
            {
                _uiSettings = value;
                _logger?.LogInfo("UiSettings property set.");
            }
        }
    }

    private IUserSettings _userSettings;
    private IUiSettings _uiSettings;

    [JsonConstructor]
    public Settings(IUserSettings userSettings, IUiSettings uiSettings)
    {
        _userSettings = userSettings ?? throw new ArgumentNullException(nameof(userSettings), "User settings cannot be null.");
        _uiSettings = uiSettings ?? throw new ArgumentNullException(nameof(uiSettings), "UI settings cannot be null.");
    }

    public Settings(IUserSettings userSettings, IUiSettings uiSettings, ILogService? logger)
        : this(userSettings, uiSettings)
    {
        _logger = logger;
        _logger?.LogInfo("Settings initialized.");
    }
}
