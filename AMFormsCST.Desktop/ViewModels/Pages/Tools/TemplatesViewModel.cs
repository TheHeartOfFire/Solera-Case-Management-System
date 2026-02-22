using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Desktop.Models.Templates;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.ViewModels.Dialogs;
using AMFormsCST.Desktop.ViewModels.Pages.Tools.Templates;
using AMFormsCST.Desktop.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools;

public partial class TemplatesViewModel : ViewModel
{
    private readonly ILogService? _logger;

    [ObservableProperty]
    private ObservableCollection<TemplateItemViewModel> _templates;

    [ObservableProperty]
    private TemplateItemViewModel? _selectedTemplate;

    private ISupportTool _supportTool;
    private readonly IFileSystem _fileSystem;

    public ICollectionView TemplatesView { get; }

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
                TemplatesView.Refresh();
        }
    }

    public ObservableCollection<string> SortOptions { get; }
    private string _selectedSortOption;
    public string SelectedSortOption
    {
        get => _selectedSortOption;
        set
        {
            if (SetProperty(ref _selectedSortOption, value))
                ApplySort();
        }
    }

    public ObservableCollection<TemplateTypeFilterItem> TemplateTypes { get; }
    private TemplateTypeFilterItem? _selectedFilterType;
    public TemplateTypeFilterItem? SelectedFilterType
    {
        get => _selectedFilterType;
        set
        {
            if (SetProperty(ref _selectedFilterType, value))
                TemplatesView.Refresh();
        }
    }

    public TemplatesViewModel(ISupportTool supportTool, IFileSystem fs, ILogService? logger = null)
    {
        _supportTool = supportTool ?? throw new ArgumentNullException(nameof(supportTool));
        _fileSystem = fs ?? throw new ArgumentNullException(nameof(fs));
        _logger = logger;
        _templates = new(_supportTool.Enforcer.Templates.Select(t => new TemplateItemViewModel(t, _supportTool) { Template = t }));

        TemplatesView = CollectionViewSource.GetDefaultView(_templates);
        TemplatesView.Filter = FilterTemplates;

        SortOptions = new ObservableCollection<string> { "Type", "Name (A-Z)", "Name (Z-A)" };
        _selectedSortOption = SortOptions.First();

        var types = Enum.GetValues(typeof(TextTemplate.TemplateType))
                        .Cast<TextTemplate.TemplateType?>()
                        .Select(t => new TemplateTypeFilterItem(t))
                        .ToList();
        types.Insert(0, new TemplateTypeFilterItem(null));
        TemplateTypes = new ObservableCollection<TemplateTypeFilterItem>(types);
        _selectedFilterType = TemplateTypes.First();

        ApplySort();

        if (_templates.Count > 0)
        {
            SelectTemplate(_selectedTemplate = _templates.First());
        }
        _logger?.LogInfo($"TemplatesViewModel initialized with {_templates.Count} templates.");
    }
    #region Design-Time Constructor
    public TemplatesViewModel()
    {
        _supportTool = new DesignTimeSupportTool();
        _fileSystem = new DesignTimeFileSystem();
        _templates = new(_supportTool.Enforcer.Templates.Select(t => new TemplateItemViewModel(t, _supportTool)));

        TemplatesView = CollectionViewSource.GetDefaultView(_templates);
        SortOptions = new ObservableCollection<string> { "Type", "Name (A-Z)", "Name (Z-A)" };
        _selectedSortOption = SortOptions.First();
        TemplateTypes = new ObservableCollection<TemplateTypeFilterItem> { new(null) };
        _selectedFilterType = TemplateTypes.First();

        if (_templates.Any())
        {
            SelectTemplate(_templates.First());
        }
    }
    #endregion

    private bool FilterTemplates(object obj)
    {
        if (obj is not TemplateItemViewModel templateVm) return false;

        var nameMatch = string.IsNullOrWhiteSpace(FilterText) ||
                        templateVm.Template.Name.Contains(FilterText, StringComparison.OrdinalIgnoreCase) ||
                        templateVm.Template.Description.Contains(FilterText, StringComparison.OrdinalIgnoreCase);

        var typeMatch = SelectedFilterType?.Value is null ||
                        templateVm.Template.Type == SelectedFilterType.Value;

        return nameMatch && typeMatch;
    }

    private void ApplySort()
    {
        TemplatesView.SortDescriptions.Clear();
        ListSortDirection direction;
        string propertyName;

        switch (SelectedSortOption)
        {
            case "Name (A-Z)":
                propertyName = "Template.Name";
                direction = ListSortDirection.Ascending;
                break;
            case "Name (Z-A)":
                propertyName = "Template.Name";
                direction = ListSortDirection.Descending;
                break;
            case "Type":
            default:
                propertyName = "Template.Type";
                direction = ListSortDirection.Ascending;
                break;
        }
        TemplatesView.SortDescriptions.Add(new SortDescription(propertyName, direction));
    }

    partial void OnTemplatesChanged(ObservableCollection<TemplateItemViewModel> value)
    {
        var newView = CollectionViewSource.GetDefaultView(value);
        newView.Filter = TemplatesView.Filter;
        foreach (var sort in TemplatesView.SortDescriptions)
        {
            newView.SortDescriptions.Add(sort);
        }
        
        newView.Refresh();
        OnPropertyChanged(nameof(TemplatesView)); 
    }

    [RelayCommand]
    private void AddTemplate()
    {
        try
        {
            var dialog = new NewTemplateDialog();
            bool? result = dialog.ShowDialog();

            if (result is not true) return;

            var template = new TemplateItemViewModel(
                new TextTemplate(
                    dialog.TemplateName,
                    dialog.TemplateDescription,
                    ((NewTemplateDialogViewModel)dialog.DataContext).TemplateContent,
                    dialog.Type),
                _supportTool);

            _supportTool.Enforcer.AddTemplate(template.Template);

            Templates = new(_supportTool.Enforcer.Templates.Select(t => new TemplateItemViewModel(t, _supportTool) { Template = t }));

            SelectTemplate(template);
            TemplatesView.Refresh();
            _logger?.LogInfo($"Template added: {template.Template.Name}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error adding template.", ex);
        }
    }

    [RelayCommand]
    private void RefreshTemplate()
    {
        SelectedTemplate.RefreshTemplateData();
    }

    [RelayCommand]
    private void EditTemplate()
    {
        try
        {
            if (SelectedTemplate == null) return;

            // Template.Text now returns a fresh FlowDocument from stored XAML string, 
            // so no manual cloning is needed to avoid parenting issues.
            var dialog = new NewTemplateDialog(
                SelectedTemplate.Template.Name,
                SelectedTemplate.Template.Description,
                SelectedTemplate.Template.Text,
                SelectedTemplate.Template.Type);

            bool? result = dialog.ShowDialog();

            if (result is not true) return;

            SelectedTemplate.Template.Name = dialog.TemplateName;
            SelectedTemplate.Template.Description = dialog.TemplateDescription;
            // Setting .Text will serialize the document to XAML string for storage
            SelectedTemplate.Template.Text = ((NewTemplateDialogViewModel)dialog.DataContext).TemplateContent;
            SelectedTemplate.Template.Type = dialog.Type;

            SelectedTemplate.RefreshTemplateData();
            _supportTool.Enforcer.UpdateTemplate(SelectedTemplate.Template);
            SelectTemplate(SelectedTemplate);
            TemplatesView.Refresh(); 
            _logger?.LogInfo($"Template edited: {SelectedTemplate.Template.Name}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error editing template.", ex);
        }
    }

    [RelayCommand]
    private void RemoveTemplate()
    {
        try
        {
            if (SelectedTemplate is null) return;

            _supportTool.Enforcer.RemoveTemplate(SelectedTemplate.Template);
            Templates = new(_supportTool.Enforcer.Templates.Select(t => new TemplateItemViewModel(t, _supportTool) { Template = t }));

            SelectTemplate(Templates.FirstOrDefault());
            TemplatesView.Refresh();
            _logger?.LogInfo($"Template removed: {SelectedTemplate?.Template.Name}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error removing template.", ex);
        }
    }

    [RelayCommand]
    internal void SelectTemplate(TemplateItemViewModel? item)
    {
        var itemInCollection = _templates.FirstOrDefault(t => t.Template.Id == item?.Template.Id);

        if (itemInCollection == SelectedTemplate) return;

        SelectedTemplate?.Deselect();
        SelectedTemplate = itemInCollection;
        SelectedTemplate?.Select();
        _logger?.LogInfo($"Template selected: {item?.Template.Name}");
    }

    [RelayCommand]
    private void CopyTemplate(TemplateItemViewModel item)
    {
        try
        {
            if (item == SelectedTemplate) return;

            Clipboard.SetText(TextTemplate.GetFlowDocumentPlainText(SelectedTemplate?.Output));
            _logger?.LogInfo($"Template output copied: {SelectedTemplate?.Template.Name}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error copying template output.", ex);
        }
    }

    [RelayCommand]
    private void ResetTemplate(TemplateItemViewModel item)
    {
        _logger?.LogInfo($"ResetTemplate called for: {item.Template.Name}");
    }

    [RelayCommand]
    private void ImportTemplate(TemplateItemViewModel item)
    {
        try
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Templates",
                Filter = "JSON Files (*.json)|*.json",
                InitialDirectory = _fileSystem.CombinePath(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "FormgenAssistant"
                ),
                FileName = "Templates.json",
                CheckFileExists = true,
                CheckPathExists = true
            };

            bool? result = dialog.ShowDialog();
            if (result != true) return;

            var filePath = dialog.FileName;
            if (!_fileSystem.FileExists(filePath)) return;

            var json = _fileSystem.ReadAllText(filePath);

            var importedTemplates = System.Text.Json.JsonSerializer.Deserialize<DeprecatedTemplateList>(json);

            if (importedTemplates is null || importedTemplates.TemplateList.Count == 0)
            {
                MessageBox.Show("No templates found in the selected file.", "Import Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                _logger?.LogWarning("ImportTemplate: No templates found in the selected file.");
                return;
            }

            foreach (var deprecated in importedTemplates.TemplateList)
            {
                var converted = (TextTemplate)deprecated;
                _supportTool.Enforcer.AddTemplate(converted);
            }

            Templates = new(_supportTool.Enforcer.Templates.Select(t => new TemplateItemViewModel(t, _supportTool) { Template = t }));
            _logger?.LogInfo($"Templates imported from: {filePath}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error importing templates.", ex);
        }
    }

    public void Refresh()
    {
        TemplatesView.Refresh();
        SelectedTemplate.RefreshTemplateData();
    }
}