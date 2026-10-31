using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Utils;
using Moq;
using System.IO;
using System.Text;
using System.Xml;

namespace AMFormsCST.Test.Core.Utils;

public class FormgenUtilsTests
{
    private readonly Mock<IFileSystem> _mockFileSystem;
    private readonly FormgenUtilsProperties _formgenUtilsProperties;

    public FormgenUtilsTests()
    {
        _mockFileSystem = new Mock<IFileSystem>();
        _formgenUtilsProperties = new FormgenUtilsProperties();
    }

    [Fact]
    public void OpenFile_WithValidXml_ParsesFileAndSetsProperty()
    {
        // Arrange
        var fakeFilePath = "C:\\forms\\test.formgen";
        var fakeXmlContent = @"<?xml version=""1.0"" encoding=""utf-8""?>
<formDef version=""1"" publishedUUID=""some-guid"" legacyImport=""false"" totalPages=""1"" defaultPoints=""10"" missingSourceJpeg=""false"" duplex=""false"" maxAccessoryLines=""0"" prePrintedLaserForm=""false"">
  <title>My Test Form</title>
</formDef>";

        // Configure the mock file system to return our fake XML content when ReadAllText is called.
        _mockFileSystem.Setup(fs => fs.ReadAllText(fakeFilePath)).Returns(fakeXmlContent);
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);

        // Act
        formgenUtils.OpenFile(fakeFilePath);

        // Assert
        // Verify that the file system was asked to read the file.
        _mockFileSystem.Verify(fs => fs.ReadAllText(fakeFilePath), Times.Once);

        // Verify that the ParsedFormgenFile property is not null and was populated correctly.
        Assert.NotNull(formgenUtils.ParsedFormgenFile);
        Assert.Equal("My Test Form", formgenUtils.ParsedFormgenFile.Title);
        Assert.Equal("some-guid", formgenUtils.ParsedFormgenFile.Settings.UUID);
    }

    [Fact]
    public void OpenFile_WithInvalidXml_ThrowsXmlException()
    {
        // Arrange
        var fakeFilePath = "C:\\forms\\invalid.formgen";
        var invalidXmlContent = "<formDef><title>Missing closing tag</formDef>"; // Malformed XML

        _mockFileSystem.Setup(fs => fs.ReadAllText(fakeFilePath)).Returns(invalidXmlContent);
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);

        // Act & Assert
        // Verify that attempting to open a malformed XML file throws an XmlException.
        Assert.Throws<XmlException>(() => formgenUtils.OpenFile(fakeFilePath));
    }

    [Fact]
    public void SaveFile_WritesCorrectXmlToFileSystem()
    {
        // Arrange
        var fakeFilePath = "C:\\forms\\output.formgen";
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);

        // Create a DotFormgen object in memory to be "saved".
        var fileToSave = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.DotFormgen
        {
            Title = "Saved Form",
            Settings = { UUID = "new-guid", Version = 2 }
        };
        formgenUtils.ParsedFormgenFile = fileToSave;

        // Use a string builder to capture the text that the mock file system "writes".
        var savedContent = new StringBuilder();
        _mockFileSystem.Setup(fs => fs.WriteAllText(fakeFilePath, It.IsAny<string>()))
                       .Callback<string, string>((path, content) => savedContent.Append(content));

        // Act
        formgenUtils.SaveFile(fakeFilePath);

        // Assert
        // Verify that the file system's WriteAllText method was called.
        _mockFileSystem.Verify(fs => fs.WriteAllText(fakeFilePath, It.IsAny<string>()), Times.Once);

        // Verify that the generated XML contains the correct data.
        var resultXml = savedContent.ToString();
        Assert.Contains(@"<title>Saved Form</title>", resultXml);
        Assert.Contains(@"publishedUUID=""new-guid""", resultXml);
    }

    [Fact]
    public void HasChanged_AfterSave_DetectsSubsequentEdits()
    {
        var fakeFilePath = "C:\\forms\\test.formgen";
        var xml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<formDef version=""1"" publishedUUID=""some-guid"" legacyImport=""false"" totalPages=""1"" defaultPoints=""10"" missingSourceJpeg=""false"" duplex=""false"" maxAccessoryLines=""0"" prePrintedLaserForm=""false"">
  <title>My Test Form</title>
  <formPrintType>Pdf</formPrintType>
</formDef>";
        _mockFileSystem.Setup(fs => fs.ReadAllText(fakeFilePath)).Returns(xml);
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);
        formgenUtils.OpenFile(fakeFilePath);
        Assert.False(formgenUtils.HasChanged);

        formgenUtils.ParsedFormgenFile!.Title = "Edit 1";
        Assert.True(formgenUtils.HasChanged);

        formgenUtils.SaveFile(fakeFilePath);
        Assert.False(formgenUtils.HasChanged);

        // Regression: the baseline used to alias the live object, so every later edit looked unchanged.
        formgenUtils.ParsedFormgenFile.Title = "Edit 2";
        Assert.True(formgenUtils.HasChanged);

        formgenUtils.SaveFile(fakeFilePath);
        Assert.False(formgenUtils.HasChanged);
    }

    [Fact]
    public void CloseFile_ResetsParsedFormgenFile_ToNull()
    {
        // Arrange
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);
        formgenUtils.ParsedFormgenFile = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.DotFormgen();
        Assert.NotNull(formgenUtils.ParsedFormgenFile); // Pre-condition check

        // Act
        formgenUtils.CloseFile();

        // Assert
        Assert.Null(formgenUtils.ParsedFormgenFile);
    }

    [Fact]
    public void ClonePrompt_ClonesPromptWithIncrementedOrderAndDeepCopiedPromptData()
    {
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);
        var file = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.DotFormgen();
        var p0 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLine
        {
            Settings = { Variable = "F0", Order = 0, Type = AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLineSettings.CodeType.PROMPT },
            PromptData = { Message = "Original Message", Choices = ["A", "B"] }
        };
        file.CodeLines.Add(p0);
        formgenUtils.ParsedFormgenFile = file;

        formgenUtils.ClonePrompt([p0]);

        Assert.Equal(2, file.CodeLines.Count);
        var clone = file.CodeLines[1];
        Assert.Equal("Copy of F0", clone.Settings?.Variable);
        Assert.Equal(1, clone.Settings?.Order);
        Assert.NotSame(p0.PromptData, clone.PromptData);
        Assert.Equal("Original Message", clone.PromptData?.Message);
        Assert.Equal(2, clone.PromptData?.Choices.Count);
    }

    [Fact]
    public void DeletePrompts_RemovesPromptsAndRenormalizesOrders()
    {
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);
        var file = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.DotFormgen();
        var p0 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLine
        {
            Settings = { Variable = "F0", Order = 0, Type = AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLineSettings.CodeType.PROMPT }
        };
        var p1 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLine
        {
            Settings = { Variable = "F1", Order = 1, Type = AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLineSettings.CodeType.PROMPT }
        };
        var p2 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLine
        {
            Settings = { Variable = "F2", Order = 2, Type = AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLineSettings.CodeType.PROMPT }
        };
        file.CodeLines.AddRange([p0, p1, p2]);
        formgenUtils.ParsedFormgenFile = file;

        formgenUtils.DeletePrompts([p1]);

        Assert.Equal(2, file.CodeLines.Count);
        Assert.Equal("F0", file.CodeLines[0].Settings?.Variable);
        Assert.Equal(0, file.CodeLines[0].Settings?.Order);
        Assert.Equal("F2", file.CodeLines[1].Settings?.Variable);
        Assert.Equal(1, file.CodeLines[1].Settings?.Order);
    }

    [Fact]
    public void MovePrompt_ReordersPromptsWithinSameGroup()
    {
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);
        var file = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.DotFormgen();
        var p0 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLine
        {
            Settings = { Variable = "F0", Order = 0, Type = AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLineSettings.CodeType.PROMPT }
        };
        var p1 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLine
        {
            Settings = { Variable = "F1", Order = 1, Type = AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.CodeLineSettings.CodeType.PROMPT }
        };
        file.CodeLines.AddRange([p0, p1]);
        formgenUtils.ParsedFormgenFile = file;

        formgenUtils.MovePrompt(p1, moveUp: true);

        Assert.Equal("F1", file.CodeLines[0].Settings?.Variable);
        Assert.Equal(0, file.CodeLines[0].Settings?.Order);
        Assert.Equal("F0", file.CodeLines[1].Settings?.Variable);
        Assert.Equal(1, file.CodeLines[1].Settings?.Order);
    }

    [Fact]
    public void CloneField_CreatesFieldWithUniqueIdAndOffsetCoordinates()
    {
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);
        var file = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.DotFormgen();
        var page = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormPage();
        var f1 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField
        {
            Expression = "'Test'",
            Settings =
            {
                ID = 5,
                LaserRect = new System.Drawing.Rectangle(100, 200, 80, 20),
                ImpactPosition = new System.Drawing.Point(10, 5)
            }
        };
        page.Fields.Add(f1);
        file.Pages.Add(page);
        formgenUtils.ParsedFormgenFile = file;

        var cloned = formgenUtils.CloneField(page, f1);

        Assert.Equal(2, page.Fields.Count);
        Assert.Equal(6, cloned.Settings?.ID);
        Assert.Equal(110, cloned.Settings?.LaserRect.X);
        Assert.Equal(210, cloned.Settings?.LaserRect.Y);
        Assert.Equal(11, cloned.Settings?.ImpactPosition.X);
        Assert.Equal(6, cloned.Settings?.ImpactPosition.Y);
    }

    [Fact]
    public void AlignFields_AlignsCoordinatesCorrectly()
    {
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);
        var f1 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField
        {
            Settings = { ID = 1, LaserRect = new System.Drawing.Rectangle(50, 100, 100, 20) }
        };
        var f2 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField
        {
            Settings = { ID = 2, LaserRect = new System.Drawing.Rectangle(120, 250, 150, 30) }
        };

        formgenUtils.AlignFields([f1, f2], FieldAlignmentMode.Left);

        Assert.Equal(50, f1.Settings.LaserRect.X);
        Assert.Equal(50, f2.Settings.LaserRect.X);

        formgenUtils.AlignFields([f1, f2], FieldAlignmentMode.MatchWidth);
        Assert.Equal(100, f2.Settings.LaserRect.Width);
    }

    [Fact]
    public void DistributeFields_SpacesFieldsEquidistantly()
    {
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);
        var f1 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField
        {
            Settings = { ID = 1, LaserRect = new System.Drawing.Rectangle(0, 0, 50, 20) }
        };
        var f2 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField
        {
            Settings = { ID = 2, LaserRect = new System.Drawing.Rectangle(30, 0, 50, 20) }
        };
        var f3 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField
        {
            Settings = { ID = 3, LaserRect = new System.Drawing.Rectangle(100, 0, 50, 20) }
        };

        formgenUtils.DistributeFields([f1, f2, f3], FieldDistributionMode.Horizontal);

        Assert.Equal(0, f1.Settings.LaserRect.X);
        Assert.Equal(50, f2.Settings.LaserRect.X);
        Assert.Equal(100, f3.Settings.LaserRect.X);
    }

    [Fact]
    public void BulkSetFieldProperties_UpdatesAllTargetFields()
    {
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);
        var f1 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField
        {
            Settings = { ID = 1, Bold = false, FontSize = 10 }
        };
        var f2 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField
        {
            Settings = { ID = 2, Bold = false, FontSize = 10 }
        };

        formgenUtils.BulkSetFieldProperties([f1, f2], bold: true, fontSize: 14);

        Assert.True(f1.Settings.Bold);
        Assert.Equal(14, f1.Settings.FontSize);
        Assert.True(f2.Settings.Bold);
        Assert.Equal(14, f2.Settings.FontSize);
    }

    [Fact]
    public void AlignFieldsAcrossPages_SyncsMatchingFieldGeometry()
    {
        var formgenUtils = new FormgenUtils(_mockFileSystem.Object, _formgenUtilsProperties);
        var file = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.DotFormgen();

        var page1 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormPage();
        page1.Fields.Add(new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField
        {
            Expression = "F100",
            Settings = { ID = 1, LaserRect = new System.Drawing.Rectangle(200, 300, 120, 25), Bold = true, FontSize = 12 }
        });

        var page2 = new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormPage();
        page2.Fields.Add(new AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField
        {
            Expression = "F100",
            Settings = { ID = 2, LaserRect = new System.Drawing.Rectangle(10, 10, 50, 10), Bold = false, FontSize = 8 }
        });

        file.Pages.AddRange([page1, page2]);
        formgenUtils.ParsedFormgenFile = file;

        int matched = formgenUtils.AlignFieldsAcrossPages(sourcePageIndex: 0);

        Assert.Equal(1, matched);
        var p2Field = page2.Fields[0];
        Assert.Equal(200, p2Field.Settings.LaserRect.X);
        Assert.Equal(300, p2Field.Settings.LaserRect.Y);
        Assert.Equal(120, p2Field.Settings.LaserRect.Width);
        Assert.Equal(25, p2Field.Settings.LaserRect.Height);
        Assert.True(p2Field.Settings.Bold);
        Assert.Equal(12, p2Field.Settings.FontSize);
    }
}