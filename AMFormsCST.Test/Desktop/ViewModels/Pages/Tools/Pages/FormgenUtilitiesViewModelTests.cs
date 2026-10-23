using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure;
using AMFormsCST.Desktop.Interfaces;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using Moq;
using Assert = Xunit.Assert;

namespace AMFormsCST.Test.Desktop.ViewModels.Pages.Tools.Pages;

public class FormgenUtilitiesViewModelTests
{
    private readonly Mock<ISupportTool> _mockSupportTool;
    private readonly Mock<IDialogService> _mockDialogService;
    private readonly Mock<IFormgenUtils> _mockFormgenUtils;
    private readonly Mock<IFileSystem> _mockFileSystem;

    public FormgenUtilitiesViewModelTests()
    {
        // 1. Mock the view model's direct dependencies.
        _mockSupportTool = new Mock<ISupportTool>();
        _mockDialogService = new Mock<IDialogService>();
        _mockFormgenUtils = new Mock<IFormgenUtils>();
        _mockFileSystem = new Mock<IFileSystem>();

        // 2. Configure the SupportTool mock to provide the FormgenUtils mock.
        _mockSupportTool.Setup(st => st.FormgenUtils).Returns(_mockFormgenUtils.Object);
    }

    [Fact]
    public void OpenFileCommand_WhenFileSelected_LoadsFormAndPopulatesProperties()
    {
        // Arrange
        var viewModel = new FormgenUtilitiesViewModel(_mockSupportTool.Object, _mockDialogService.Object, _mockFileSystem.Object);
        var fakeFilePath = "C:\\temp\\test.formgen";

        var fakeFormgenFile = new DotFormgen
        {
            Title = "Test Form Title",
            Settings = { TotalPages = 3 }
        };

        // Configure all mock services
        _mockDialogService.Setup(ds => ds.ShowOpenFileDialog(It.IsAny<string>())).Returns(fakeFilePath);
        _mockFormgenUtils.Setup(fu => fu.OpenFile(fakeFilePath));
        _mockFormgenUtils.Setup(fu => fu.ParsedFormgenFile).Returns(fakeFormgenFile);
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true); // Assume image file exists
        _mockFileSystem.Setup(fs => fs.GetDirectoryName(It.IsAny<string>())).Returns("C:\\temp");
        _mockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(It.IsAny<string>())).Returns("test");
        _mockFileSystem.Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>())).Returns((string p1, string p2) => p1 + "\\" + p2);


        // Act
        viewModel.OpenFormgenFileCommand.Execute(null);

        // Assert
        _mockDialogService.Verify(ds => ds.ShowOpenFileDialog(It.IsAny<string>()), Times.Once);
        _mockFormgenUtils.Verify(fu => fu.OpenFile(fakeFilePath), Times.Once);
        Assert.NotNull(viewModel.FormTitle);
        Assert.Equal("Test Form Title", viewModel.FormTitle);
        Assert.NotNull(viewModel.TreeViewNodes);
        Assert.NotEmpty(viewModel.TreeViewNodes);
    }

    [Fact]
    public void OpenFileCommand_WhenNoFileSelected_DoesNothing()
    {
        // Arrange
        var viewModel = new FormgenUtilitiesViewModel(_mockSupportTool.Object, _mockDialogService.Object, _mockFileSystem.Object);
        _mockDialogService.Setup(ds => ds.ShowOpenFileDialog(It.IsAny<string>())).Returns((string?)null);

        // Act
        viewModel.OpenFormgenFileCommand.Execute(null);

        // Assert
        _mockDialogService.Verify(ds => ds.ShowOpenFileDialog(It.IsAny<string>()), Times.Once);
        _mockFormgenUtils.Verify(fu => fu.OpenFile(It.IsAny<string>()), Times.Never);
        Assert.True(string.IsNullOrEmpty(viewModel.FilePath));
        Assert.Empty(viewModel.TreeViewNodes);
    }

    [Fact]
    public void ClearFileCommand_ResetsFileProperties()
    {
        // Arrange
        var viewModel = new FormgenUtilitiesViewModel(_mockSupportTool.Object, _mockDialogService.Object, _mockFileSystem.Object);
        
        // Load a file first to give the properties a state to be cleared from.
        var fakeFilePath = "C:\\temp\\test.formgen";
        var fakeFormgenFile = new DotFormgen { Title = "Test Form Title" };
        _mockDialogService.Setup(ds => ds.ShowOpenFileDialog(It.IsAny<string>())).Returns(fakeFilePath);
        _mockFormgenUtils.Setup(fu => fu.OpenFile(fakeFilePath));
        _mockFormgenUtils.Setup(fu => fu.ParsedFormgenFile).Returns(fakeFormgenFile);
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(false); // Doesn't matter for this test
        _mockFileSystem.Setup(fs => fs.GetDirectoryName(It.IsAny<string>())).Returns("C:\\temp");
        _mockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(It.IsAny<string>())).Returns("test");
        _mockFileSystem.Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>())).Returns((string p1, string p2) => p1 + "\\" + p2);

        viewModel.OpenFormgenFileCommand.Execute(null);
        Assert.NotNull(viewModel.FilePath); // Pre-condition check

        // Act
        viewModel.ClearFileCommand.Execute(null);

        // Assert
        Assert.Null(viewModel.FilePath);
        Assert.Empty(viewModel.TreeViewNodes);
        Assert.Null(viewModel.SelectedNode);
    }

    [Fact]
    public void PushToFormGenCommand_WhenNoFileLoaded_DoesNotThrow()
    {
        var viewModel = new FormgenUtilitiesViewModel(_mockSupportTool.Object, _mockDialogService.Object, _mockFileSystem.Object);
        Assert.False(viewModel.IsFileLoaded);

        // Act & Assert
        viewModel.PushToFormGenCommand.Execute(null);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void PushToFormGenCommand_WhenFileLoadedAndChanged_SavesFileBeforePush()
    {
        var viewModel = new FormgenUtilitiesViewModel(_mockSupportTool.Object, _mockDialogService.Object, _mockFileSystem.Object);
        var fakeFilePath = "C:\\temp\\test.formgen";
        var fakeFormgenFile = new DotFormgen
        {
            Title = "Test Form Title",
            Settings = { UUID = "1234-uuid" }
        };

        _mockDialogService.Setup(ds => ds.ShowOpenFileDialog(It.IsAny<string>())).Returns(fakeFilePath);
        _mockFormgenUtils.Setup(fu => fu.OpenFile(fakeFilePath));
        _mockFormgenUtils.Setup(fu => fu.ParsedFormgenFile).Returns(fakeFormgenFile);
        _mockFormgenUtils.Setup(fu => fu.HasChanged).Returns(true);
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetDirectoryName(It.IsAny<string>())).Returns("C:\\temp");
        _mockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(It.IsAny<string>())).Returns("test");
        _mockFileSystem.Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>())).Returns((string p1, string p2) => p1 + "\\" + p2);

        viewModel.OpenFormgenFileCommand.Execute(null);
        Assert.True(viewModel.IsFileLoaded);

        // Act
        viewModel.PushToFormGenCommand.Execute(null);

        // Assert that SaveFile was called because HasChanged was true
        _mockFormgenUtils.Verify(fu => fu.SaveFile(It.IsAny<string>(), It.IsAny<DotFormgen?>()), Times.Once);
    }
}