using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.Models;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.ViewModels.Dialogs;
using AMFormsCST.Desktop.Views.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using ILinksService = AMFormsCST.Desktop.Services.ILinksService;

namespace AMFormsCST.Desktop.ViewModels.Pages.Links
{
    public partial class LinksViewModel : ObservableObject, INavigationAware
{
    private readonly ILogService? _logger;
    private readonly ILinksService _linksService;
    private readonly IDialogService? _dialogService;
    private readonly ISnackbarService? _snackbarService;

    [ObservableProperty]
    private ObservableCollection<LinkModel> _links = [];

    [ObservableProperty]
    private ObservableCollection<LinkModel> _filteredLinks = [];

    [ObservableProperty]
    private ObservableCollection<string> _categories = [];

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinksViewModel"/> class for design-time purposes.
    /// </summary>
    public LinksViewModel() : this(new DesignTimeLinksService(), new DesignTimeDialogService())
    {
    }

    public LinksViewModel(
        ILinksService linksService,
        IDialogService dialogService,
        ISnackbarService? snackbarService = null,
        ILogService? logger = null)
    {
        _linksService = linksService;
        _dialogService = dialogService;
        _snackbarService = snackbarService;
        _logger = logger;

        LoadLinks();
    }

    public void LoadLinks()
    {
        try
        {
            var loaded = _linksService.LoadLinks();
            Links = new ObservableCollection<LinkModel>(loaded);
            RefreshCategories();
            ApplyFilter();
            _logger?.LogInfo($"Quick Links initialized with {Links.Count} items.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error loading quick links in LinksViewModel.", ex);
            Links = new ObservableCollection<LinkModel>(_linksService.GetDefaultLinks());
            RefreshCategories();
            ApplyFilter();
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedCategoryChanged(string value)
    {
        ApplyFilter();
    }

    public void RefreshCategories()
    {
        var categoryList = new List<string> { "All" };

        var distinctCategories = Links
            .Where(l => !string.IsNullOrWhiteSpace(l.Category))
            .Select(l => l.Category!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c);

        categoryList.AddRange(distinctCategories);

        Categories = new ObservableCollection<string>(categoryList);

        if (!Categories.Contains(SelectedCategory, StringComparer.OrdinalIgnoreCase))
        {
            SelectedCategory = "All";
        }
    }

    public void ApplyFilter()
    {
        var query = SearchQuery?.Trim() ?? string.Empty;
        var selectedCat = SelectedCategory?.Trim() ?? "All";

        var filtered = Links.AsEnumerable();

        if (!string.Equals(selectedCat, "All", StringComparison.OrdinalIgnoreCase))
        {
            filtered = filtered.Where(l => string.Equals(l.Category, selectedCat, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(l =>
                l.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (l.Description != null && l.Description.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                (l.Category != null && l.Category.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                l.ResolvedPath.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        FilteredLinks = new ObservableCollection<LinkModel>(filtered);
    }

    [RelayCommand]
    private void SelectCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) category = "All";
        SelectedCategory = category;
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchQuery = string.Empty;
    }

    [RelayCommand]
    private void OnLinkClick(LinkModel? parameter)
    {
        if (parameter is null || string.IsNullOrEmpty(parameter.ResolvedPath))
        {
            return;
        }

        var success = _linksService.TryOpenLink(parameter.ResolvedPath);
        if (!success)
        {
            _snackbarService?.Show(
                "Launch Failed",
                $"Could not open '{parameter.DisplayName}'. Verify the URL or file path.",
                ControlAppearance.Danger,
                new SymbolIcon(SymbolRegular.ErrorCircle24),
                TimeSpan.FromSeconds(4));
        }
    }

    [RelayCommand]
    private void OnAddLinkClick()
    {
        var addLinkVm = new AddLinkDialogViewModel();
        var dialog = new AddEditLinkWindow(addLinkVm);

        if (dialog.ShowDialog() == true)
        {
            var newLink = new LinkModel
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = addLinkVm.DisplayName.Trim(),
                Key = addLinkVm.Path.Trim(),
                TargetUrl = addLinkVm.Path.Trim(),
                Category = string.IsNullOrWhiteSpace(addLinkVm.Category) ? "Custom" : addLinkVm.Category.Trim(),
                Description = string.IsNullOrWhiteSpace(addLinkVm.Description) ? null : addLinkVm.Description.Trim(),
                Icon = addLinkVm.SelectedIcon?.Symbol ?? SymbolRegular.Globe24,
                IsBuiltIn = false
            };

            Links.Add(newLink);
            _linksService.SaveLinks(Links);
            RefreshCategories();
            ApplyFilter();

            _snackbarService?.Show(
                "Quick Link Added",
                $"Added '{newLink.DisplayName}' to Quick Links.",
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(3));

            _logger?.LogInfo($"New quick link added: {newLink.DisplayName} ({newLink.ResolvedPath})");
        }
    }

    [RelayCommand]
    private void OnEditLinkClick(LinkModel? parameter)
    {
        if (parameter is null) return;

        var editLinkVm = new AddLinkDialogViewModel();
        editLinkVm.LoadFrom(parameter);
        var dialog = new AddEditLinkWindow(editLinkVm);

        if (dialog.ShowDialog() == true)
        {
            parameter.DisplayName = editLinkVm.DisplayName.Trim();
            parameter.TargetUrl = editLinkVm.Path.Trim();
            parameter.Key = editLinkVm.Path.Trim();
            parameter.Category = string.IsNullOrWhiteSpace(editLinkVm.Category) ? "Custom" : editLinkVm.Category.Trim();
            parameter.Description = string.IsNullOrWhiteSpace(editLinkVm.Description) ? null : editLinkVm.Description.Trim();
            parameter.Icon = editLinkVm.SelectedIcon?.Symbol ?? SymbolRegular.Globe24;

            _linksService.SaveLinks(Links);
            RefreshCategories();
            ApplyFilter();

            _snackbarService?.Show(
                "Quick Link Updated",
                $"Updated '{parameter.DisplayName}'.",
                ControlAppearance.Success,
                new SymbolIcon(SymbolRegular.CheckmarkCircle24),
                TimeSpan.FromSeconds(3));

            _logger?.LogInfo($"Quick link updated: {parameter.DisplayName}");
        }
    }

    [RelayCommand]
    private void OnDeleteLinkClick(LinkModel? parameter)
    {
        if (parameter is null) return;

        var result = System.Windows.MessageBox.Show(
            $"Are you sure you want to remove '{parameter.DisplayName}' from Quick Links?",
            "Remove Quick Link",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        Links.Remove(parameter);
        _linksService.SaveLinks(Links);
        RefreshCategories();
        ApplyFilter();

        _snackbarService?.Show(
            "Quick Link Removed",
            $"Removed '{parameter.DisplayName}'.",
            ControlAppearance.Info,
            new SymbolIcon(SymbolRegular.Delete24),
            TimeSpan.FromSeconds(3));

        _logger?.LogInfo($"Quick link deleted: {parameter.DisplayName}");
    }

    [RelayCommand]
    private void OnCopyLinkClick(LinkModel? parameter)
    {
        if (parameter is null || string.IsNullOrWhiteSpace(parameter.ResolvedPath)) return;

        try
        {
            Clipboard.SetText(parameter.ResolvedPath);
            _snackbarService?.Show(
                "Copied",
                $"Copied link for '{parameter.DisplayName}' to clipboard.",
                ControlAppearance.Info,
                new SymbolIcon(SymbolRegular.Copy24),
                TimeSpan.FromSeconds(2));
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error copying link to clipboard.", ex);
        }
    }

    [RelayCommand]
    private void OnResetDefaultsClick()
    {
        var result = System.Windows.MessageBox.Show(
            "Reset all Quick Links to default configuration? Custom links will be replaced.",
            "Reset Quick Links",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result != System.Windows.MessageBoxResult.Yes) return;

        var defaults = _linksService.GetDefaultLinks();
        Links = new ObservableCollection<LinkModel>(defaults);
        _linksService.SaveLinks(Links);
        RefreshCategories();
        ApplyFilter();

        _snackbarService?.Show(
            "Quick Links Reset",
            "Restored default quick links.",
            ControlAppearance.Info,
            new SymbolIcon(SymbolRegular.ArrowReset24),
            TimeSpan.FromSeconds(3));
    }

    public void OnNavigatedTo()
    {
        _logger?.LogInfo("LinksViewModel navigated to.");
    }

    public void OnNavigatedFrom()
    {
        _logger?.LogInfo("LinksViewModel navigated from.");
    }

    public Task OnNavigatedToAsync()
    {
        _logger?.LogInfo("LinksViewModel navigated to asynchronously.");
        return Task.CompletedTask;
    }

    public Task OnNavigatedFromAsync()
    {
        _logger?.LogInfo("LinksViewModel navigated from asynchronously.");
        return Task.CompletedTask;
    }
}
}