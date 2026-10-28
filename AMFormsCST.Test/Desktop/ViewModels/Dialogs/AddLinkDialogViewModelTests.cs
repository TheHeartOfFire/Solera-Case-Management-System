using AMFormsCST.Desktop.Models;
using AMFormsCST.Desktop.ViewModels.Dialogs;
using Wpf.Ui.Controls;
using Xunit;

namespace AMFormsCST.Test.Desktop.ViewModels.Dialogs;

public class AddLinkDialogViewModelTests
{
    [Fact]
    public void LoadFrom_PopulatesViewModelFields()
    {
        var model = new LinkModel
        {
            DisplayName = "Test App",
            TargetUrl = "https://example.com/app",
            Category = "Productivity",
            Description = "A useful app",
            Icon = SymbolRegular.Bookmark24
        };

        var vm = new AddLinkDialogViewModel();
        vm.LoadFrom(model);

        Assert.Equal("Edit Quick Link", vm.DialogTitle);
        Assert.Equal("Test App", vm.DisplayName);
        Assert.Equal("https://example.com/app", vm.Path);
        Assert.Equal("Productivity", vm.Category);
        Assert.Equal("A useful app", vm.Description);
        Assert.NotNull(vm.SelectedIcon);
        Assert.Equal(SymbolRegular.Bookmark24, vm.SelectedIcon.Symbol);
    }

    [Fact]
    public void AvailableIcons_ContainsDefaultChoices()
    {
        var vm = new AddLinkDialogViewModel();

        Assert.NotEmpty(vm.AvailableIcons);
        Assert.Contains(vm.AvailableIcons, i => i.Symbol == SymbolRegular.Globe24);
        Assert.Contains(vm.AvailableIcons, i => i.Symbol == SymbolRegular.Cloud24);
        Assert.Contains(vm.AvailableIcons, i => i.Symbol == SymbolRegular.Wrench24);
    }
}

