using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Types.UserSettings;
using Moq;
using Xunit;

namespace AMFormsCST.Test.Core.Types.UserSettings;

public class AutomateFormsSettingsTests
{
    [Fact]
    public void AutomateFormsSettings_InitializesWithDefaultValues()
    {
        var formsSettings = new AutomateFormsSettings();

        Assert.True(formsSettings.IsFormGenPatched);
        Assert.True(formsSettings.FormGenPreserveUdfTab);
        Assert.True(formsSettings.FormGenCanvasPasteField);
        Assert.True(formsSettings.FormGenAlreadyOpenToast);
        Assert.True(formsSettings.FormGenContextMenus);
        Assert.True(formsSettings.FormGenEditorUndoRedo);
        Assert.True(formsSettings.FormGenDialogAutoFill);
        Assert.True(formsSettings.FormGenAutoRestoreForms);
        Assert.True(formsSettings.EnableShiftRightClickFlyout);
        Assert.True(formsSettings.EnableExplorerContextMenu);
        Assert.Equal(string.Empty, formsSettings.FormGenExecutablePath);
        Assert.Equal("Attn: A/M Forms (Sue)", formsSettings.MailingName);
        Assert.Equal("131 Griffis Rd", formsSettings.StreetAddress);
        Assert.Equal("Gloversville", formsSettings.City);
        Assert.Equal("NY", formsSettings.State);
        Assert.Equal("12078", formsSettings.Zip);
        Assert.Contains("Attn: A/M Forms (Sue)", formsSettings.MailingAddress);
        Assert.Contains("131 Griffis Rd", formsSettings.MailingAddress);
    }

    [Fact]
    public void UserSettings_DelegatesFormGenPropertiesTo_FormsSettings()
    {
        var mockOrg = new Mock<IOrgVariables>();
        var userSettings = new AMFormsCST.Core.Types.UserSettings.UserSettings(mockOrg.Object);

        Assert.NotNull(userSettings.FormsSettings);
        Assert.Equal(UserRole.AutomateForms, userSettings.Role);

        // Mutating through UserSettings proxy
        userSettings.IsFormGenPatched = false;
        userSettings.FormGenExecutablePath = @"C:\Formgen\NewFormGen.bat";
        userSettings.FormGenCanvasPasteField = false;
        userSettings.EnableExplorerContextMenu = false;

        // Verifying direct FormsSettings reflects mutations
        Assert.False(userSettings.FormsSettings.IsFormGenPatched);
        Assert.Equal(@"C:\Formgen\NewFormGen.bat", userSettings.FormsSettings.FormGenExecutablePath);
        Assert.False(userSettings.FormsSettings.FormGenCanvasPasteField);
        Assert.False(userSettings.FormsSettings.EnableExplorerContextMenu);

        // Mutating FormsSettings directly
        userSettings.FormsSettings.FormGenPreserveUdfTab = false;
        Assert.False(userSettings.FormGenPreserveUdfTab);
        userSettings.FormsSettings.EnableExplorerContextMenu = true;
        Assert.True(userSettings.EnableExplorerContextMenu);
    }

    [Fact]
    public void UserSettings_ProfileProperties_CanBeSetAndRetrieved()
    {
        var mockOrg = new Mock<IOrgVariables>();
        var userSettings = new AMFormsCST.Core.Types.UserSettings.UserSettings(mockOrg.Object);

        userSettings.Name = "Jane Doe";
        userSettings.Email = "jane.doe@solera.com";
        userSettings.Phone = "518-555-1234";
        userSettings.Title = "Forms Architect";
        userSettings.Role = UserRole.CustomerSupport;
        userSettings.SupportEmail = "custom-support@solera.com";
        userSettings.SupportPhone = "888-555-4321";

        Assert.Equal("Jane Doe", userSettings.Name);
        Assert.Equal("jane.doe@solera.com", userSettings.Email);
        Assert.Equal("518-555-1234", userSettings.Phone);
        Assert.Equal("Forms Architect", userSettings.Title);
        Assert.Equal(UserRole.CustomerSupport, userSettings.Role);
        Assert.Equal("custom-support@solera.com", userSettings.SupportEmail);
        Assert.Equal("888-555-4321", userSettings.SupportPhone);
    }
}

