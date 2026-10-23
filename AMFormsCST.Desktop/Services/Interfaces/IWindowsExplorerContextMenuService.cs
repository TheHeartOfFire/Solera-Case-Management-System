namespace AMFormsCST.Desktop.Services.Interfaces;

/// <summary>
/// Service for managing Windows Explorer context menu entries for .formgen files.
/// </summary>
public interface IWindowsExplorerContextMenuService
{
    /// <summary>
    /// Checks whether the Solera Formgen Utilities context menu entry is currently registered in HKCU.
    /// </summary>
    bool IsRegistered();

    /// <summary>
    /// Registers shell context verbs for .formgen files in HKCU.
    /// </summary>
    /// <param name="formgenExePath">Optional path to the native FormGen executable.</param>
    void RegisterContextMenu(string? formgenExePath = null);

    /// <summary>
    /// Removes shell context verbs for .formgen files from HKCU.
    /// </summary>
    void UnregisterContextMenu();

    /// <summary>
    /// Synchronizes context menu registration with the user's settings.
    /// </summary>
    void SyncWithSettings(bool isEnabled, string? formgenExePath = null);

    /// <summary>
    /// Checks whether .formgen files are currently associated with FormGen in HKCU.
    /// </summary>
    bool IsFormgenAssociated();

    /// <summary>
    /// Associates .formgen and .fga files with the specified FormGen executable, batch file, or jar as the default open handler.
    /// </summary>
    /// <param name="formgenPath">Path to formgen executable, batch file, or jar.</param>
    /// <returns>True if association succeeded; false otherwise.</returns>
    bool AssociateFormgen(string formgenPath);
}

