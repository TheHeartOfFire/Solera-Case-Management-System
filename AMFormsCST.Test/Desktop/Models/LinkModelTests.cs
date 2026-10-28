using AMFormsCST.Desktop.Models;
using Wpf.Ui.Controls;
using Xunit;

namespace AMFormsCST.Test.Desktop.Models;

public class LinkModelTests
{
    [Fact]
    public void LinkModel_Defaults_AreInitialized()
    {
        var model = new LinkModel();

        Assert.NotNull(model.Id);
        Assert.NotEmpty(model.Id);
        Assert.Equal(string.Empty, model.DisplayName);
        Assert.Equal("General", model.Category);
        Assert.Equal(SymbolRegular.Globe24, model.Icon);
        Assert.False(model.IsBuiltIn);
    }

    [Fact]
    public void ResolvedPath_PrefersTargetUrl_WhenProvided()
    {
        var model = new LinkModel
        {
            Key = "OldKey",
            TargetUrl = "https://example.com/target"
        };

        Assert.Equal("https://example.com/target", model.ResolvedPath);
    }

    [Fact]
    public void ResolvedPath_FallsBackToKey_WhenTargetUrlIsEmpty()
    {
        var model = new LinkModel
        {
            Key = "FallbackKey",
            TargetUrl = ""
        };

        Assert.Equal("FallbackKey", model.ResolvedPath);
    }

    [Fact]
    public void IsWebLink_ReturnsTrueForHttpAndHttps()
    {
        var http = new LinkModel { TargetUrl = "http://example.com" };
        var https = new LinkModel { TargetUrl = "https://example.com" };
        var file = new LinkModel { TargetUrl = "C:\\Tools\\app.exe" };

        Assert.True(http.IsWebLink);
        Assert.True(https.IsWebLink);
        Assert.False(file.IsWebLink);
    }

    [Fact]
    public void DisplayHost_ExtractsHost_ForWebUrls()
    {
        var model = new LinkModel { TargetUrl = "https://sub.domain.com/path/to/resource" };

        Assert.Equal("sub.domain.com", model.DisplayHost);
    }

    [Fact]
    public void DisplayHost_ExtractsFileName_ForFilePaths()
    {
        var model = new LinkModel { TargetUrl = @"C:\Program Files\App\test.exe" };

        Assert.Equal("test.exe", model.DisplayHost);
    }
}

