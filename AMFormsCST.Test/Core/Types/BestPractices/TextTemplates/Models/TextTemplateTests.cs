using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.BestPractices;
using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Windows.Documents;
using System.Windows.Markup;
using Assert = Xunit.Assert;

namespace AMFormsCST.Test.Core.Types.BestPractices.TextTemplates.Models;

public class TextTemplateTests
{
    private readonly Mock<ISupportTool> _mockSupportTool;
    private readonly List<ITextTemplateVariable> _variables;

    public TextTemplateTests()
    {
        var mockVar1 = new Mock<ITextTemplateVariable>();
        mockVar1.Setup(v => v.ProperName).Returns("{CaseNumber}");
        mockVar1.Setup(v => v.Prefix).Returns("$");
        mockVar1.Setup(v => v.Name).Returns("Case");
        mockVar1.Setup(v => v.GetValue()).Returns("12345");
        mockVar1.Setup(v => v.Aliases).Returns(new List<string> { "CaseNum" });

        var mockVar2 = new Mock<ITextTemplateVariable>();
        mockVar2.Setup(v => v.ProperName).Returns("{UserName}");
        mockVar2.Setup(v => v.Prefix).Returns("$");
        mockVar2.Setup(v => v.Name).Returns("User");
        mockVar2.Setup(v => v.GetValue()).Returns("JohnDoe");
        mockVar2.Setup(v => v.Aliases).Returns(new List<string>());

        _variables = [mockVar1.Object, mockVar2.Object];

        var mockOrgVariables = new Mock<IOrgVariables>();
        mockOrgVariables.Setup(ov => ov.Variables).Returns(_variables);

        var mockUserSettings = new Mock<IUserSettings>();
        mockUserSettings.Setup(us => us.Organization).Returns(mockOrgVariables.Object);

        var mockSettings = new Mock<ISettings>();
        mockSettings.Setup(s => s.UserSettings).Returns(mockUserSettings.Object);

        _mockSupportTool = new Mock<ISupportTool>();
        _mockSupportTool.Setup(st => st.Settings).Returns(mockSettings.Object);
    }

    [Fact]
    public void GetVariables_FindsVariablesInText_AndReturnsThem()
    {
        // Arrange
        var flowDoc = new FlowDocument(new Paragraph(new Run("Case number is {CaseNumber} and user is $User.")));
        var template = new TextTemplate("Test", "Desc", flowDoc, TextTemplate.TemplateType.Other);
    
        // Act
        var foundVariables = template.GetVariables(_mockSupportTool.Object);

        // Assert
        Assert.Equal(2, foundVariables.Count);
        Assert.Contains(_variables[0], foundVariables); // {CaseNumber}
        Assert.Contains(_variables[1], foundVariables); // $User
    }

    [Fact]
    public void GetVariables_WhenNoVariablesInText_ReturnsEmptyList()
    {
        // Arrange
        var flowDoc = new FlowDocument(new Paragraph(new Run("This text has no variables.")));
        var template = new TextTemplate("Test", "Desc", flowDoc, TextTemplate.TemplateType.Other);

        // Act
        var foundVariables = template.GetVariables(_mockSupportTool.Object);

        // Assert
        Assert.Empty(foundVariables);
    }

    [Fact]
    public void Process_ReplacesPlaceholdersWithVariableValues()
    {
        // Arrange
        var processedText = "Case: {0}, User: {1}";
        var orderedVariables = new List<ITextTemplateVariable> { _variables[0], _variables[1] };
        var overrides = new List<string> { "", "" }; // No overrides

        // Act
        var result = TextTemplate.Process(processedText, orderedVariables, overrides);

        // Assert
        Assert.Equal("Case: 12345, User: JohnDoe", result);
    }

    [Fact]
    public void Process_WithOverrides_UsesOverrideValues()
    {
        // Arrange
        var processedText = "Case: {0}, User: {1}";
        var orderedVariables = new List<ITextTemplateVariable> { _variables[0], _variables[1] };
        var overrides = new List<string> { "OVERRIDE-CASE", "OVERRIDE-USER" };

        // Act
        var result = TextTemplate.Process(processedText, orderedVariables, overrides);

        // Assert
        Assert.Equal("Case: OVERRIDE-CASE, User: OVERRIDE-USER", result);
    }

    [Fact]
    public void ContainsVariable_FindsVariableByAlias_ReturnsTrue()
    {
        // Arrange
        var textWithAlias = "The case is $CaseNum.";

        // Act
        var result = TextTemplate.ContainsVariable(textWithAlias, _mockSupportTool.Object);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetFirstVariable_FindsCorrectFirstVariableInText()
    {
        // Arrange
        var text = "The user is {UserName} and the case is $Case.";

        // Act
        var (position, variable, alias) = TextTemplate.GetFirstVariable(text, _mockSupportTool.Object);

        // Assert
        Assert.Equal(12, position);
        Assert.Same(_variables[1], variable); // {UserName} is the first variable
        Assert.Equal("{UserName}", alias);
    }

    [Fact]
    public void GetFirstVariable_WhenNoMatch_ReturnsNullTuple()
    {
        // Arrange
        var text = "No variables here.";

        // Act
        var (position, variable, alias) = TextTemplate.GetFirstVariable(text, _mockSupportTool.Object);

        // Assert
        Assert.Equal(-1, position);
        Assert.Null(variable);
        Assert.Equal(string.Empty, alias);
    }

    [Theory]
    [InlineData(TextTemplate.TemplateType.PublishComments)]
    [InlineData(TextTemplate.TemplateType.InternalComments)]
    [InlineData(TextTemplate.TemplateType.ClosureComments)]
    [InlineData(TextTemplate.TemplateType.Email)]
    [InlineData(TextTemplate.TemplateType.Other)]
    public void Constructor_SetsTemplateTypeCorrectly(TextTemplate.TemplateType type)
    {
        // Arrange & Act
        var flowDoc = new FlowDocument(new Paragraph(new Run("Text")));
        var template = new TextTemplate("Name", "Desc", flowDoc, type);

        // Assert
        Assert.Equal(type, template.Type);
    }

    [Fact]
    public void JsonConstructor_SetsAllPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var flowDoc = new FlowDocument(new Paragraph(new Run("Text")));

        // Act
        var template = new TextTemplate(id, "Name", "Desc", XamlWriter.Save(flowDoc), TextTemplate.TemplateType.Email);

        // Assert
        Assert.Equal(id, template.Id);
        Assert.Equal("Name", template.Name);
        Assert.Equal("Desc", template.Description);
        Assert.Equal("Text\r\n", TextTemplate.GetFlowDocumentPlainText(template.Text));
        Assert.Equal(TextTemplate.TemplateType.Email, template.Type);
    }
}