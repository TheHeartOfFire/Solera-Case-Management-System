using System.IO;
using AMFormsCST.Desktop.Services;
using Microsoft.Win32;
using Xunit;

namespace AMFormsCST.Test.Desktop.Services;

public class WindowsExplorerContextMenuServiceTests
{
    [Fact]
    public void AssociateFormgen_WhenPathDoesNotExist_ReturnsFalse()
    {
        var service = new WindowsExplorerContextMenuService();
        bool result = service.AssociateFormgen(@"C:\NonExistent_Path_12345\formgen.bat");
        Assert.False(result);
    }

    [Fact]
    public void AssociateFormgen_WhenPathIsEmpty_ReturnsFalse()
    {
        var service = new WindowsExplorerContextMenuService();
        bool result = service.AssociateFormgen(string.Empty);
        Assert.False(result);
    }

    [Fact]
    public void AssociateFormgen_WhenFileExists_ConfiguresRegistryAndReturnsTrue()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            var service = new WindowsExplorerContextMenuService();
            bool result = service.AssociateFormgen(tempFile);
            Assert.True(result);

            // Verify .formgen ProgId
            using var extKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.formgen");
            Assert.NotNull(extKey);
            Assert.Equal("formgenfile", extKey.GetValue(""));

            // Verify formgenfile command
            using var cmdKey = Registry.CurrentUser.OpenSubKey(@"Software\Classes\formgenfile\shell\open\command");
            Assert.NotNull(cmdKey);
            string? command = cmdKey.GetValue("") as string;
            Assert.NotNull(command);
            Assert.Contains("--open-in-formgen", command);

            // Verify IsFormgenAssociated
            Assert.True(service.IsFormgenAssociated());
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void UnregisterContextMenu_CleansUpVerbsWithoutThrowing()
    {
        var service = new WindowsExplorerContextMenuService();
        var ex = Record.Exception(() => service.UnregisterContextMenu());
        Assert.Null(ex);
    }
}
