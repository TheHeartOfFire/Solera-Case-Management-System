namespace AMFormsCST.Desktop.Services.Interfaces;

public enum BrowserType
{
    OperaGX,
    Edge,
    Chrome
}

/// <summary>
/// Service for registering and launching browser extensions for Opera GX, Edge, and Chrome.
/// </summary>
public interface IBrowserIntegrationService
{
    /// <summary>
    /// Gets the fixed deterministic Extension ID for the Solera CMS browser extension.
    /// </summary>
    string ExtensionId { get; }

    /// <summary>
    /// Checks whether the native messaging host is registered in Windows Registry for Opera GX or other browsers.
    /// </summary>
    bool IsRegistered();

    /// <summary>
    /// Registers the native messaging host manifest in HKCU for Opera GX, Microsoft Edge, and Google Chrome.
    /// </summary>
    void RegisterNativeMessagingHost();

    /// <summary>
    /// Removes native messaging host registrations from HKCU.
    /// </summary>
    void UnregisterNativeMessagingHost();

    /// <summary>
    /// Automatically launches the selected browser with the extension loaded via --load-extension.
    /// </summary>
    /// <param name="browserType">The browser to launch.</param>
    /// <returns>True if the browser was successfully launched; false otherwise.</returns>
    bool LaunchBrowserWithExtension(BrowserType browserType);

    /// <summary>
    /// Checks if the specified browser is installed on this machine.
    /// </summary>
    bool IsBrowserInstalled(BrowserType browserType);
}

