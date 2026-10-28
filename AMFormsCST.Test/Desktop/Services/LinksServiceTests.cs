using AMFormsCST.Desktop.Models;
using AMFormsCST.Desktop.Services;
using System.Text.Json;
using Xunit;

namespace AMFormsCST.Test.Desktop.Services;

public class LinksServiceTests
{
    private readonly LinksService _service = new(logger: null);

    [Fact]
    public void GetDefaultLinks_ReturnsExpectedList()
    {
        var defaults = _service.GetDefaultLinks();

        Assert.NotNull(defaults);
        Assert.NotEmpty(defaults);
        Assert.Contains(defaults, l => l.DisplayName == "Salesforce");
        Assert.Contains(defaults, l => l.DisplayName == "Workday");
        Assert.Contains(defaults, l => l.DisplayName == "ADP");
        Assert.Contains(defaults, l => l.DisplayName == "FormGen");
        Assert.Contains(defaults, l => l.DisplayName == "CST");
    }

    [Fact]
    public void ResolvePath_ResolvesKnownKeys()
    {
        var salesforcePath = _service.ResolvePath("SalesForce");
        Assert.Equal("https://am.my.salesforce.com", salesforcePath);

        var workdayPath = _service.ResolvePath("Workday");
        Assert.Equal("https://wd5.myworkday.com/solera/d/home.htmld", workdayPath);

        var adpPath = _service.ResolvePath("ADP");
        Assert.Equal("https://my.adp.com", adpPath);
    }

    [Fact]
    public void ResolvePath_PreservesArbitraryUrl()
    {
        const string url = "https://custom.internal.portal/dashboard";
        var resolved = _service.ResolvePath(url);

        Assert.Equal(url, resolved);
    }

    [Fact]
    public void ResolvePath_ExpandsEnvironmentVariables()
    {
        var input = @"%USERPROFILE%\Documents";
        var resolved = _service.ResolvePath(input);

        Assert.DoesNotContain("%USERPROFILE%", resolved);
        Assert.Equal(Environment.ExpandEnvironmentVariables(input), resolved);
    }

    [Fact]
    public void Links_CanBeSerializedAndDeserialized()
    {
        var original = new List<LinkModel>
        {
            new()
            {
                Id = "custom-1",
                DisplayName = "Custom Tool",
                Category = "Utilities",
                Key = "https://internal.tool",
                TargetUrl = "https://internal.tool",
                Description = "My custom tool",
                IsBuiltIn = false
            }
        };

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<List<LinkModel>>(json);

        Assert.NotNull(deserialized);
        Assert.Single(deserialized);
        Assert.Equal("Custom Tool", deserialized[0].DisplayName);
        Assert.Equal("Utilities", deserialized[0].Category);
        Assert.Equal("https://internal.tool", deserialized[0].TargetUrl);
        Assert.False(deserialized[0].IsBuiltIn);
    }
}

