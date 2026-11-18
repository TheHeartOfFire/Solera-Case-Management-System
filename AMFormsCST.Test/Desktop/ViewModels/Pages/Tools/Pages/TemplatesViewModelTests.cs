using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.BestPractices;
using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using AMFormsCST.Desktop.ViewModels.Pages.Tools.Templates;
using Moq;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using Assert = Xunit.Assert;

namespace AMFormsCST.Test.Desktop.ViewModels.Pages.Tools.Pages;

public class TemplatesViewModelTests
{
    private readonly Mock<ISupportTool> _mockSupportTool;
    private readonly Mock<IBestPracticeEnforcer> _mockEnforcer;
    private readonly Mock<ISettings> _mockSettings;
    private readonly Mock<IUserSettings> _mockUserSettings;
    private readonly Mock<IOrgVariables> _mockOrgVariables;
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly List<TextTemplate> _templateList;

    public TemplatesViewModelTests()
    {
        // 1. Mock all required dependencies
        _mockSupportTool = new Mock<ISupportTool>();
        _mockEnforcer = new Mock<IBestPracticeEnforcer>();
        _mockSettings = new Mock<ISettings>();
        _mockUserSettings = new Mock<IUserSettings>();
        _mockOrgVariables = new Mock<IOrgVariables>();
        _mockFileSystem = new Mock<IFileSystem>();

        // 2. Create a list of templates to be returned by the mock enforcer
        _templateList =
        [
            new TextTemplate("Template 1", "Desc 1", new FlowDocument(new Paragraph(new Run("Text 1"))), TextTemplate.TemplateType.Other),
            new TextTemplate("Template 2", "Desc 2", new FlowDocument(new Paragraph(new Run("Text 2"))), TextTemplate.TemplateType.Email)
        ];

        // 3. Configure the full dependency chain required by the view models
        _mockOrgVariables.Setup(ov => ov.Variables).Returns([]); // Return an empty list for the variables
        _mockUserSettings.Setup(us => us.Organization).Returns(_mockOrgVariables.Object);
        _mockSettings.Setup(s => s.UserSettings).Returns(_mockUserSettings.Object);
        _mockSupportTool.Setup(st => st.Settings).Returns(_mockSettings.Object);
        _mockEnforcer.Setup(e => e.Templates).Returns(_templateList);
        _mockSupportTool.Setup(st => st.Enforcer).Returns(_mockEnforcer.Object);
    }

    [Fact]
    public void Constructor_LoadsTemplatesFromEnforcer()
    {
        // Arrange & Act
        var viewModel = new TemplatesViewModel(_mockSupportTool.Object, _mockFileSystem.Object);

        // Assert
        Assert.Equal(2, viewModel.Templates.Count);
        Assert.NotNull(viewModel.SelectedTemplate);
        Assert.True(viewModel.SelectedTemplate == viewModel.Templates[0]);
        Assert.Equal(TextTemplate.TemplateType.Other, viewModel.Templates[0].Template.Type);
        Assert.Equal(TextTemplate.TemplateType.Email, viewModel.Templates[1].Template.Type);
    }

    [Fact]
    public void RemoveTemplateCommand_RemovesTemplateFromEnforcer_AndRefreshesList()
    {
        // Arrange
        var viewModel = new TemplatesViewModel(_mockSupportTool.Object, _mockFileSystem.Object);
        var templateToRemove = viewModel.Templates.First();
        var modelToRemove = templateToRemove.Template;

        // Act
        viewModel.RemoveTemplateCommand.Execute(templateToRemove);

        // Assert
        _mockEnforcer.Verify(e => e.RemoveTemplate(modelToRemove), Times.Once);
        _mockEnforcer.Verify(e => e.Templates, Times.Exactly(2)); // Once in constructor, once after remove
    }

    [Fact]
    public void SelectTemplateCommand_ChangesSelectedTemplate()
    {
        // Arrange
        var viewModel = new TemplatesViewModel(_mockSupportTool.Object, _mockFileSystem.Object);
        var templateToSelect = viewModel.Templates[1];

        // Act
        viewModel.SelectTemplateCommand.Execute(templateToSelect);

        // Assert
        Assert.Same(templateToSelect, viewModel.SelectedTemplate);
        Assert.Equal(TextTemplate.TemplateType.Email, viewModel.SelectedTemplate.Template.Type);
    }

    [Fact]
    public void TagFiltering_FiltersTemplatesViewByAssignedTag()
    {
        // Arrange
        _templateList[0].Tags = ["#Internal"];
        _templateList[1].Tags = ["#Email"];
        var viewModel = new TemplatesViewModel(_mockSupportTool.Object, _mockFileSystem.Object);

        // Act - Filter by #Email
        viewModel.SelectedTagFilter = "#Email";

        // Assert
        var filtered = viewModel.TemplatesView.Cast<TemplateItemViewModel>().ToList();
        Assert.Single(filtered);
        Assert.Equal("Template 2", filtered[0].Template.Name);

        // Act - Reset to All
        viewModel.SelectedTagFilter = "All";
        filtered = viewModel.TemplatesView.Cast<TemplateItemViewModel>().ToList();
        Assert.Equal(2, filtered.Count);
    }

    [Fact]
    public void InsertVariableTokenCommand_AppendsTokenToSelectedTemplate()
    {
        // Arrange
        var viewModel = new TemplatesViewModel(_mockSupportTool.Object, _mockFileSystem.Object);
        var initialText = TextTemplate.GetFlowDocumentPlainText(viewModel.SelectedTemplate!.Template.Text);

        // Act
        viewModel.InsertVariableTokenCommand.Execute("{FormName}");

        // Assert
        var updatedText = TextTemplate.GetFlowDocumentPlainText(viewModel.SelectedTemplate.Template.Text);
        Assert.Contains("{FormName}", updatedText);
    }

    [Fact]
    public void AddTagAndRemoveTagCommand_UpdatesTemplateTags()
    {
        // Arrange
        var viewModel = new TemplatesViewModel(_mockSupportTool.Object, _mockFileSystem.Object);
        var template = viewModel.SelectedTemplate!;

        // Act - Add tag
        viewModel.NewTagInput = "Publish";
        viewModel.AddTagCommand.Execute(null);

        // Assert
        Assert.Contains("#Publish", template.Tags);
        Assert.Empty(viewModel.NewTagInput);

        // Act - Remove tag
        viewModel.RemoveTagCommand.Execute("#Publish");

        // Assert
        Assert.DoesNotContain("#Publish", template.Tags);
    }

    [Fact]
    public void LivePreview_RendersActionBlock_ForActionPartial()
    {
        // Arrange
        var actionPartial = new TextTemplate(
            "Action Partial",
            "Desc",
            "<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Paragraph><Run Text=\"Action on {FormName}\"/></Paragraph></FlowDocument>",
            TextTemplate.TemplateType.Other,
            TextTemplate.TemplateCategoryKind.ActionPartial,
            ["#Internal"],
            AMFormsCST.Core.Types.Notebook.CaseActionType.LineAdjustments
        );
        _templateList.Add(actionPartial);

        var viewModel = new TemplatesViewModel(_mockSupportTool.Object, _mockFileSystem.Object);
        var item = viewModel.Templates.First(t => t.Template.Name == "Action Partial");

        // Act
        viewModel.SelectTemplateCommand.Execute(item);

        // Assert
        Assert.NotNull(viewModel.LivePreviewText);
        Assert.Contains("Action on", viewModel.LivePreviewText);
    }
}