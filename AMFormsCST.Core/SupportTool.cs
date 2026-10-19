using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.BestPractices;
using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Utils;

namespace AMFormsCST.Core;
public class SupportTool : ISupportTool
{
    public IBestPracticeEnforcer Enforcer { get; set; }
    public ICodeBlocks CodeBlocks { get; set; }
    public IFormgenUtils FormgenUtils { get; set; }
    public INotebook Notebook { get; set; }
    public ISettings Settings { get; set; }
    private readonly Properties _properties;

    public ILogService? Logger { get; }
    public SupportTool(
        IFileSystem fileSystem,
        IFormNameBestPractice formNameBestPractice,
        ISettings defaultSettings,
        ITemplateRepository templateRepository,
        INotebook notebook,
        ILogService? logger = null)
    {
        Logger = logger;
        Logger?.LogInfo("Initializing SupportTool.");

        IO.ConfigureLogger(logger);

        _properties = IO.LoadConfig() ?? new Properties(logger);

        CodeBlocks = new CodeBlocks(logger, enablePersistence: true);
        Notebook = notebook ?? new Notebook(logger);
        FormgenUtils = new FormgenUtils(fileSystem, _properties.FormgenUtils, logger);
        Enforcer = new BestPracticeEnforcer(formNameBestPractice, templateRepository, logger);

        Settings = IO.LoadSettings() ?? defaultSettings;
        Settings.UserSettings.Organization.InstantiateVariables(this);

        Logger?.LogInfo("SupportTool initialized successfully.");
    }

    public void SaveAllSettings()
    {
        Logger?.LogInfo("Saving all settings.");
        IO.SaveSettings(Settings);
        IO.SaveConfig(_properties);
        CodeBlocks.SaveCustomBlocks();
        Logger?.LogInfo("Settings saved.");
    }
}
