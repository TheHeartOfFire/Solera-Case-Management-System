using AMFormsCST.Desktop.Models;
using AMFormsCST.Desktop.ViewModels.Dialogs;
using AMFormsCST.Desktop.ViewModels.Pages.Links;
using AMFormsCST.Desktop.Views.Dialogs;
using AMFormsCST.Test.Helpers;
using System.Linq;
using System.Windows;
using Wpf.Ui.Controls;
using Xunit;

namespace AMFormsCST.Test.Desktop.Views.Dialogs;

[Collection("STA Tests")]
public class AddEditLinkWindowTests
{
    [WpfFact]
    public void Constructor_WithViewModel_InitializesCorrectly()
    {
        var vm = new AddLinkDialogViewModel();
        var window = new AddEditLinkWindow(vm);

        Assert.Equal(vm, window.ViewModel);
        Assert.Equal(vm, window.DataContext);
        Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);
        Assert.Equal(WindowBackdropType.None, window.WindowBackdropType);
        Assert.Null(window.Owner);
    }

    [WpfFact]
    public void Constructor_WithEditModel_InitializesCorrectly()
    {
        var model = new LinkModel
        {
            DisplayName = "Test",
            Category = "Portals",
            TargetUrl = "https://test.com"
        };
        var vm = new AddLinkDialogViewModel();
        vm.LoadFrom(model);
        var window = new AddEditLinkWindow(vm);

        Assert.Equal(vm, window.ViewModel);
        Assert.Equal("Edit Quick Link", vm.DialogTitle);
        Assert.Equal(WindowStartupLocation.CenterScreen, window.WindowStartupLocation);
        Assert.Equal(WindowBackdropType.None, window.WindowBackdropType);
        Assert.Null(window.Owner);
    }

    [WpfFact]
    public void LinksViewModel_ExposesExpectedRelayCommands()
    {
        var vm = new LinksViewModel();
        var propertyNames = typeof(LinksViewModel)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        Assert.Contains("AddLinkCommand", propertyNames);
        Assert.Contains("EditLinkCommand", propertyNames);
        Assert.Contains("DeleteLinkCommand", propertyNames);
        Assert.Contains("CopyLinkCommand", propertyNames);
        Assert.Contains("ResetDefaultsCommand", propertyNames);
        Assert.Contains("OpenLinkCommand", propertyNames);
        Assert.Contains("ClearSearchCommand", propertyNames);

        Assert.NotNull(vm.AddLinkCommand);
        Assert.NotNull(vm.EditLinkCommand);
        Assert.NotNull(vm.DeleteLinkCommand);
        Assert.NotNull(vm.CopyLinkCommand);
        Assert.NotNull(vm.ResetDefaultsCommand);
        Assert.NotNull(vm.OpenLinkCommand);
        Assert.NotNull(vm.ClearSearchCommand);
    }
}

