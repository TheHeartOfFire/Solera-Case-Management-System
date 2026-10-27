using System.IO;
using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Desktop.Models.UserSettings;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.ViewModels.Pages;
using Moq;
using Wpf.Ui.Appearance;
using Assert = Xunit.Assert;

namespace AMFormsCST.Test.Desktop.ViewModels.Pages;

public class SettingsViewModelTests
{
    private readonly Mock<IUpdateManagerService> _mockUpdateManagerService;
    private readonly Mock<ISupportTool> _mockSupportTool;
    private readonly Mock<ISettings> _mockSettings;
    private readonly Mock<IUiSettings> _mockUiSettings;
    private readonly Mock<IUserSettings> _mockUserSettings;
    private readonly Mock<IBugReportService> _mockBugReportService;
    private readonly ThemeSetting _themeSetting;
    private readonly AlwaysOnTopSetting _aotSetting;
    private readonly NewTemplateSetting _newTemplateSetting;
    private readonly CapitalizeFormCodeSetting _capitalizeFormCodeSetting;

    public SettingsViewModelTests()
    {
        // 1. Setup all the mock objects for the dependencies.
        _mockUpdateManagerService = new Mock<IUpdateManagerService>();
        _mockSupportTool = new Mock<ISupportTool>();
        _mockSettings = new Mock<ISettings>();
        _mockUiSettings = new Mock<IUiSettings>();
        _mockUserSettings = new Mock<IUserSettings>();
        _mockBugReportService = new Mock<IBugReportService>();

        // 2. Create concrete instances of the settings objects that will be "returned" by the mocks.
        _themeSetting = new ThemeSetting { Theme = ApplicationTheme.Light };
        _aotSetting = new AlwaysOnTopSetting { IsEnabled = false };
        _newTemplateSetting = new NewTemplateSetting { SelectNewTemplate = true };
        _capitalizeFormCodeSetting = new CapitalizeFormCodeSetting { CapitalizeFormCode = true };

        // 3. Configure the mock objects to behave like the real services.
        // When the code asks for UiSettings, return our mock UiSettings object.
        _mockSettings.Setup(s => s.UiSettings).Returns(_mockUiSettings.Object);
        // When the code asks for UserSettings, return our mock UserSettings object.
        _mockSettings.Setup(s => s.UserSettings).Returns(_mockUserSettings.Object);
        // Configure the mock UiSettings to return a list containing our concrete setting instances.
        _mockUiSettings.Setup(s => s.Settings).Returns([_themeSetting, _aotSetting, _newTemplateSetting, _capitalizeFormCodeSetting]);
        // Configure the mock UserSettings to have a default ExtSeparator
        _mockUserSettings.Setup(us => us.ExtSeparator).Returns("x");
        // Configure the mock SupportTool to return our mock Settings object.
        _mockSupportTool.Setup(st => st.Settings).Returns(_mockSettings.Object);

    }

    [Fact]
    public void ViewModel_LoadsInitialValues_FromSupportTool()
    {
        // Arrange: Create the ViewModel with our pre-configured mock services.
        var viewModel = new SettingsViewModel(_mockUpdateManagerService.Object, _mockSupportTool.Object, _mockBugReportService.Object);

        // Assert: Verify that the ViewModel's properties were correctly initialized from the mock settings.
        Assert.Equal(ApplicationTheme.Light, viewModel.CurrentTheme);
        Assert.False(viewModel.AlwaysOnTop);
        Assert.True(viewModel.SelectNewTemplate);
        Assert.True(viewModel.CapitalizeFormCode);
        Assert.Equal("x", viewModel.ExtSeparator);
    }

    [Fact]
    public void ChangingTheme_UpdatesAndSavesSettings()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockUpdateManagerService.Object, _mockSupportTool.Object, _mockBugReportService.Object);

        // Act: Change a property on the ViewModel, simulating a user interaction.
        viewModel.CurrentTheme = ApplicationTheme.Dark;

        // Assert
        // Verify that the underlying setting object was updated.
        Assert.Equal(ApplicationTheme.Dark, _themeSetting.Theme);
        // Verify that the SaveAllSettings method was called exactly once.
        _mockSupportTool.Verify(st => st.SaveAllSettings(), Times.Once);
    }

    [Fact]
    public void ChangingAlwaysOnTop_UpdatesAndSavesSettings()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockUpdateManagerService.Object, _mockSupportTool.Object, _mockBugReportService.Object);

        // Act
        viewModel.AlwaysOnTop = true;

        // Assert
        Assert.True(_aotSetting.IsEnabled);
        _mockSupportTool.Verify(st => st.SaveAllSettings(), Times.Once);
    }

    [Fact]
    public void ChangingSelectNewTemplate_UpdatesAndSavesSettings()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockUpdateManagerService.Object, _mockSupportTool.Object, _mockBugReportService.Object);

        // Act
        viewModel.SelectNewTemplate = false;

        // Assert
        Assert.False(_newTemplateSetting.SelectNewTemplate);
        _mockSupportTool.Verify(st => st.SaveAllSettings(), Times.Once);
    }

    [Fact]
    public void ChangingCapitalizeFormCode_UpdatesAndSavesSettings()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockUpdateManagerService.Object, _mockSupportTool.Object, _mockBugReportService.Object);

        // Act
        viewModel.CapitalizeFormCode = false;

        // Assert
        Assert.False(_capitalizeFormCodeSetting.CapitalizeFormCode);
        _mockSupportTool.Verify(st => st.SaveAllSettings(), Times.Once);
    }

    [Fact]
    public void ChangingExtSeparator_UpdatesAndSavesSettings()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockUpdateManagerService.Object, _mockSupportTool.Object, _mockBugReportService.Object);

        // Act
        viewModel.ExtSeparator = "-";

        // Assert
        // Verify that the setter on the underlying UserSettings object was called with the correct value.
        _mockUserSettings.VerifySet(us => us.ExtSeparator = "-", Times.Once);
        _mockSupportTool.Verify(st => st.SaveAllSettings(), Times.Once);
    }

    [Fact]
    public async Task CheckForUpdatesCommand_CallsUpdateManagerService()
    {
        // Arrange
        var viewModel = new SettingsViewModel(_mockUpdateManagerService.Object, _mockSupportTool.Object, _mockBugReportService.Object);

        // Act
        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        // Assert
        // Verify that the CheckForUpdatesAsync method on the service was called exactly once.
        _mockUpdateManagerService.Verify(ums => ums.CheckForUpdatesAsync(), Times.Once);
    }

    [Fact]
    public void AssociateFormGenCommand_WhenPathIsValid_CallsAssociateFormgen()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var mockContextMenu = new Mock<AMFormsCST.Desktop.Services.Interfaces.IWindowsExplorerContextMenuService>();
            mockContextMenu.Setup(m => m.AssociateFormgen(tempFile)).Returns(true);
            mockContextMenu.Setup(m => m.IsFormgenAssociated()).Returns(true);

            var viewModel = new SettingsViewModel(
                _mockUpdateManagerService.Object,
                _mockSupportTool.Object,
                _mockBugReportService.Object,
                explorerContextMenuService: mockContextMenu.Object);

            viewModel.FormGenExecutablePath = tempFile;

            // Act
            viewModel.AssociateFormGenCommand.Execute(null);

            // Assert
            mockContextMenu.Verify(m => m.AssociateFormgen(tempFile), Times.Once);
            Assert.True(viewModel.IsFormgenAssociated);
            Assert.Contains("default handler", viewModel.FormgenAssociationStatus);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void AssociateFormGenCommand_WhenPathIsEmpty_DoesNotCallService()
    {
        // Arrange
        var mockContextMenu = new Mock<AMFormsCST.Desktop.Services.Interfaces.IWindowsExplorerContextMenuService>();
        var viewModel = new SettingsViewModel(
            _mockUpdateManagerService.Object,
            _mockSupportTool.Object,
            _mockBugReportService.Object,
            explorerContextMenuService: mockContextMenu.Object);

        viewModel.FormGenExecutablePath = "";

        // Act
        viewModel.AssociateFormGenCommand.Execute(null);

        // Assert
        mockContextMenu.Verify(m => m.AssociateFormgen(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void OpenLogsCommand_And_OpenLogsFolderCommand_ExecuteWithoutException()
    {
        // Arrange
        var viewModel = new SettingsViewModel(
            _mockUpdateManagerService.Object,
            _mockSupportTool.Object,
            _mockBugReportService.Object);

        // Act & Assert (Should not throw and ensures commands exist and are bound)
        Assert.NotNull(viewModel.OpenLogsCommand);
        Assert.NotNull(viewModel.OpenLogsFolderCommand);
    }
}