using AMFormsCST.Core.Converters;
using AMFormsCST.Core.Helpers;
using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Core.Utils;
using System.Text.Json;
using System.Xml;
using System.IO;

namespace AMFormsCST.Core;

public static class IO
{
    private static readonly string _appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string _rootPath;
    private static readonly string _notesPath;
    private static readonly string _settingsPath;
    public static readonly string BackupPath;
    private static readonly string _templatesPath;
    private static readonly string _configPath;
    private static JsonSerializerOptions _jsonOptions = new() { WriteIndented = true }; 

    public static ILogService? Logger { get; private set; }

    public static void ConfigureLogger(ILogService? logger = null)
    {
        if (logger == null) return;
        Logger = logger;
        Logger?.LogInfo("IO logger configured.");
    }

    public static string BackupFormgenFilePath(string uuid) => $"{BackupPath}\\{uuid}\\{DateTime.Now:mm-dd-yyyy.hh-mm-ss}.bak";

    static IO()
    {
        _rootPath = Path.Combine(_appData, "Solera Case Management Tool");
        _notesPath = Path.Combine(_rootPath, "SavedNotes.json");
        _settingsPath = Path.Combine(_rootPath, "AppSettings.json");
        BackupPath = Path.Combine(_rootPath, "FormgenBackup");
        _templatesPath = Path.Combine(_rootPath, "TextTemplates.json");
        _configPath = Path.Combine(_rootPath, "Config.json");

        try
        {
            if (!Directory.Exists(_rootPath)) Directory.CreateDirectory(_rootPath);
            if (!Directory.Exists(BackupPath)) Directory.CreateDirectory(BackupPath);
        }
        catch (Exception ex)
        {
            Logger?.LogError("Error creating application directories.", ex);
        }
    }

    /// <summary>
    /// Configures the JSON serializer with options provided by the UI project.
    /// </summary>
    public static void ConfigureJson(JsonSerializerOptions options)
    {
        _jsonOptions = options;
        _jsonOptions.Converters.Add(new SelectableListJsonConverter<ICompany>());
        _jsonOptions.Converters.Add(new SelectableListJsonConverter<IContact>());
        _jsonOptions.Converters.Add(new SelectableListJsonConverter<IDealer>());
        _jsonOptions.Converters.Add(new SelectableListJsonConverter<IForm>());
        _jsonOptions.Converters.Add(new SelectableListJsonConverter<ITestDeal>());
        _jsonOptions.Converters.Add(new SelectableListJsonConverter<INote>());
        _jsonOptions.Converters.Add(new TextTemplateJsonConverter());
        Logger?.LogInfo("IO JSON serializer configured.");
    }

    public static void SaveSettings(ISettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_settingsPath, json);
            Logger?.LogInfo("Settings saved.");
        }
        catch (Exception ex)
        {
            Logger?.LogError("Error saving settings.", ex);
        }
    }

    public static ISettings? LoadSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            Logger?.LogWarning("Settings file not found.");
            return null;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<ISettings>(json, _jsonOptions);
            Logger?.LogInfo("Settings loaded.");
            return settings;
        }
        catch (Exception ex)
        {
            Logger?.LogError("Error loading settings.", ex);
            return null;
        }
    }

    public static void BackupFormgenFile(string uuid, XmlDocument file, uint? retentionCount)
    {
        try
        {
            var di = Directory.CreateDirectory($"{BackupPath}\\{uuid}");

            if(retentionCount is not null && di.EnumerateFiles().Count() > retentionCount)
            {
                var files = di.EnumerateFiles().OrderByDescending(x => x.LastWriteTime).Skip((int)retentionCount);
                foreach (var fileToDelete in files)
                {
                    fileToDelete.Delete();
                    Logger?.LogInfo($"Deleted old backup: {fileToDelete.FullName}");
                }
            }

            file.Save(BackupFormgenFilePath(uuid));
            Logger?.LogInfo($"Backup created for Formgen file: {uuid}");
        }
        catch (Exception ex)
        {
            Logger?.LogError("Error creating Formgen backup.", ex);
        }
    }

    public static string AutoIncrement(string? input)
    {
        if (input == null) return input ?? string.Empty;

        var index = input.Length - 1;
        while (int.TryParse(input[index].ToString(), out _))
        {
            index--;
        }

        _ = int.TryParse(input.AsSpan(index + 1), out int number);

        number++;
        var output = string.Concat(input.AsSpan(0, index + 1), number.ToString());
        return output;
    }

    public static void SaveNotes(List<INote> notes)
    {
        var concreteNotes = notes.Cast<INote>().ToList(); 

        var json = JsonSerializer.Serialize(concreteNotes, _jsonOptions);

        try
        {
            File.WriteAllText(_notesPath, json);
            Logger?.LogInfo("Notes saved.");
        }
        catch (Exception ex)
        {
            Logger?.LogError("Error saving Notes.", ex);
        }
    }
    public static List<INote> LoadNotes()
    {
        if (!File.Exists(_notesPath))
        {
            SaveNotes([]); 
            Logger?.LogWarning("Notes file not found. Created new file.");
            return [];
        }

        try
        {
            var json = File.ReadAllText(_notesPath);

            var deserializedNotes = JsonSerializer.Deserialize<List<INote>>(json, _jsonOptions);
            Logger?.LogInfo("Notes loaded.");
            return deserializedNotes ?? [];
        }
        catch (JsonException ex)
        {
            Logger?.LogError("Error deserializing Notes.", ex);
            return [];
        }
        catch (Exception ex)
        {
            Logger?.LogError("Error loading Notes file.", ex);
            return [];
        }
    }

    public static List<TextTemplate> LoadTemplates()
    {
        if (!File.Exists(_templatesPath))
        {
            SaveTemplates([]); 
            Logger?.LogWarning("TextTemplates file not found. Created new file.");
            return []; 
        }

        try
        {
            var json = File.ReadAllText(_templatesPath);
            var templates = JsonSerializer.Deserialize<List<TextTemplate>>(json, _jsonOptions);
            Logger?.LogInfo("TextTemplates loaded.");
            return templates ?? []; 
        }
        catch (JsonException ex)
        {
            Logger?.LogError("Error deserializing TextTemplates.", ex);
            return []; 
        }
        catch (Exception ex) 
        {
            Logger?.LogError("Error loading TextTemplates file.", ex);
            return [];
        }
    }
    public static void SaveTemplates(List<TextTemplate> templates)
    {
        try
        {
            var json = JsonSerializer.Serialize(templates, _jsonOptions);

            File.WriteAllText(_templatesPath, json);
            Logger?.LogInfo("TextTemplates saved.");
        }
        catch (Exception ex) 
        {
            Logger?.LogError("Error saving TextTemplates.", ex);
        }
    }

    public static void SaveConfig(Properties config)
    {
        try
        {
            var json = JsonSerializer.Serialize(config, _jsonOptions);
            File.WriteAllText(_configPath, json);
            Logger?.LogInfo("Config saved.");
        }
        catch (Exception ex)
        {
            Logger?.LogError("Error saving config.", ex);
        }
    }

    public static Properties? LoadConfig()
    {
        if (!File.Exists(_configPath))
        {
            Logger?.LogWarning("Config file not found.");
            return null;
        }

        try
        {
            var json = File.ReadAllText(_configPath);
            var config = JsonSerializer.Deserialize<Properties>(json, _jsonOptions);
            Logger?.LogInfo("Config loaded.");
            return config;
        }
        catch (Exception ex)
        {
            Logger?.LogError("Error loading config.", ex);
            return null;
        }
    }
}
