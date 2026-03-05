using AMFormsCST.Core.Interfaces.BestPractices;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Core.Utils;
using Moq;
using System.Windows.Documents;
using System.Windows.Markup;
using Assert = Xunit.Assert;

namespace AMFormsCST.Test.Core.Utils;

public class BestPracticeEnforcerTests
{
    private readonly Mock<IFormNameBestPractice> _mockFormNamePractice;
    private readonly Mock<ITemplateRepository> _mockTemplateRepository;
    private readonly List<TextTemplate> _templateList;
    private readonly BestPracticeEnforcer _enforcer;

    public BestPracticeEnforcerTests()
    {
        _mockFormNamePractice = new Mock<IFormNameBestPractice>();
        _mockTemplateRepository = new Mock<ITemplateRepository>();
        var flowDoc = new FlowDocument(new Paragraph(new Run("Text")));
        _templateList = [new TextTemplate("Existing", "Desc", flowDoc, TextTemplate.TemplateType.Other)];

        // Configure the mock repository to return our in-memory list
        _mockTemplateRepository.Setup(repo => repo.LoadTemplates()).Returns(_templateList);

        // Create the instance of the class we are testing
        _enforcer = new BestPracticeEnforcer(_mockFormNamePractice.Object, _mockTemplateRepository.Object);
    }

    [Fact]
    public void GetFormName_CallsGenerate_OnFormNameBestPractice()
    {
        // Arrange
        _mockFormNamePractice.Setup(p => p.Generate()).Returns("GeneratedName");

        // Act
        var result = _enforcer.GetFormName();

        // Assert
        Assert.Equal("GeneratedName", result);
        _mockFormNamePractice.Verify(p => p.Generate(), Times.Once);
    }

    [Fact]
    public void AddTemplate_AddsToCollection_AndSaves()
    {
        // Arrange
        var newTemplate = new TextTemplate("New", "New Desc", new FlowDocument(new Paragraph(new Run("New Text"))), TextTemplate.TemplateType.Other);

        // Act
        _enforcer.AddTemplate(newTemplate);

        // Assert
        Assert.Equal(2, _enforcer.Templates.Count);
        Assert.Contains(newTemplate, _enforcer.Templates);
        _mockTemplateRepository.Verify(repo => repo.SaveTemplates(_enforcer.Templates), Times.Once);
    }

    [Fact]
    public void AddTemplate_WithExistingTemplate_ThrowsArgumentException()
    {
        // Arrange
        var existingTemplate = _templateList[0];

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _enforcer.AddTemplate(existingTemplate));
        _mockTemplateRepository.Verify(repo => repo.SaveTemplates(It.IsAny<List<TextTemplate>>()), Times.Never);
    }

    [Fact]
    public void AddTemplate_WithNullTemplate_ThrowsArgumentNullException()
    {
        // Arrange
        TextTemplate? nullTemplate = null;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _enforcer.AddTemplate(nullTemplate!));
    }

    [Fact]
    public void AddTemplate_WithEmptyText_ThrowsArgumentException()
    {
        // Arrange
        var templateWithEmptyText = new TextTemplate("New", "Desc", new FlowDocument(), TextTemplate.TemplateType.Other);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _enforcer.AddTemplate(templateWithEmptyText));
    }

    [Fact]
    public void RemoveTemplate_RemovesFromCollection_AndSaves()
    {
        // Arrange
        var templateToRemove = _templateList[0];

        // Act
        _enforcer.RemoveTemplate(templateToRemove);

        // Assert
        Assert.Empty(_enforcer.Templates);
        _mockTemplateRepository.Verify(repo => repo.SaveTemplates(_enforcer.Templates), Times.Once);
    }

    [Fact]
    public void RemoveTemplate_WithNonExistingTemplate_ThrowsArgumentException()
    {
        // Arrange
        var nonExistingTemplate = new TextTemplate("Non-existent", "Desc", XamlWriter.Save(new FlowDocument(new Paragraph(new Run("Text")))), TextTemplate.TemplateType.Other);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _enforcer.RemoveTemplate(nonExistingTemplate));
    }

    [Fact]
    public void UpdateTemplate_UpdatesProperties_AndSaves()
    {
        // Arrange
        var originalTemplate = _templateList[0];
        var updatedTemplate = new TextTemplate(originalTemplate.Id, "Updated Name", "Updated Desc", XamlWriter.Save(new FlowDocument(new Paragraph(new Run("Updated Text")))), TextTemplate.TemplateType.Other);

        // Act
        _enforcer.UpdateTemplate(updatedTemplate);

        // Assert
        Assert.Single(_enforcer.Templates);
        var templateInList = _enforcer.Templates[0];
        Assert.Equal("Updated Name", templateInList.Name);
        Assert.Equal("Updated Desc", templateInList.Description);
        _mockTemplateRepository.Verify(repo => repo.SaveTemplates(_enforcer.Templates), Times.Once);
    }

    [Fact]
    public void UpdateTemplate_WithNonExistingTemplate_DoesNotThrow_AndDoesNotSave()
    {
        // Arrange
        // Create a template with a new, unknown Guid
        var nonExistingTemplate = new TextTemplate(Guid.NewGuid(), "Non-existent", "Desc", XamlWriter.Save(new FlowDocument(new Paragraph(new Run("Text")))), TextTemplate.TemplateType.Other);

        // Act
        // This should not throw an exception.
        _enforcer.UpdateTemplate(nonExistingTemplate);

        // Assert
        // Verify that the save method was never called because no update occurred.
        _mockTemplateRepository.Verify(repo => repo.SaveTemplates(It.IsAny<List<TextTemplate>>()), Times.Never);
        // Verify the original template is unchanged.
        Assert.Equal("Existing", _templateList[0].Name);
    }


    [Fact]
    public void TestNonExistentTemplate()
    {
        var nonExistingTemplate = new TextTemplate(Guid.NewGuid(), "Non-existent", "Desc", XamlWriter.Save(new FlowDocument(new Paragraph(new Run("Text")))), TextTemplate.TemplateType.Other);

        // Act
        _enforcer.UpdateTemplate(nonExistingTemplate);

        // Assert
        _mockTemplateRepository.Verify(repo => repo.SaveTemplates(It.IsAny<List<TextTemplate>>()), Times.Never);
    }
}