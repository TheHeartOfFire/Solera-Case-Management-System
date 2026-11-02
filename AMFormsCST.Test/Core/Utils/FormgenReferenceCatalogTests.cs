using AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure;
using AMFormsCST.Core.Utils;
using Xunit;

namespace AMFormsCST.Test.Core.Utils;

public class FormgenReferenceCatalogTests
{
    [Fact]
    public void Catalog_ContainsF0_F199_AndUdfFields()
    {
        // F0: Separator and label prompt placeholder
        var f0 = FormgenReferenceCatalog.TryGetField("F0");
        Assert.NotNull(f0);
        Assert.Equal(0, f0.FieldNum);
        Assert.Equal("F0", f0.VariableName);
        Assert.True(f0.IsVehicleMerchandisingSafe);
        Assert.Contains("Separator", f0.Description);

        // Numeric lookup
        Assert.Same(f0, FormgenReferenceCatalog.TryGetField(0));

        // F199: NA field
        var f199 = FormgenReferenceCatalog.TryGetField("F199");
        Assert.NotNull(f199);
        Assert.Equal(199, f199.FieldNum);
        Assert.True(f199.IsVehicleMerchandisingSafe);
        Assert.Same(f199, FormgenReferenceCatalog.TryGetField(199));

        // F101..F198: Programmer-reserved UDFs
        var f101 = FormgenReferenceCatalog.TryGetField("F101");
        Assert.NotNull(f101);
        Assert.Equal(101, f101.FieldNum);
        Assert.True(f101.IsVehicleMerchandisingSafe);

        var f150 = FormgenReferenceCatalog.TryGetField("F150");
        Assert.NotNull(f150);
        Assert.Equal(150, f150.FieldNum);
        Assert.True(f150.IsVehicleMerchandisingSafe);

        var f198 = FormgenReferenceCatalog.TryGetField("F198");
        Assert.NotNull(f198);
        Assert.Equal(198, f198.FieldNum);
        Assert.True(f198.IsVehicleMerchandisingSafe);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(199, true)]
    [InlineData(101, true)]
    [InlineData(150, true)]
    [InlineData(198, true)]
    [InlineData(61, true)]    // VH-YEAR (VM Safe)
    [InlineData(71, true)]    // DL-VEH-VIN (VM Safe)
    [InlineData(3, false)]    // Selling Price (Deal Only)
    [InlineData(202, false)]  // DL-MSRP-AMT (Explicitly marked VM unsafe)
    public void IsVmSafeField_ReturnsExpectedSafety(int fieldNum, bool expectedSafe)
    {
        bool actual = FormgenReferenceCatalog.IsVmSafeField(fieldNum);
        Assert.Equal(expectedSafe, actual);
    }

    [Fact]
    public void CheckVehicleMerchandisingSafety_WithF0_F199_AndUdfs_EvaluatesSafe()
    {
        var form = new DotFormgen
        {
            Pages =
            [
                new FormPage
                {
                    Fields =
                    [
                        new FormField { Expression = "F0" },
                        new FormField { Expression = "F199" },
                        new FormField { Expression = "F101 + F150" },
                        new FormField { Expression = "F71" } // VIN
                    ]
                }
            ],
            CodeLines =
            [
                new CodeLine { Expression = "F198 = 'TEST'" }
            ]
        };

        var analysis = FormgenReferenceCatalog.CheckVehicleMerchandisingSafety(form);

        Assert.True(analysis.IsVmSafe);
        Assert.Empty(analysis.DisallowedFields);
        Assert.Empty(analysis.Violations);
        Assert.Equal("VM Safe ✓", analysis.BadgeText);
    }

    [Fact]
    public void CheckVehicleMerchandisingSafety_WithDealFields_FlagsDisallowed()
    {
        var form = new DotFormgen
        {
            Pages =
            [
                new FormPage
                {
                    Fields =
                    [
                        new FormField { Expression = "F3" }, // Selling price
                        new FormField { Expression = "F0" }
                    ]
                }
            ]
        };

        var analysis = FormgenReferenceCatalog.CheckVehicleMerchandisingSafety(form);

        Assert.False(analysis.IsVmSafe);
        Assert.Contains(3, analysis.DisallowedFields);
        Assert.DoesNotContain(0, analysis.DisallowedFields);
        Assert.Equal("Deal Only", analysis.BadgeText);
    }
}

