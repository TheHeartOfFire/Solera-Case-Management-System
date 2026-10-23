namespace AMFormsCST.Core.Interfaces.UserSettings;

public interface IUserSettings : ISetting
{
    IOrgVariables Organization { get; set; }
    string Name { get; set; }
    string ApplicationFilesPath { get; set; }
    string ExtSeparator { get; set; }
    bool EnableExplorerContextMenu { get; set; }
    string FormGenExecutablePath { get; set; }
}
