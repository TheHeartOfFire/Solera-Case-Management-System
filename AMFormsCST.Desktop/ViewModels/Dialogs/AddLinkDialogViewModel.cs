using AMFormsCST.Desktop.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Linq;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop.ViewModels.Dialogs
{
    public class IconChoiceItem
{
    public SymbolRegular Symbol { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public partial class AddLinkDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _dialogTitle = "Add Quick Link";

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _path = string.Empty;

    [ObservableProperty]
    private string _category = "Custom";

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private IconChoiceItem _selectedIcon;

    [ObservableProperty]
    private ObservableCollection<IconChoiceItem> _availableIcons = [];

    [ObservableProperty]
    private ObservableCollection<string> _availableCategories = [];

    public bool IsEditMode { get; set; }
    public string? LinkId { get; set; }

    public AddLinkDialogViewModel()
    {
        AvailableCategories =
        [
            "Portals",
            "Forms & Compliance",
            "Tools & Utilities",
            "Custom",
            "General"
        ];

        AvailableIcons =
        [
            new() { Symbol = SymbolRegular.Globe24, DisplayName = "Web / Portal (Globe)" },
            new() { Symbol = SymbolRegular.Link24, DisplayName = "Quick Link (Link)" },
            new() { Symbol = SymbolRegular.Cloud24, DisplayName = "Cloud / Salesforce" },
            new() { Symbol = SymbolRegular.PersonAccounts24, DisplayName = "People / HR (Workday)" },
            new() { Symbol = SymbolRegular.Money24, DisplayName = "Payroll / Finance (ADP)" },
            new() { Symbol = SymbolRegular.AppGeneric24, DisplayName = "Applications (MyApps)" },
            new() { Symbol = SymbolRegular.DocumentTable24, DisplayName = "Reports / Excel" },
            new() { Symbol = SymbolRegular.TaskListLtr24, DisplayName = "Forms / Checklist" },
            new() { Symbol = SymbolRegular.Wrench24, DisplayName = "Tools / Utilities (CST)" },
            new() { Symbol = SymbolRegular.DocumentBulletList24, DisplayName = "FormGen Designer" },
            new() { Symbol = SymbolRegular.FolderOpen24, DisplayName = "Folder / Local Path" },
            new() { Symbol = SymbolRegular.Code24, DisplayName = "Code / Script" },
            new() { Symbol = SymbolRegular.BookQuestionMark24, DisplayName = "Documentation / Help" }
        ];

        _selectedIcon = AvailableIcons[0];
    }

    public void LoadFrom(LinkModel link)
    {
        IsEditMode = true;
        LinkId = link.Id;
        DialogTitle = "Edit Quick Link";
        DisplayName = link.DisplayName;
        Path = link.ResolvedPath;
        Category = string.IsNullOrWhiteSpace(link.Category) ? "Custom" : link.Category;
        Description = link.Description ?? string.Empty;

        var foundIcon = AvailableIcons.FirstOrDefault(i => i.Symbol == link.Icon);
        if (foundIcon != null)
        {
            SelectedIcon = foundIcon;
        }
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Application or File",
            Filter = "All Openable Files (*.exe;*.bat;*.cmd;*.lnk;*.url;*.*)|*.exe;*.bat;*.cmd;*.lnk;*.url;*.*|Applications (*.exe)|*.exe|Shortcuts & Scripts (*.bat;*.cmd;*.lnk;*.url)|*.bat;*.cmd;*.lnk;*.url|All Files (*.*)|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            Path = dialog.FileName;
            if (string.IsNullOrWhiteSpace(DisplayName))
            {
                DisplayName = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }
}
}