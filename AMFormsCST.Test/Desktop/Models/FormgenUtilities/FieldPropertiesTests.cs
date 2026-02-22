using AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure;
using AMFormsCST.Desktop.Models.FormgenUtilities;
using System.Linq;
using CoreField = AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField;
using CoreFormatOption = AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField.FormatOption;
using CoreFieldType = AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormFieldSettings.FieldType;
using CoreAlignment = AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormFieldSettings.Alignment;
using Assert = Xunit.Assert;

namespace AMFormsCST.Test.Desktop.Models.FormgenUtilities;

public class FieldPropertiesTests
{
    [Fact]
    public void Expression_SetProperty_UpdatesCoreModel()
    {
        // Arrange
        var coreField = new CoreField();
        var wrapper = new FieldProperties(coreField);

        // Act
        wrapper.Expression = "SUM(A,B)";

        // Assert
        Assert.Equal("SUM(A,B)", coreField.Expression);
    }

    [Fact]
    public void SampleData_SetProperty_UpdatesCoreModel()
    {
        // Arrange
        var coreField = new CoreField();
        var wrapper = new FieldProperties(coreField);

        // Act
        wrapper.SampleData = "Sample";

        // Assert
        Assert.Equal("Sample", coreField.SampleData);
    }

    [Fact]
    public void FormattingOption_SetProperty_UpdatesCoreModel()
    {
        // Arrange
        var coreField = new CoreField { FormattingOption = CoreFormatOption.None };
        var wrapper = new FieldProperties(coreField);

        // Act
        wrapper.FormattingOption = CoreFormatOption.NumbersAsWords;

        // Assert
        Assert.Equal(CoreFormatOption.NumbersAsWords, coreField.FormattingOption);
    }

    [Fact]
    public void GetDisplayProperties_ReturnsCorrectProperties()
    {
        // Arrange
        var coreField = new CoreField
        {
            Expression = "MyExpr",
            SampleData = "MyData",
            FormattingOption = CoreFormatOption.NAIfBlank,
            Settings =
            {
                ID = 123,
                Type = CoreFieldType.SIGNATURE,
                FontSize = 12,
                FontAlignment = CoreAlignment.RIGHT,
                Bold = true,
                ShrinkToFit = false
            }
        };
        var wrapper = new FieldProperties(coreField);

        // Act
        var displayProperties = wrapper.GetDisplayProperties().ToList();

        // Assert
        Assert.Equal(13, displayProperties.Count);
        Assert.Contains(displayProperties, dp => dp.Name == "Expression" && dp.Value == "MyExpr");
        Assert.Contains(displayProperties, dp => dp.Name == "SampleData" && dp.Value == "MyData");
        Assert.Contains(displayProperties, dp => dp.Name == "FormattingOption" && dp.Value.ToString() == "NAIfBlank");
        Assert.Contains(displayProperties, dp => dp.Name == "ID" && dp.Value.Equals(123));
        Assert.Contains(displayProperties, dp => dp.Name == "Type" && dp.Value.ToString() == "SIGNATURE");
        Assert.Contains(displayProperties, dp => dp.Name == "FontSize" && dp.Value.Equals(12));
        Assert.Contains(displayProperties, dp => dp.Name == "FontAlignment" && dp.Value.ToString() == "RIGHT");
        Assert.Contains(displayProperties, dp => dp.Name == "Bold" && dp.Value.Equals(true));
        Assert.Contains(displayProperties, dp => dp.Name == "ShrinkToFit" && dp.Value.Equals(false));
        Assert.Contains(displayProperties, dp => dp.Name == "LaserRectY");
        Assert.Contains(displayProperties, dp => dp.Name == "LaserRectHeight");
        Assert.Contains(displayProperties, dp => dp.Name == "LaserRectX");
        Assert.Contains(displayProperties, dp => dp.Name == "LaserRectWidth");
    }

    [Fact]
    public void LaserRectX_SetProperty_UpdatesCoreModel()
    {
        // Arrange
        var coreField = new CoreField();
        // Initialize LaserRect in Settings
        var rect = new System.Drawing.Rectangle(10, 20, 100, 50);
        coreField.Settings.LaserRect = rect;
        
        var wrapper = new FieldProperties(coreField);

        // Act
        wrapper.LaserRectX = 99;

        // Assert
        Assert.Equal(99, coreField.Settings.LaserRect.X);
    }
}