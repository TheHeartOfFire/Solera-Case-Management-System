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
}