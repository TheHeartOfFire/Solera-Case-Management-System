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

        // Assert that SaveFile was called
        _mockFormgenUtils.Verify(fu => fu.SaveFile(It.IsAny<string>(), It.IsAny<DotFormgen?>()), Times.Once);
    }

    [Fact]
    public void PushToFormGenCommand_WhenFileLoadedAndHasChangedFalse_StillSavesAndBacksUpBeforePush()
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
        _mockFormgenUtils.Setup(fu => fu.HasChanged).Returns(false);
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetDirectoryName(It.IsAny<string>())).Returns("C:\\temp");
        _mockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(It.IsAny<string>())).Returns("test");
        _mockFileSystem.Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>())).Returns((string p1, string p2) => p1 + "\\" + p2);

        viewModel.OpenFormgenFileCommand.Execute(null);
        Assert.True(viewModel.IsFileLoaded);

        // Act
        viewModel.PushToFormGenCommand.Execute(null);

        // Assert that SaveFile was unconditionally called even when HasChanged was false
        _mockFormgenUtils.Verify(fu => fu.SaveFile(fakeFilePath, It.IsAny<DotFormgen?>()), Times.Once);
    }

    [Fact]
    public void EditingPromptVariableViaDisplayProperty_RaisesHasChangedAndRefreshesTreeNodeHeader()
    {
        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<formDef version=""4"" publishedUUID=""1234-uuid"" legacyImport=""false"" totalPages=""1"" defaultPoints=""10"" missingSourceJpeg=""false"" duplex=""false"" maxAccessoryLines=""0"" prePrintedLaserForm=""false"">
  <title>Test Form Title</title>
  <formPrintType>Pdf</formPrintType>
  <codeLines order=""0"" type=""PROMPT"" destVariable=""F0"">
    <promptData type=""Text"" promptIsExpression=""false"" required=""false"" leftSize=""10"" rightSize=""0"" choicesDelimiter=""0"" allowNegatives=""false"" forceUpperCase=""false"" makeBuyerVars=""false"" includeNoneAsOption=""false"">
      <promptMessage>Original Prompt</promptMessage>
    </promptData>
  </codeLines>
</formDef>";
        var fakeFilePath = "C:\\temp\\test.formgen";
        _mockFileSystem.Setup(fs => fs.ReadAllText(fakeFilePath)).Returns(xml);
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetDirectoryName(It.IsAny<string>())).Returns("C:\\temp");
        _mockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(It.IsAny<string>())).Returns("test");
        _mockFileSystem.Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>())).Returns((string p1, string p2) => p1 + "\\" + p2);

        var realFormgenUtils = new AMFormsCST.Core.Utils.FormgenUtils(
            _mockFileSystem.Object,
            new AMFormsCST.Core.Utils.FormgenUtilsProperties());
        _mockSupportTool.Setup(st => st.FormgenUtils).Returns(realFormgenUtils);

        var viewModel = new FormgenUtilitiesViewModel(_mockSupportTool.Object, _mockDialogService.Object, _mockFileSystem.Object);
        viewModel.LoadFormgenFile(fakeFilePath);

        Assert.False(viewModel.HasChanged);

        // Locate the Prompt CodeLine node in TreeViewNodes
        var rootNode = viewModel.TreeViewNodes[0];
        var codeLinesGroup = rootNode.Children.First(c => c.Header == "Code Lines");
        var promptsGroup = codeLinesGroup.Children.First(c => c.Header == "PROMPT");
        var promptNode = promptsGroup.Children.First();

        Assert.Equal("F0", promptNode.Header);

        // Select the prompt node so SelectedNodeProperties is populated
        viewModel.SelectedNode = promptNode;
        Assert.NotNull(viewModel.SelectedNodeProperties);

        bool hasChangedNotified = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(FormgenUtilitiesViewModel.HasChanged))
                hasChangedNotified = true;
        };

        // Edit the prompt's Variable (name) via DisplayProperty
        var variableProp = viewModel.SelectedNodeProperties!.First(p => p.Name == "Variable");
        variableProp.Value = "F99";

        Assert.True(hasChangedNotified);
        Assert.True(viewModel.HasChanged);
        Assert.Equal("F99", promptNode.Header);
    }

    [Fact]
    public async Task SaveFormgenFileCommand_SavesWithoutModalSuccessMessageBox()
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
        _mockFileSystem.Setup(fs => fs.FileExists(It.IsAny<string>())).Returns(true);
        _mockFileSystem.Setup(fs => fs.GetDirectoryName(It.IsAny<string>())).Returns("C:\\temp");
        _mockFileSystem.Setup(fs => fs.GetFileNameWithoutExtension(It.IsAny<string>())).Returns("test");
        _mockFileSystem.Setup(fs => fs.CombinePath(It.IsAny<string>(), It.IsAny<string>())).Returns((string p1, string p2) => p1 + "\\" + p2);

        viewModel.OpenFormgenFileCommand.Execute(null);
        Assert.Equal("Save", viewModel.SaveButtonText);

        await viewModel.SaveFormgenFileCommand.ExecuteAsync(null);

        _mockFormgenUtils.Verify(fu => fu.SaveFile(fakeFilePath, It.IsAny<DotFormgen?>()), Times.Once);
        _mockDialogService.Verify(
            ds => ds.ShowMessageBox(It.IsAny<string>(), "Success", It.IsAny<System.Windows.MessageBoxButton>(), It.IsAny<System.Windows.MessageBoxImage>()),
            Times.Never);
        Assert.Equal("Save", viewModel.SaveButtonText);
        Assert.False(viewModel.IsSaving);
    }

    [Fact]
    public void FieldItemViewModel_EditsLaserRectAndCoordinates_ResolvesIssue72()
    {
        var coreField = new FormField
        {
            Expression = "'TestField'",
            Settings =
            {
                ID = 42,
                LaserRect = new System.Drawing.Rectangle(10, 20, 100, 30),
                ImpactPosition = new System.Drawing.Point(5, 12)
            }
        };
        var page = new FormPage();
        page.Fields.Add(coreField);

        bool changedNotified = false;
        var vm = new AMFormsCST.Desktop.ViewModels.Pages.Tools.FormgenUtils.FieldItemViewModel(coreField, page, () => changedNotified = true);

        Assert.Equal(10, vm.LaserRectX);
        Assert.Equal(20, vm.LaserRectY);
        Assert.Equal(100, vm.LaserRectWidth);
        Assert.Equal(30, vm.LaserRectHeight);
        Assert.Equal(5, vm.ImpactCol);
        Assert.Equal(12, vm.ImpactLine);

        // Edit LaserRect properties directly
        vm.LaserRectX = 55;
        vm.LaserRectY = 75;
        vm.LaserRectWidth = 200;
        vm.LaserRectHeight = 45;
        vm.ImpactCol = 8;
        vm.ImpactLine = 15;

        // Verify underlying core model was updated (Issue #72 resolution)
        Assert.True(changedNotified);
        Assert.Equal(55, coreField.Settings.LaserRect.X);
        Assert.Equal(75, coreField.Settings.LaserRect.Y);
        Assert.Equal(200, coreField.Settings.LaserRect.Width);
        Assert.Equal(45, coreField.Settings.LaserRect.Height);
        Assert.Equal(8, coreField.Settings.ImpactPosition.X);
        Assert.Equal(15, coreField.Settings.ImpactPosition.Y);
    }

    [Fact]
    public void ScanAllCode_DetectsMalformedCodeAndSuggestedFix()
    {
        var viewModel = new FormgenUtilitiesViewModel(_mockSupportTool.Object, _mockDialogService.Object, _mockFileSystem.Object);
        var fakeFormgen = new DotFormgen();
        var page = new FormPage();
        var badField = new FormField
        {
            Expression = "IF(F10 = \"NEW\", 'N', 'U')", // double quotes trigger malformed syntax warning with single-quote suggested fix
            Settings = { ID = 1 }
        };
        page.Fields.Add(badField);
        fakeFormgen.Pages.Add(page);

        _mockFormgenUtils.Setup(fu => fu.ParsedFormgenFile).Returns(fakeFormgen);

        viewModel.ScanAllCodeCommand.Execute(null);

        Assert.NotEmpty(viewModel.CodeValidationIssues);
        var issue = viewModel.CodeValidationIssues[0];
        Assert.True(issue.HasSuggestedFix);
        Assert.Contains("single quotes", issue.DiagnosticMessage);

        // Apply fix
        viewModel.ApplyCodeFixCommand.Execute(issue);
        Assert.Equal("IF( F10 = 'NEW', 'N', 'U' )", badField.Expression);
        Assert.True(issue.IsValid);
    }

    [Fact]
    public void PromptSelection_MasterCheckboxAndMultiSelect_WorksCorrectly()
    {
        var viewModel = new FormgenUtilitiesViewModel(_mockSupportTool.Object, _mockDialogService.Object, _mockFileSystem.Object);
        var fakeFormgen = new DotFormgen();
        var prompt1 = new CodeLine { Settings = { Order = 1, Variable = "P1", Type = CodeLineSettings.CodeType.PROMPT } };
        var prompt2 = new CodeLine { Settings = { Order = 2, Variable = "P2", Type = CodeLineSettings.CodeType.PROMPT } };
        var prompt3 = new CodeLine { Settings = { Order = 3, Variable = "P3", Type = CodeLineSettings.CodeType.PROMPT } };
        fakeFormgen.CodeLines.Add(prompt1);
        fakeFormgen.CodeLines.Add(prompt2);
        fakeFormgen.CodeLines.Add(prompt3);

        _mockFormgenUtils.Setup(fu => fu.ParsedFormgenFile).Returns(fakeFormgen);

        // Populate lists
        viewModel.PromptsList.Add(new AMFormsCST.Desktop.ViewModels.Pages.Tools.FormgenUtils.PromptItemViewModel(prompt1, null, viewModel.UpdateAreAllPromptsSelected));
        viewModel.PromptsList.Add(new AMFormsCST.Desktop.ViewModels.Pages.Tools.FormgenUtils.PromptItemViewModel(prompt2, null, viewModel.UpdateAreAllPromptsSelected));
        viewModel.PromptsList.Add(new AMFormsCST.Desktop.ViewModels.Pages.Tools.FormgenUtils.PromptItemViewModel(prompt3, null, viewModel.UpdateAreAllPromptsSelected));
        viewModel.UpdateAreAllPromptsSelected();

        Assert.False(viewModel.AreAllPromptsSelected);
        Assert.Equal("3 Prompts & Code Lines", viewModel.PromptsSelectedCountText);

        // Select one prompt
        viewModel.PromptsList[0].IsSelected = true;
        Assert.Null(viewModel.AreAllPromptsSelected); // Indeterminate / partially selected
        Assert.Equal("1 of 3 Selected", viewModel.PromptsSelectedCountText);

        // Select all via master checkbox
        viewModel.AreAllPromptsSelected = true;
        Assert.All(viewModel.PromptsList, p => Assert.True(p.IsSelected));
        Assert.Equal("3 of 3 Selected", viewModel.PromptsSelectedCountText);

        // Deselect all via master checkbox
        viewModel.AreAllPromptsSelected = false;
        Assert.All(viewModel.PromptsList, p => Assert.False(p.IsSelected));
        Assert.Equal("3 Prompts & Code Lines", viewModel.PromptsSelectedCountText);

        // Multi-select prompts 1 and 2 and delete them
        viewModel.PromptsList[0].IsSelected = true;
        viewModel.PromptsList[1].IsSelected = true;

        viewModel.DeleteSelectedPromptsCommand.Execute(null);

        _mockFormgenUtils.Verify(fu => fu.DeletePrompts(It.Is<IEnumerable<CodeLine>>(lines => lines.Count() == 2)), Times.Once);
    }
}