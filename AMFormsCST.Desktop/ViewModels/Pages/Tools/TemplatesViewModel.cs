using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Core.Types.Notebook;
using AMFormsCST.Core.Utils;
using AMFormsCST.Desktop.Models.Templates;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.ViewModels.Dialogs;
using AMFormsCST.Desktop.ViewModels.Pages.Tools.Templates;
using AMFormsCST.Desktop.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using static AMFormsCST.Core.Types.BestPractices.TextTemplates.Models.TextTemplate;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools;

public partial class TemplatesViewModel : ViewModel
{
    private readonly ILogService? _logger;
    private readonly ITemplateAssemblyService _assemblyService;

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

    public ObservableCollection<string> TagFilterOptions { get; } = ["All", "#Internal", "#Email", "#Publish", "#Closure"];

    private string _selectedTagFilter = "All";
    public string SelectedTagFilter
    {
        get => _selectedTagFilter;
        set
        {
            if (SetProperty(ref _selectedTagFilter, value))
                TemplatesView.Refresh();
        }
    }

    private TemplateCategoryKind? _selectedCategoryKind;
    public TemplateCategoryKind? SelectedCategoryKind
    {
        get => _selectedCategoryKind;
        set
        {
            if (SetProperty(ref _selectedCategoryKind, value))
                TemplatesView.Refresh();
        }
    }

    [ObservableProperty]
    private string _livePreviewText = string.Empty;

    [ObservableProperty]
    private string _newTagInput = string.Empty;

    public ObservableCollection<string> VariableTokens { get; } =
    [
        "{Contact:FirstName}",
        "{StoreList}",
        "{FormName}",
        "{DealNumber}",
        "{LinesAdjusted}",
        "{CaseNumber}",
        "{ServerID}",
        "{Dealership}",
        "{DealerEmail}"
    ];

    public IEnumerable<TemplateCategoryKind> AvailableCategoryKinds =>
        Enum.GetValues(typeof(TemplateCategoryKind)).Cast<TemplateCategoryKind>();

    public IEnumerable<CaseActionType> AvailableActionTypes =>
        Enum.GetValues(typeof(CaseActionType)).Cast<CaseActionType>();

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

    public TemplatesViewModel(ISupportTool supportTool, IFileSystem fs, ITemplateAssemblyService? assemblyService = null, ILogService? logger = null)
    {
        _supportTool = supportTool ?? throw new ArgumentNullException(nameof(supportTool));
        _fileSystem = fs ?? throw new ArgumentNullException(nameof(fs));
        _assemblyService = assemblyService ?? new TemplateAssemblyService();
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
        _assemblyService = new TemplateAssemblyService();
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

        var tagMatch = SelectedTagFilter == "All" ||
                       templateVm.Template.Tags.Any(t => t.Equals(SelectedTagFilter, StringComparison.OrdinalIgnoreCase));

        var kindMatch = SelectedCategoryKind == null ||
                        templateVm.Template.Kind == SelectedCategoryKind;

        return nameMatch && typeMatch && tagMatch && kindMatch;
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
        var itemInCollection = Templates.FirstOrDefault(t => t.Template.Id == item?.Template.Id);

        if (itemInCollection == SelectedTemplate) return;

        SelectedTemplate?.Deselect();
        SelectedTemplate = itemInCollection;
        SelectedTemplate?.Select();
        RefreshLivePreview();
        _logger?.LogInfo($"Template selected: {item?.Template.Name}");
    }

    [RelayCommand]
    private void InsertVariableToken(string? token)
    {
        if (SelectedTemplate == null || string.IsNullOrWhiteSpace(token)) return;

        var currentText = TextTemplate.GetFlowDocumentPlainText(SelectedTemplate.Template.Text);
        var updated = (currentText + " " + token).Trim();
        var doc = new FlowDocument(new Paragraph(new Run(updated)));
        SelectedTemplate.Template.Text = doc;
        SelectedTemplate.RefreshTemplateData();
        RefreshLivePreview();
    }

    [RelayCommand]
    private void AddTag()
    {
        if (SelectedTemplate == null || string.IsNullOrWhiteSpace(NewTagInput)) return;
        SelectedTemplate.AddTag(NewTagInput);
        NewTagInput = string.Empty;
        TemplatesView.Refresh();
        RefreshLivePreview();
    }

    [RelayCommand]
    private void RemoveTag(string? tag)
    {
        if (SelectedTemplate == null || string.IsNullOrWhiteSpace(tag)) return;
        SelectedTemplate.RemoveTag(tag);
        TemplatesView.Refresh();
        RefreshLivePreview();
    }

    [RelayCommand]
    private void SaveTemplateChanges()
    {
        if (SelectedTemplate == null) return;
        _supportTool.Enforcer.UpdateTemplate(SelectedTemplate.Template);
        RefreshLivePreview();
        _logger?.LogInfo($"Saved template changes for: {SelectedTemplate.Template.Name}");
    }

    [RelayCommand]
    public void RefreshLivePreview()
    {
        if (SelectedTemplate == null)
        {
            LivePreviewText = string.Empty;
            return;
        }

        try
        {
            var note = _supportTool?.Notebook?.Notes?.SelectedItem
                ?? _supportTool?.Notebook?.Notes?.FirstOrDefault();

            if (SelectedTemplate.Template.Kind == TemplateCategoryKind.ActionPartial)
            {
                var entry = note?.LogEntries?.LastOrDefault(e => e.IncludeInOutput)
                    ?? new CaseLogEntry
                    {
                        Action = SelectedTemplate.Template.AssociatedAction ?? CaseActionType.LineAdjustments,
                        FormName = "F102 - Buyers Order",
                        Details = "Shifted lines 14-16 down 2pt for dot matrix alignment",
                        Timestamp = DateTime.Now
                    };

                LivePreviewText = _assemblyService.ResolveActionBlock(entry, SelectedTemplate.Template);
            }
            else
            {
                LivePreviewText = TextTemplate.GetFlowDocumentPlainText(SelectedTemplate.Output);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error generating live preview.", ex);
            LivePreviewText = $"Preview Error: {ex.Message}";
        }
    }

    [RelayCommand]
    private void CopyTemplate(TemplateItemViewModel? item)
    {
        try
        {
            var target = item ?? SelectedTemplate;
            if (target == null) return;

            var text = !string.IsNullOrWhiteSpace(LivePreviewText)
                ? LivePreviewText
                : TextTemplate.GetFlowDocumentPlainText(target.Output);

            Clipboard.SetText(text);
            _logger?.LogInfo($"Template output copied: {target.Template.Name}");
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
        SelectedTemplate?.RefreshTemplateData();
        RefreshLivePreview();
    }
}