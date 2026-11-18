using AMFormsCST.Core;
using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.Notebook;
using AMFormsCST.Core.Utils;
using AMFormsCST.Desktop.BaseClasses;
using AMFormsCST.Desktop.Models;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.Types;
using AMFormsCST.Desktop.ViewModels.Dialogs;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using AMFormsCST.Desktop.Views.Dialogs;
using AMFormsCST.Desktop.Views.Pages.Tools;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Threading;
using Wpf.Ui;

namespace AMFormsCST.Desktop.ViewModels.Pages;

public partial class DashboardViewModel : ViewModel
{
    private readonly ILogService? _logger;
    private readonly IDebounceService _debounceService;

    private ManagedObservableCollection<NoteModel> _notes = new(() => new NoteModel(""));
    public ManagedObservableCollection<NoteModel> Notes => _notes;
    public NoteModel? SelectedNote => Notes.SelectedItem;

    private int _uiRefreshCounter;
    public int UiRefreshCounter
    {
        get => _uiRefreshCounter;
        private set => SetProperty(ref _uiRefreshCounter, value);
    }

    private readonly ISupportTool? _supportTool;
    private readonly IDialogService? _dialogService;
    private readonly IFileSystem? _fileSystem;
    private readonly ITemplateAssemblyService _templateAssemblyService;

    private NoteModel? _lastSelectedNote;
    private Models.Dealer? _lastSelectedDealer;
    private Models.Form? _lastSelectedForm;

    #region Composer & Assembly Properties
    [ObservableProperty]
    private CaseActionType _composerActionType = CaseActionType.LineAdjustments;

    [ObservableProperty]
    private string _composerFormName = string.Empty;

    [ObservableProperty]
    private bool _composerIsImpact = false;

    [ObservableProperty]
    private bool _composerIsCustomCopy = false;

    [ObservableProperty]
    private string _composerDetails = string.Empty;

    [ObservableProperty]
    private ObservableCollection<CaseDealAssignment> _composerDeals = [];

    [ObservableProperty]
    private ObservableCollection<string> _composerSelectedStores = [];

    public ObservableCollection<ComposerTagOption> AvailableComposerTags { get; } = [];

    [ObservableProperty]
    private string _newComposerTag = string.Empty;

    [ObservableProperty]
    private string _dealerRequestNotes = string.Empty;
    private bool _isSyncingDealerRequestNotes;

    [ObservableProperty]
    private string _newDealNumber = string.Empty;

    [ObservableProperty]
    private string _newDealPurpose = string.Empty;

    [ObservableProperty]
    private string _newLinkedCaseNumber = string.Empty;

    [ObservableProperty]
    private string _newCompanyCodes = string.Empty;
    private int _lastNewCompanyCodesLength = 0;

    [ObservableProperty]
    private bool _isHeaderDetailsExpanded = false;

    [ObservableProperty]
    private bool _isAdvancedStoresDealsExpanded = false;

    [ObservableProperty]
    private int _selectedOutputModeIndex = 0;

    [ObservableProperty]
    private string _assembledNoteText = string.Empty;

    [ObservableProperty]
    private bool _isAssembledOutputDirty = false;
    private bool _isProgrammaticAssembledUpdate = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingLogEntry))]
    [NotifyPropertyChangedFor(nameof(PostLogButtonText))]
    private CaseLogEntryItem? _editingLogEntry;

    public bool IsEditingLogEntry => EditingLogEntry != null;
    public string PostLogButtonText => EditingLogEntry != null ? "Update Work Item" : "Add to Work Log";

    public ObservableCollection<string> AvailablePublishForms { get; } = [];

    [ObservableProperty]
    private string? _selectedPublishForm;

    public bool IsPublishFormPickerVisible => SelectedOutputModeIndex == 2 && AvailablePublishForms.Count > 1;

    public IEnumerable<CaseActionType> AvailableActionTypes => Enum.GetValues(typeof(CaseActionType)).Cast<CaseActionType>();

    public string ComposerStoresSummary => ComposerSelectedStores.Count > 0
        ? string.Join(", ", ComposerSelectedStores)
        : "All Stores";

    public string ComposerDealsSummary => ComposerDeals.Count > 0
        ? string.Join(", ", ComposerDeals.Select(d => d.DisplayText))
        : "No Deals";

    partial void OnSelectedOutputModeIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsPublishFormPickerVisible));
        RefreshAssembledNote();
    }

    partial void OnSelectedPublishFormChanged(string? value)
    {
        if (SelectedOutputModeIndex == 2)
        {
            RefreshAssembledNote();
        }
    }

    partial void OnAssembledNoteTextChanged(string value)
    {
        if (!_isProgrammaticAssembledUpdate)
        {
            IsAssembledOutputDirty = true;
        }
    }
    #endregion

    public DashboardViewModel()
    {
        _debounceService = new DesignTimeDebounceService();
        _templateAssemblyService = new TemplateAssemblyService();

        var note1 = new NoteModel("x")
        {
            CaseNumber = "00123456",
            NotesXaml = XamlWriter.Save(new FlowDocument(new Paragraph(new Run("This is the first sample note.")))),
        };
        note1.LinkedCases.Add("00123457");
        note1.LinkedCases.Add("00123480");

        var note2 = new NoteModel("x")
        {
            CaseNumber = "00234567",
            NotesXaml = XamlWriter.Save(new FlowDocument(new Paragraph(new Run("This is the second sample note.")))),
        };

        var contact1 = new Models.Contact("x") { Name = "John Doe", Email = "john.doe@email.com", Phone = "888-555-1234", PhoneExtension = "1234", Role = "Finance Director", Notes = "Off on Fridays", IsPrimary = true };
        note1.Contacts.Add(contact1);
        note1.Contacts.FirstOrDefault(x => !x.IsBlank, note1.Contacts.First())?.Select();

        var dealer1 = new Models.Dealer { Name = "Sample Dealer", ServerCode = "SVR1" };
        var company1 = new Models.Company { Name = "Sample Company", CompanyCode = "C001" };
        dealer1.Companies.Add(company1);
        dealer1.Companies.FirstOrDefault(x => !x.IsBlank, dealer1.Companies.First())?.Select();
        note1.Dealers.Add(dealer1);
        note1.Dealers.FirstOrDefault(x => !x.IsBlank, note1.Dealers.First())?.Select();

        var form1 = new Models.Form(note1) { Name = "Sample Form 1", Notes = new() { Blocks = { new Paragraph(new Run("Notes for form 1")) } } };
        var testDeal1 = new Models.TestDeal { DealNumber = "D001", Purpose = "Test purpose 1" };
        form1.TestDeals.Add(testDeal1);
        form1.TestDeals.FirstOrDefault(x => !x.IsBlank, form1.TestDeals.First())?.Select();
        note1.Forms.Add(form1);
        note1.Forms.FirstOrDefault(x => !x.IsBlank, note1.Forms.First())?.Select();

        var sampleEntry1 = new CaseLogEntryItem
        {
            Action = CaseActionType.LineAdjustments,
            FormName = "F102 - Buyers Order",
            Details = "Shifted lines 14-16 down 2pt for dot matrix alignment",
            IsCustomCopy = true,
            IncludeInOutput = true,
            Timestamp = DateTime.Now
        };
        sampleEntry1.Stores.Add(new CaseStoreAssignment("C001", "Sample Company"));
        sampleEntry1.Deals.Add(new CaseDealAssignment("D001", "Retail Deal"));
        note1.LogEntries.Add(sampleEntry1);

        _notes = new ManagedObservableCollection<NoteModel>(() => new NoteModel("x"), [note1, note2]);
        note1.Select();
        RefreshAssembledNote();
    }

    public DashboardViewModel(
        ISupportTool supportTool,
        IDialogService dialogService,
        IFileSystem fileSystem,
        IDebounceService debounceService,
        ILogService? logger = null,
        ITemplateAssemblyService? templateAssemblyService = null)
    {
        _supportTool = supportTool ?? throw new ArgumentNullException(nameof(supportTool));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _fileSystem = fileSystem;
        _logger = logger;
        _templateAssemblyService = templateAssemblyService ?? new TemplateAssemblyService();
        _logger?.LogInfo("DashboardViewModel initialized.");
        _debounceService = debounceService;
        _debounceService.DebouncedElapsed += AutosaveTimerElapsed;

        void postCreationAction(NoteModel note)
        {
            note.Parent = this;
            note.PropertyChanged += OnNoteModelPropertyChanged;
        }

        if (_supportTool.Notebook.Notes.Count == 0)
        {
            var initialNote = new NoteModel(_supportTool.Settings.UserSettings.ExtSeparator, _logger);
            postCreationAction(initialNote);

            _notes = new ManagedObservableCollection<NoteModel>(
                () => new NoteModel(_supportTool.Settings.UserSettings.ExtSeparator, _logger),
                [initialNote],
                _logger,
                postCreationAction
            );
        }
        else
        {
            var notes = _supportTool.Notebook.Notes
                .Select(note =>
                {
                    var noteModel = new NoteModel(note, _supportTool.Settings.UserSettings.ExtSeparator, _logger);
                    postCreationAction(noteModel);
                    return noteModel;
                });

            _notes = new ManagedObservableCollection<NoteModel>(
                () => new NoteModel(_supportTool.Settings.UserSettings.ExtSeparator, _logger),
                notes.Where(c => !c.IsBlank),
                _logger,
                postCreationAction
            );
        }

        _notes.PropertyChanged += Notes_PropertyChanged;
        _notes.CollectionChanged += Notes_CollectionChanged;
        InitializeComposerTags();
        if (_notes.SelectedItem is not null)
        {
            Notes_PropertyChanged(this, new PropertyChangedEventArgs(nameof(Notes.SelectedItem)));
        }
    }

    private void Notes_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SelectPreviousNoteCommand.NotifyCanExecuteChanged();
        SelectNextNoteCommand.NotifyCanExecuteChanged();
    }

    public void NotifyDealerNavigationChanged()
    {
        SelectPreviousDealerCommand.NotifyCanExecuteChanged();
        SelectNextDealerCommand.NotifyCanExecuteChanged();
    }

    public void NotifyCompanyNavigationChanged()
    {
        SelectPreviousCompanyCommand.NotifyCanExecuteChanged();
        SelectNextCompanyCommand.NotifyCanExecuteChanged();
    }

    public void NotifyContactNavigationChanged()
    {
        SelectPreviousContactCommand.NotifyCanExecuteChanged();
        SelectNextContactCommand.NotifyCanExecuteChanged();
    }

    public void NotifyFormNavigationChanged()
    {
        SelectPreviousFormCommand.NotifyCanExecuteChanged();
        SelectNextFormCommand.NotifyCanExecuteChanged();
    }

    public void NotifyTestDealNavigationChanged()
    {
        SelectPreviousTestDealCommand.NotifyCanExecuteChanged();
        SelectNextTestDealCommand.NotifyCanExecuteChanged();
    }

    private void AutosaveTimerElapsed(object? sender, EventArgs e) => IO.SaveNotes([.. Notes.Select(n => n.CoreType).Cast<INote>()]);

    private void Notes_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ManagedObservableCollection<NoteModel>.SelectedItem)) return;

        if (_lastSelectedNote != null)
        {
            _lastSelectedNote.Dealers.PropertyChanged -= SelectedNote_Dealers_PropertyChanged;
            _lastSelectedNote.Contacts.PropertyChanged -= SelectedNote_Contacts_PropertyChanged;
            _lastSelectedNote.Forms.PropertyChanged -= SelectedNote_Forms_PropertyChanged;
            _lastSelectedNote.LogEntries.CollectionChanged -= SelectedNote_LogEntries_CollectionChanged;
            _lastSelectedNote.LinkedCases.CollectionChanged -= SelectedNote_LinkedCases_CollectionChanged;
            foreach (var entry in _lastSelectedNote.LogEntries)
            {
                entry.PropertyChanged -= LogEntry_PropertyChanged;
            }
        }

        OnPropertyChanged(nameof(SelectedNote));
        SelectPreviousNoteCommand.NotifyCanExecuteChanged();
        SelectNextNoteCommand.NotifyCanExecuteChanged();

        if (SelectedNote != null)
        {
            SelectedNote.Dealers.PropertyChanged += SelectedNote_Dealers_PropertyChanged;
            SelectedNote.Contacts.PropertyChanged += SelectedNote_Contacts_PropertyChanged;
            SelectedNote.Forms.PropertyChanged += SelectedNote_Forms_PropertyChanged;
            SelectedNote.LogEntries.CollectionChanged += SelectedNote_LogEntries_CollectionChanged;
            SelectedNote.LinkedCases.CollectionChanged += SelectedNote_LinkedCases_CollectionChanged;
            foreach (var entry in SelectedNote.LogEntries)
            {
                entry.PropertyChanged += LogEntry_PropertyChanged;
            }
        }

        // Manually trigger property changed for sub-collections to update CanExecute
        SelectedNote_Dealers_PropertyChanged(this, new PropertyChangedEventArgs(nameof(ManagedObservableCollection<Models.Dealer>.SelectedItem)));
        SelectedNote_Contacts_PropertyChanged(this, new PropertyChangedEventArgs(nameof(ManagedObservableCollection<Models.Contact>.SelectedItem)));
        SelectedNote_Forms_PropertyChanged(this, new PropertyChangedEventArgs(nameof(ManagedObservableCollection<Models.Form>.SelectedItem)));

        _lastSelectedNote = SelectedNote;
        _isSyncingDealerRequestNotes = true;
        try
        {
            DealerRequestNotes = SelectedNote?.CoreType?.NotesText ?? string.Empty;
        }
        finally
        {
            _isSyncingDealerRequestNotes = false;
        }
        UpdateTemplatesVM();
        RefreshAssembledNote();
    }

    partial void OnDealerRequestNotesChanged(string value)
    {
        if (_isSyncingDealerRequestNotes || SelectedNote == null) return;
        try
        {
            var doc = new FlowDocument();
            if (!string.IsNullOrEmpty(value))
            {
                foreach (var line in value.Replace("\r\n", "\n").Split('\n'))
                {
                    doc.Blocks.Add(new Paragraph(new Run(line)));
                }
            }
            SelectedNote.NotesXaml = XamlWriter.Save(doc);
            if (SelectedNote.CoreType != null)
            {
                SelectedNote.CoreType.NotesText = value ?? string.Empty;
            }
            RefreshAssembledNote();
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error updating dealer request notes.", ex);
        }
    }

    private void InitializeComposerTags()
    {
        AvailableComposerTags.Clear();
        var defaultTags = new[]
        {
            "#NewFormPublish",
            "#ProgrammingUpdate",
            "#LineAdjustments",
            "#Investigation",
            "#Republish"
        };

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in defaultTags)
        {
            if (seen.Add(tag))
            {
                AvailableComposerTags.Add(new ComposerTagOption(tag));
            }
        }

        var excludedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "#Closure", "#Solved", "#Unresolved", "#Resolved", "#Duplicate"
        };

        if (_supportTool?.Enforcer?.Templates != null)
        {
            foreach (var template in _supportTool.Enforcer.Templates)
            {
                foreach (var rawTag in template.Tags ?? [])
                {
                    var normalized = rawTag.Trim();
                    if (string.IsNullOrWhiteSpace(normalized)) continue;
                    if (!normalized.StartsWith('#')) normalized = "#" + normalized;
                    if (!excludedTags.Contains(normalized) && seen.Add(normalized))
                    {
                        AvailableComposerTags.Add(new ComposerTagOption(normalized));
                    }
                }
            }
        }
    }

    [RelayCommand]
    private void AddComposerTag()
    {
        if (string.IsNullOrWhiteSpace(NewComposerTag)) return;
        var clean = NewComposerTag.Trim();
        if (!clean.StartsWith('#')) clean = "#" + clean;

        var existing = AvailableComposerTags.FirstOrDefault(t => string.Equals(t.Tag, clean, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            existing.IsSelected = true;
        }
        else
        {
            AvailableComposerTags.Add(new ComposerTagOption(clean) { IsSelected = true });
        }

        NewComposerTag = string.Empty;
    }

    private void SelectedNote_LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (CaseLogEntryItem item in e.NewItems)
            {
                item.PropertyChanged += LogEntry_PropertyChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (CaseLogEntryItem item in e.OldItems)
            {
                item.PropertyChanged -= LogEntry_PropertyChanged;
            }
        }
        RefreshAssembledNote();
    }

    private void SelectedNote_LinkedCases_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshAssembledNote();
    }

    private void LogEntry_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        RefreshAssembledNote();
    }

    private void SelectedNote_Dealers_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ManagedObservableCollection<Models.Dealer>.SelectedItem)) return;

        if (_lastSelectedDealer != null)
        {
            _lastSelectedDealer.Companies.PropertyChanged -= SelectedDealer_Companies_PropertyChanged;
        }

        OnPropertyChanged(nameof(SelectedNote));
        SelectPreviousDealerCommand.NotifyCanExecuteChanged();
        SelectNextDealerCommand.NotifyCanExecuteChanged();

        if (SelectedNote?.SelectedDealer != null)
        {
            SelectedNote.SelectedDealer.Companies.PropertyChanged += SelectedDealer_Companies_PropertyChanged;
        }

        // Manually trigger property changed for sub-collection to update CanExecute
        SelectedDealer_Companies_PropertyChanged(this, new PropertyChangedEventArgs(nameof(ManagedObservableCollection<Models.Company>.SelectedItem)));

        _lastSelectedDealer = SelectedNote?.SelectedDealer;
        UpdateTemplatesVM();
    }

    private void SelectedDealer_Companies_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ManagedObservableCollection<Models.Company>.SelectedItem)) return;

        OnPropertyChanged(nameof(SelectedNote));
        SelectPreviousCompanyCommand.NotifyCanExecuteChanged();
        SelectNextCompanyCommand.NotifyCanExecuteChanged();
        UpdateTemplatesVM();
    }

    private void SelectedNote_Contacts_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ManagedObservableCollection<Models.Contact>.SelectedItem)) return;

        OnPropertyChanged(nameof(SelectedNote));
        SelectPreviousContactCommand.NotifyCanExecuteChanged();
        SelectNextContactCommand.NotifyCanExecuteChanged();
        UpdateTemplatesVM();
    }

    private void SelectedNote_Forms_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ManagedObservableCollection<Models.Form>.SelectedItem)) return;

        if (_lastSelectedForm != null)
        {
            _lastSelectedForm.TestDeals.PropertyChanged -= SelectedForm_TestDeals_PropertyChanged;
        }

        OnPropertyChanged(nameof(SelectedNote));
        SelectPreviousFormCommand.NotifyCanExecuteChanged();
        SelectNextFormCommand.NotifyCanExecuteChanged();

        if (SelectedNote?.SelectedForm != null)
        {
            SelectedNote.SelectedForm.TestDeals.PropertyChanged += SelectedForm_TestDeals_PropertyChanged;
        }

        // Manually trigger property changed for sub-collection to update CanExecute
        SelectedForm_TestDeals_PropertyChanged(this, new PropertyChangedEventArgs(nameof(ManagedObservableCollection<Models.TestDeal>.SelectedItem)));

        _lastSelectedForm = SelectedNote?.SelectedForm;
        UpdateTemplatesVM();
    }

    private void SelectedForm_TestDeals_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ManagedObservableCollection<Models.TestDeal>.SelectedItem)) return;

        OnPropertyChanged(nameof(SelectedNote));
        SelectPreviousTestDealCommand.NotifyCanExecuteChanged();
        SelectNextTestDealCommand.NotifyCanExecuteChanged();
        UpdateTemplatesVM();
    }

    private void OnNoteModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UiRefreshCounter++;
        _debounceService.ScheduleEvent();
        _logger?.LogDebug($"Note property changed: {e.PropertyName} on {sender}");
        UpdateTemplatesVM();
    }

    [RelayCommand(CanExecute = nameof(CanSelectPreviousNote))]
    private void SelectPreviousNote()
    {
        if (SelectedNote is null) return;
        var currentIndex = Notes.IndexOf(SelectedNote);
        Notes[currentIndex - 1].Select();
        if (_supportTool is not null)
            _supportTool.Notebook.Notes.SelectedItem = SelectedNote?.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectPreviousNote()
    {
        if (SelectedNote is null) return false;
        var currentIndex = Notes.IndexOf(SelectedNote);
        return currentIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanSelectNextNote))]
    private void SelectNextNote()
    {
        if (SelectedNote is null) return;
        var currentIndex = Notes.IndexOf(SelectedNote);
        Notes[currentIndex + 1].Select();
        if (_supportTool is not null)
            _supportTool.Notebook.Notes.SelectedItem = SelectedNote?.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectNextNote()
    {
        if (SelectedNote is null) return false;
        var currentIndex = Notes.IndexOf(SelectedNote);
        return currentIndex < Notes.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanSelectPreviousDealer))]
    private void SelectPreviousDealer()
    {
        if (SelectedNote?.SelectedDealer is null) return;
        var currentIndex = SelectedNote.Dealers.IndexOf(SelectedNote.SelectedDealer);
        SelectedNote.Dealers[currentIndex - 1].Select();
        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem is not null)
            _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem = SelectedNote.SelectedDealer.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectPreviousDealer()
    {
        if (SelectedNote?.SelectedDealer is null) return false;
        var currentIndex = SelectedNote.Dealers.IndexOf(SelectedNote.SelectedDealer);
        return currentIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanSelectNextDealer))]
    private void SelectNextDealer()
    {
        if (SelectedNote?.SelectedDealer is null) return;
        var currentIndex = SelectedNote.Dealers.IndexOf(SelectedNote.SelectedDealer);
        SelectedNote.Dealers[currentIndex + 1].Select();
        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem is not null)
            _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem = SelectedNote.SelectedDealer.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectNextDealer()
    {
        if (SelectedNote?.SelectedDealer is null) return false;
        var currentIndex = SelectedNote.Dealers.IndexOf(SelectedNote.SelectedDealer);
        return currentIndex < SelectedNote.Dealers.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanSelectPreviousCompany))]
    private void SelectPreviousCompany()
    {
        if (SelectedNote?.SelectedDealer?.SelectedCompany is null) return;
        var currentIndex = SelectedNote.SelectedDealer.Companies.IndexOf(SelectedNote.SelectedDealer.SelectedCompany);
        SelectedNote.SelectedDealer.Companies[currentIndex - 1].Select();
        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem?.Dealers.SelectedItem is not null)
            _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem.Companies.SelectedItem = 
                SelectedNote.SelectedDealer.SelectedCompany.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectPreviousCompany()
    {
        if (SelectedNote?.SelectedDealer?.SelectedCompany is null) return false;
        var currentIndex = SelectedNote.SelectedDealer.Companies.IndexOf(SelectedNote.SelectedDealer.SelectedCompany);
        return currentIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanSelectNextCompany))]
    private void SelectNextCompany()
    {
        if (SelectedNote?.SelectedDealer?.SelectedCompany is null) return;
        var currentIndex = SelectedNote.SelectedDealer.Companies.IndexOf(SelectedNote.SelectedDealer.SelectedCompany);
        SelectedNote.SelectedDealer.Companies[currentIndex + 1].Select();
        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem?.Dealers.SelectedItem is not null)
            _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem.Companies.SelectedItem = 
                SelectedNote.SelectedDealer.SelectedCompany.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectNextCompany()
    {
        if (SelectedNote?.SelectedDealer?.SelectedCompany is null) return false;
        var currentIndex = SelectedNote.SelectedDealer.Companies.IndexOf(SelectedNote.SelectedDealer.SelectedCompany);
        return currentIndex < SelectedNote.SelectedDealer.Companies.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanSelectPreviousContact))]
    private void SelectPreviousContact()
    {
        if (SelectedNote?.SelectedContact is null) return;
        var currentIndex = SelectedNote.Contacts.IndexOf(SelectedNote.SelectedContact);
        SelectedNote.Contacts[currentIndex - 1].Select();
        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem is not null)
            _supportTool.Notebook.Notes.SelectedItem.Contacts.SelectedItem = SelectedNote.SelectedContact.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectPreviousContact()
    {
        if (SelectedNote?.SelectedContact is null) return false;
        var currentIndex = SelectedNote.Contacts.IndexOf(SelectedNote.SelectedContact);
        return currentIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanSelectNextContact))]
    private void SelectNextContact()
    {
        if (SelectedNote?.SelectedContact is null) return;
        var currentIndex = SelectedNote.Contacts.IndexOf(SelectedNote.SelectedContact);
        SelectedNote.Contacts[currentIndex + 1].Select();
        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem is not null)
            _supportTool.Notebook.Notes.SelectedItem.Contacts.SelectedItem = SelectedNote.SelectedContact.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectNextContact()
    {
        if (SelectedNote?.SelectedContact is null) return false;
        var currentIndex = SelectedNote.Contacts.IndexOf(SelectedNote.SelectedContact);
        return currentIndex < SelectedNote.Contacts.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanSelectPreviousForm))]
    private void SelectPreviousForm()
    {
        if (SelectedNote?.SelectedForm is null) return;
        var currentIndex = SelectedNote.Forms.IndexOf(SelectedNote.SelectedForm);
        SelectedNote.Forms[currentIndex - 1].Select();
        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem is not null)
            _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem = SelectedNote.SelectedForm.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectPreviousForm()
    {
        if (SelectedNote?.SelectedForm is null) return false;
        var currentIndex = SelectedNote.Forms.IndexOf(SelectedNote.SelectedForm);
        return currentIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanSelectNextForm))]
    private void SelectNextForm()
    {
        if (SelectedNote?.SelectedForm is null) return;
        var currentIndex = SelectedNote.Forms.IndexOf(SelectedNote.SelectedForm);
        SelectedNote.Forms[currentIndex + 1].Select();
        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem is not null)
            _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem = SelectedNote.SelectedForm.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectNextForm()
    {
        if (SelectedNote?.SelectedForm is null) return false;
        var currentIndex = SelectedNote.Forms.IndexOf(SelectedNote.SelectedForm);
        return currentIndex < SelectedNote.Forms.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanSelectPreviousTestDeal))]
    private void SelectPreviousTestDeal()
    {
        if (SelectedNote?.SelectedForm?.SelectedTestDeal is null) return;
        var currentIndex = SelectedNote.SelectedForm.TestDeals.IndexOf(SelectedNote.SelectedForm.SelectedTestDeal);
        SelectedNote.SelectedForm.TestDeals[currentIndex - 1].Select();
        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem?.Forms.SelectedItem is not null)
            _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem.TestDeals.SelectedItem = 
                SelectedNote.SelectedForm.SelectedTestDeal.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectPreviousTestDeal()
    {
        if (SelectedNote?.SelectedForm?.SelectedTestDeal is null) return false;
        var currentIndex = SelectedNote.SelectedForm.TestDeals.IndexOf(SelectedNote.SelectedForm.SelectedTestDeal);
        return currentIndex > 0;
    }

    [RelayCommand(CanExecute = nameof(CanSelectNextTestDeal))]
    private void SelectNextTestDeal()
    {
        if (SelectedNote?.SelectedForm?.SelectedTestDeal is null) return;
        var currentIndex = SelectedNote.SelectedForm.TestDeals.IndexOf(SelectedNote.SelectedForm.SelectedTestDeal);
        SelectedNote.SelectedForm.TestDeals[currentIndex + 1].Select();
        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem?.Forms.SelectedItem is not null)
            _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem.TestDeals.SelectedItem = 
                SelectedNote.SelectedForm.SelectedTestDeal.CoreType;
        UpdateTemplatesVM();
    }

    private bool CanSelectNextTestDeal()
    {
        if (SelectedNote?.SelectedForm?.SelectedTestDeal is null) return false;
        var currentIndex = SelectedNote.SelectedForm.TestDeals.IndexOf(SelectedNote.SelectedForm.SelectedTestDeal);
        return currentIndex < SelectedNote.SelectedForm.TestDeals.Count - 1;
    }

    [RelayCommand]
    private void OnNoteClicked(Guid caseId)
    {
        try
        {
            if (Notes.SelectedItem is null || caseId == Notes.SelectedItem.Id) return;

            Notes.First(x => x.Id == caseId).Select();

            if (_supportTool is not null &&
                _supportTool.Notebook.Notes.SelectedItem?.Id != SelectedNote?.CoreType?.Id)
                _supportTool.Notebook.Notes.SelectedItem = SelectedNote?.CoreType;
            UpdateTemplatesVM();

            _logger?.LogInfo($"Note clicked and selected: {caseId}");

        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnNoteClicked.", ex);
        }
    }

    [RelayCommand]
    private void OnDealerClicked(Models.Dealer dealer)
    {
        try
        {
            if (SelectedNote is null ||
                SelectedNote.SelectedDealer is null ||
                dealer.Id == SelectedNote.SelectedDealer.Id) return;
            dealer.Select();
            if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem is not null &&
                _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem?.Id != SelectedNote.SelectedDealer.CoreType?.Id)
                _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem = SelectedNote.SelectedDealer.CoreType;
            UpdateTemplatesVM();

            _logger?.LogInfo($"Dealer clicked and selected: {dealer.Id}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnDealerClicked.", ex);
        }
    }

    [RelayCommand]
    private void OnCompanyClicked(Models.Company company)
    {
        try
        {
            if (SelectedNote is null ||
                SelectedNote.SelectedDealer is null ||
                SelectedNote.SelectedDealer.SelectedCompany is null ||
                company.Id == SelectedNote.SelectedDealer.SelectedCompany.Id) return;

            company.Select();

            if (_supportTool is not null && 
                _supportTool.Notebook.Notes.SelectedItem is not null && 
                _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem is not null &&
                (_supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem!.Companies.SelectedItem is null ||
                _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem!.Companies.SelectedItem?.Id != SelectedNote.SelectedDealer.SelectedCompany.CoreType?.Id))
                _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem!.Companies.SelectedItem = SelectedNote.SelectedDealer.SelectedCompany.CoreType;

            UpdateTemplatesVM();
            _logger?.LogInfo($"Company clicked and selected: {company.Id}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnCompanyClicked.", ex);
        }
    }

    [RelayCommand]
    private void OnContactClicked(Models.Contact contact)
    {
        try
        {
            if (SelectedNote is null ||
                SelectedNote.SelectedContact is null ||
                contact.Id == SelectedNote.SelectedContact.Id) return;

            contact.Select();
            if (_supportTool is not null &&
                _supportTool.Notebook.Notes.SelectedItem is not null &&
                _supportTool.Notebook.Notes.SelectedItem.Contacts.SelectedItem?.Id != SelectedNote.SelectedContact.CoreType?.Id)
                _supportTool.Notebook.Notes.SelectedItem.Contacts.SelectedItem = SelectedNote.SelectedContact.CoreType;
            UpdateTemplatesVM();
            _logger?.LogInfo($"Contact clicked and selected: {contact.Id}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnContactClicked.", ex);
        }
    }

    [RelayCommand]
    private void OnFormClicked(Models.Form form)
    {
        try
        {
            if (SelectedNote is null ||
                SelectedNote.SelectedForm is null ||
                form.Id == SelectedNote.SelectedForm.Id) return;

            form.Select();

            if (_supportTool is not null &&
                _supportTool.Notebook.Notes.SelectedItem is not null &&
                _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem?.Id != SelectedNote.SelectedForm.CoreType?.Id)
                _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem = SelectedNote.SelectedForm.CoreType;
            UpdateTemplatesVM();

            _logger?.LogInfo($"Form clicked and selected: {form.Id}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnFormClicked.", ex);
        }
    }

    [RelayCommand]
    private void OnDealClicked(Models.TestDeal deal)
    {
        try
        {
            if (SelectedNote is null ||
                SelectedNote.SelectedForm is null ||
                SelectedNote.SelectedForm.SelectedTestDeal is null ||
                deal.Id == SelectedNote.SelectedForm.SelectedTestDeal.Id) return;

            deal.Select();

            if (_supportTool is not null &&
                _supportTool.Notebook.Notes.SelectedItem is not null &&
                _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem is not null &&
                _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem!.TestDeals.SelectedItem?.Id != SelectedNote.SelectedForm.SelectedTestDeal.CoreType?.Id)
                _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem!.TestDeals.SelectedItem = SelectedNote.SelectedForm.SelectedTestDeal.CoreType;

            UpdateTemplatesVM(); 
            _logger?.LogInfo($"Deal clicked and selected: {deal.Id}");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnDealClicked.", ex);
        }
    }

    [RelayCommand]
    private void OnDeleteItemClicked(ManagedObservableCollectionItem itemToDelete)
    {
        try
        {
            if (itemToDelete == null) return;

            switch (itemToDelete)
            {
                case NoteModel noteToDelete:
                    Notes.Remove(noteToDelete);
                    _supportTool.Notebook.Notes.SelectedItem = SelectedNote.CoreType;
                    _logger?.LogInfo($"Note deleted: {noteToDelete.Id}");
                    break;

                case Models.Dealer dealerToDelete when SelectedNote is not null:
                    SelectedNote.Dealers.Remove(dealerToDelete);
                    _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem = SelectedNote.SelectedDealer.CoreType;
                    _logger?.LogInfo($"Dealer deleted: {dealerToDelete.Id}");
                    break;

                case Models.Company companyToDelete when
                SelectedNote is not null &&
                SelectedNote.SelectedDealer is not null:
                    SelectedNote.SelectedDealer.Companies.Remove(companyToDelete);
                    _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem.Companies.SelectedItem = SelectedNote.SelectedDealer.SelectedCompany.CoreType;
                    _logger?.LogInfo($"Company deleted: {companyToDelete.Id}");
                    break;

                case Models.Contact contactToDelete when SelectedNote is not null:
                    SelectedNote.Contacts.Remove(contactToDelete);
                    _supportTool.Notebook.Notes.SelectedItem.Contacts.SelectedItem = SelectedNote.SelectedContact.CoreType;
                    _logger?.LogInfo($"Contact deleted: {contactToDelete.Id}");
                    break;

                case Models.Form formToDelete when SelectedNote is not null:
                    SelectedNote.Forms.Remove(formToDelete);
                    _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem = SelectedNote.SelectedForm.CoreType;
                    _logger?.LogInfo($"Form deleted: {formToDelete.Id}");
                    break;

                case Models.TestDeal dealToDelete when
                SelectedNote?.SelectedForm is not null:
                    SelectedNote.SelectedForm.TestDeals.Remove(dealToDelete);
                    _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem.TestDeals.SelectedItem = SelectedNote.SelectedForm.SelectedTestDeal.CoreType;
                    _logger?.LogInfo($"TestDeal deleted: {dealToDelete.Id}");
                    break;
            }
            UpdateTemplatesVM();
            _debounceService.ScheduleEvent();
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in OnDeleteItemClicked.", ex);
        }
    }
    private TemplatesViewModel? _templatesvm;
    public TemplatesViewModel? ActiveTemplatesViewModel => _templatesvm;

    [RelayCommand]
    private void OpenTemplateDialog()
    {
        try
        {
            TemplatesViewModel vm;
            try
            {
                vm = App.GetRequiredService<TemplatesViewModel>();
            }
            catch
            {
                vm = new TemplatesViewModel(_supportTool, _fileSystem);
            }

            _templatesvm = vm;
            var page = new TemplatesPage(vm);
            var dialog = new PageHostDialog(page, "Templates");
            dialog.Show();
            _logger?.LogInfo("Opened Template Dialog.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error opening Template Dialog.", ex);
        }
    }

    private DispatcherTimer? _updateTemplatesTimer;

    private void UpdateTemplatesVM(bool immediate = false)
    {
        if (Application.Current == null || immediate)
        {
            _updateTemplatesTimer?.Stop();
            try
            {
                _templatesvm?.Refresh();
                _logger?.LogDebug("TemplatesViewModel updated immediately.");
            }
            catch (Exception ex)
            {
                _logger?.LogError("Error updating TemplatesViewModel.", ex);
            }
            return;
        }

        if (_updateTemplatesTimer == null)
        {
            _updateTemplatesTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(75)
            };
            _updateTemplatesTimer.Tick += (s, e) =>
            {
                _updateTemplatesTimer.Stop();
                try
                {
                    _templatesvm?.Refresh();
                    _logger?.LogDebug("TemplatesViewModel updated.");
                }
                catch (Exception ex)
                {
                    _logger?.LogError("Error updating TemplatesViewModel.", ex);
                }
            };
        }

        _updateTemplatesTimer.Stop();
        _updateTemplatesTimer.Start();
    }

    [RelayCommand]
    private void OpenCodeSnippetDialog()
    {
        try
        {
            var vm = new CodeSnippetsViewModel(_supportTool);
            var page = new CodeSnippetsPage(vm);
            var dialog = new PageHostDialog(page, "Code Snippets");
            dialog.Show();
            _logger?.LogInfo("Opened Code Snippet Dialog.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error opening Code Snippet Dialog.", ex);
        }
    }



    [RelayCommand]
    private void OpenFormgenUtilsDialog()
    {
        try
        {
            var vm = new FormgenUtilitiesViewModel(_supportTool, _dialogService, _fileSystem);
            var navigationService = App.GetRequiredService<INavigationService>();
            var page = new FormgenUtilitiesPage(vm, navigationService, _logger);
            var dialog = new PageHostDialog(page, "Formgen Utilities");
            dialog.Show();
            _logger?.LogInfo("Opened Formgen Utilities Dialog.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error opening Formgen Utilities Dialog.", ex);
        }
    }

    [RelayCommand]
    private void OpenFormNameGeneratorDialog()
    {
        try
        {
            var vm = new FormNameGeneratorViewModel(_supportTool);
            var page = new FormNameGeneratorPage(vm);
            var dialog = new PageHostDialog(page, "Form Name Generator", true);
            dialog.Show();
            dialog.Closed += FormNameDialogClosed;
            _logger?.LogInfo("Opened Form Name Generator Dialog.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error opening Form Name Generator Dialog.", ex);
        }
    }

    private void FormNameDialogClosed(object? sender, EventArgs e)
    {
        try
        {
            if (sender is null) return;

            var dialog = (PageHostDialog)sender;
            if (dialog.DataContext is not PageHostDialogViewModel outerVm) return;
            if (outerVm.HostedPageViewModel is not FormNameGeneratorViewModel vm) return;

            var name = vm.Form.FileName ?? string.Empty;

            if (dialog.ConfirmSelected)
            {
                ComposerFormName = name;
                if (SelectedNote?.SelectedForm?.IsBlank == true)
                {
                    SelectedNote.SelectedForm.Name = name;
                }
                else if (SelectedNote is not null)
                {
                    SelectedNote.Forms.Last().Name = name ?? string.Empty;
                }
            }
            dialog.Closed -= FormNameDialogClosed;
            _logger?.LogInfo("Form Name Dialog closed.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in FormNameDialogClosed.", ex);
        }
    }

    #region Composer & Output Commands
    [RelayCommand]
    private void PostLogEntry()
    {
        if (SelectedNote == null) return;

        var stores = new List<CaseStoreAssignment>();
        if (SelectedNote.SelectedDealer?.Companies != null)
        {
            foreach (var c in SelectedNote.SelectedDealer.Companies.Where(co => !co.IsBlank))
            {
                if (ComposerSelectedStores.Contains(c.CompanyCode))
                {
                    stores.Add(new CaseStoreAssignment(c.CompanyCode, c.Name));
                }
            }
        }
        foreach (var sc in ComposerSelectedStores)
        {
            if (!stores.Any(s => s.CompanyCode == sc))
            {
                stores.Add(new CaseStoreAssignment(sc, sc));
            }
        }

        var selectedTags = AvailableComposerTags
            .Where(t => t.IsSelected)
            .Select(t => t.Tag)
            .ToList();

        var inferredAction = InferActionFromTags(selectedTags, ComposerActionType);

        CaseLogEntryItem entry;
        if (EditingLogEntry != null)
        {
            entry = EditingLogEntry;
            entry.Action = inferredAction;
            entry.FormName = ComposerFormName?.Trim() ?? string.Empty;
            entry.IsCustomCopy = ComposerIsCustomCopy;
            entry.Details = ComposerDetails?.Trim() ?? string.Empty;
            entry.Stores.Clear();
            foreach (var s in stores) entry.Stores.Add(s);
            entry.Deals.Clear();
            foreach (var d in ComposerDeals) entry.Deals.Add(d.Clone());
            entry.Tags.Clear();
            foreach (var tag in selectedTags) entry.Tags.Add(tag);
            entry.UpdateCore();
            EditingLogEntry = null;
        }
        else
        {
            entry = new CaseLogEntryItem(_logger)
            {
                Parent = SelectedNote,
                Timestamp = DateTime.Now,
                Action = inferredAction,
                FormName = ComposerFormName?.Trim() ?? string.Empty,
                IsCustomCopy = ComposerIsCustomCopy,
                Details = ComposerDetails?.Trim() ?? string.Empty,
                IncludeInOutput = true,
                IsHistorical = false
            };
            foreach (var s in stores) entry.Stores.Add(s);
            foreach (var d in ComposerDeals) entry.Deals.Add(d.Clone());
            foreach (var tag in selectedTags) entry.Tags.Add(tag);

            SelectedNote.LogEntries.Add(entry);
            entry.UpdateCore();
        }

        ClearComposerInputs();
        RefreshAssembledNote();
        _logger?.LogInfo($"Posted work log entry: {entry.ActionDisplayName} on {entry.FormName}");
    }

    private bool HasUnpostedComposerData()
    {
        return !string.IsNullOrWhiteSpace(ComposerFormName)
            || !string.IsNullOrWhiteSpace(ComposerDetails)
            || ComposerDeals.Count > 0
            || ComposerSelectedStores.Count > 0
            || AvailableComposerTags.Any(t => t.IsSelected);
    }

    private void ClearComposerInputs()
    {
        ComposerFormName = string.Empty;
        ComposerDetails = string.Empty;
        ComposerIsCustomCopy = false;
        ComposerDeals.Clear();
        ComposerSelectedStores.Clear();
        foreach (var tag in AvailableComposerTags) tag.IsSelected = false;
        OnPropertyChanged(nameof(ComposerStoresSummary));
        OnPropertyChanged(nameof(ComposerDealsSummary));
    }

    [RelayCommand]
    private void EditLogEntry(CaseLogEntryItem? entry)
    {
        if (entry == null) return;

        if (HasUnpostedComposerData() && Application.Current != null)
        {
            var result = MessageBox.Show(
                "The Work Item Composer currently has unposted data. Discard it and load this timeline entry for editing?",
                "Unposted Work Item Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;
        }

        ClearComposerInputs();
        EditingLogEntry = entry;
        ComposerActionType = entry.Action;
        ComposerFormName = entry.FormName;
        ComposerIsCustomCopy = entry.IsCustomCopy;
        ComposerDetails = entry.Details;

        foreach (var store in entry.Stores)
        {
            if (!string.IsNullOrWhiteSpace(store.CompanyCode) && !ComposerSelectedStores.Contains(store.CompanyCode))
            {
                ComposerSelectedStores.Add(store.CompanyCode);
            }
        }
        foreach (var deal in entry.Deals)
        {
            ComposerDeals.Add(deal.Clone());
        }
        foreach (var tag in entry.Tags)
        {
            var existing = AvailableComposerTags.FirstOrDefault(t => string.Equals(t.Tag, tag, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                existing.IsSelected = true;
            }
            else
            {
                AvailableComposerTags.Add(new ComposerTagOption(tag) { IsSelected = true });
            }
        }

        OnPropertyChanged(nameof(ComposerStoresSummary));
        OnPropertyChanged(nameof(ComposerDealsSummary));
    }

    [RelayCommand]
    private void CancelEditLogEntry()
    {
        EditingLogEntry = null;
        ClearComposerInputs();
    }

    private static CaseActionType InferActionFromTags(List<string> tags, CaseActionType fallback)
    {
        foreach (var tag in tags)
        {
            var lower = tag.ToLowerInvariant();
            if (lower.Contains("newform")) return CaseActionType.NewFormPublish;
            if (lower.Contains("programming")) return CaseActionType.ProgrammingUpdate;
            if (lower.Contains("lineadjust")) return CaseActionType.LineAdjustments;
            if (lower.Contains("investigat")) return CaseActionType.Investigation;
            if (lower.Contains("republish")) return CaseActionType.Republish;
        }
        return fallback;
    }

    [RelayCommand]
    private void AddComposerDeal()
    {
        if (string.IsNullOrWhiteSpace(NewDealNumber)) return;
        ComposerDeals.Add(new CaseDealAssignment(NewDealNumber.Trim(), NewDealPurpose?.Trim() ?? string.Empty));
        NewDealNumber = string.Empty;
        NewDealPurpose = string.Empty;
        OnPropertyChanged(nameof(ComposerDealsSummary));
    }

    [RelayCommand]
    private void RemoveComposerDeal(CaseDealAssignment? deal)
    {
        if (deal != null && ComposerDeals.Remove(deal))
        {
            OnPropertyChanged(nameof(ComposerDealsSummary));
        }
    }

    [RelayCommand]
    private void ToggleComposerStore(string? storeCode)
    {
        if (string.IsNullOrWhiteSpace(storeCode)) return;
        if (ComposerSelectedStores.Contains(storeCode))
            ComposerSelectedStores.Remove(storeCode);
        else
            ComposerSelectedStores.Add(storeCode);
        OnPropertyChanged(nameof(ComposerStoresSummary));
    }

    [RelayCommand]
    private void AddLinkedCase()
    {
        if (SelectedNote == null || string.IsNullOrWhiteSpace(NewLinkedCaseNumber)) return;
        var clean = NewLinkedCaseNumber.Trim();
        if (!SelectedNote.LinkedCases.Contains(clean))
        {
            SelectedNote.LinkedCases.Add(clean);
            SelectedNote.UpdateCore();
            RefreshAssembledNote();
        }
        NewLinkedCaseNumber = string.Empty;
    }

    [RelayCommand]
    private void RemoveLinkedCase(string? caseNumber)
    {
        if (SelectedNote == null || string.IsNullOrWhiteSpace(caseNumber)) return;
        if (SelectedNote.LinkedCases.Remove(caseNumber))
        {
            SelectedNote.UpdateCore();
            RefreshAssembledNote();
        }
    }

    private bool _isProcessingCompanyCodes;

    partial void OnNewCompanyCodesChanged(string value)
    {
        if (_isProcessingCompanyCodes || SelectedNote?.SelectedDealer == null)
        {
            _lastNewCompanyCodesLength = value?.Length ?? 0;
            return;
        }

        if (!string.IsNullOrEmpty(value) && (value.Contains(',') || value.Contains(';')))
        {
            _isProcessingCompanyCodes = true;
            try
            {
                var isPaste = value.Length - _lastNewCompanyCodesLength > 2;
                var endsWithSeparator = value.EndsWith(',') || value.EndsWith(';');

                if (isPaste || endsWithSeparator)
                {
                    AddCompanyCodesFromInput(value);
                    NewCompanyCodes = string.Empty;
                }
                else
                {
                    var lastSep = Math.Max(value.LastIndexOf(','), value.LastIndexOf(';'));
                    var completedPart = value[..lastSep];
                    var remainder = value[(lastSep + 1)..].TrimStart();
                    AddCompanyCodesFromInput(completedPart);
                    NewCompanyCodes = remainder;
                }
            }
            finally
            {
                _isProcessingCompanyCodes = false;
            }
        }

        _lastNewCompanyCodesLength = NewCompanyCodes?.Length ?? 0;
    }

    [RelayCommand]
    private void AddCompanies()
    {
        if (SelectedNote?.SelectedDealer == null || string.IsNullOrWhiteSpace(NewCompanyCodes)) return;

        _isProcessingCompanyCodes = true;
        try
        {
            AddCompanyCodesFromInput(NewCompanyCodes);
            NewCompanyCodes = string.Empty;
            _lastNewCompanyCodesLength = 0;
        }
        finally
        {
            _isProcessingCompanyCodes = false;
        }
    }

    private void AddCompanyCodesFromInput(string input)
    {
        if (SelectedNote?.SelectedDealer == null || string.IsNullOrWhiteSpace(input)) return;

        var codes = input
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (codes.Count == 0) return;

        var dealer = SelectedNote.SelectedDealer;
        var addedAny = false;

        foreach (var code in codes)
        {
            if (dealer.Companies.Any(c => string.Equals(c.CompanyCode?.Trim(), code, StringComparison.OrdinalIgnoreCase)))
                continue;

            var blankCompany = dealer.Companies.FirstOrDefault(c => c.IsBlank);
            if (blankCompany != null)
            {
                blankCompany.CompanyCode = code;
                blankCompany.Notable = true;
            }
            else
            {
                var newCompany = new Models.Company(_logger)
                {
                    CompanyCode = code,
                    Notable = true,
                    Parent = dealer
                };
                dealer.Companies.Add(newCompany);
            }
            addedAny = true;
        }

        if (addedAny)
        {
            dealer.UpdateCore();
            SelectedNote.UpdateCore();
            RefreshAssembledNote();
        }
    }

    [RelayCommand]
    private void CopyAssembledNote()
    {
        if (!string.IsNullOrEmpty(AssembledNoteText))
        {
            Clipboard.SetText(AssembledNoteText);
            _logger?.LogInfo("Assembled note copied to clipboard.");
        }
    }

    [RelayCommand]
    private void SendToSalesforce()
    {
        if (!string.IsNullOrEmpty(AssembledNoteText))
        {
            Clipboard.SetText(AssembledNoteText);
            _logger?.LogInfo("Assembled note copied for Salesforce.");
        }
    }

    [RelayCommand]
    private void ToggleHeaderDetails()
    {
        IsHeaderDetailsExpanded = !IsHeaderDetailsExpanded;
    }

    [RelayCommand]
    private void ToggleAdvancedStoresDeals()
    {
        IsAdvancedStoresDealsExpanded = !IsAdvancedStoresDealsExpanded;
    }

    [RelayCommand]
    private void SetOutputMode(string? modeIndexStr)
    {
        if (int.TryParse(modeIndexStr, out var mode))
        {
            SelectedOutputModeIndex = mode;
            RefreshAssembledNote();
        }
    }

    [RelayCommand]
    private void SetActionType(string? actionTypeStr)
    {
        if (Enum.TryParse<CaseActionType>(actionTypeStr, out var action))
        {
            ComposerActionType = action;
        }
    }

    [RelayCommand]
    private void ForceRefreshAssembledNote() => RefreshAssembledNote(force: true);

    public void RefreshAssembledNote() => RefreshAssembledNote(force: false);

    private void RefreshAssembledNote(bool force)
    {
        if (SelectedNote == null)
        {
            SetAssembledNoteProgrammatically(string.Empty);
            UpdateAvailablePublishForms([]);
            return;
        }

        var noteCore = SelectedNote.CoreType;
        if (noteCore == null)
        {
            SetAssembledNoteProgrammatically(string.Empty);
            UpdateAvailablePublishForms([]);
            return;
        }

        if (!force && IsAssembledOutputDirty && Application.Current != null)
        {
            var result = MessageBox.Show(
                "You have manual edits in the Assembled Output box. Overwrite them with the newly assembled text?",
                "Overwrite Manual Edits?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var includedEntries = noteCore.LogEntries.Where(e => e.IncludeInOutput).ToList();
        UpdateAvailablePublishForms(includedEntries);

        var activeEntry = includedEntries.LastOrDefault();
        var formEntries = !string.IsNullOrWhiteSpace(SelectedPublishForm)
            ? includedEntries.Where(e => string.Equals(e.FormName, SelectedPublishForm, StringComparison.OrdinalIgnoreCase)).ToList()
            : includedEntries;
        var primaryCaseNumber = !string.IsNullOrWhiteSpace(noteCore.CaseText) ? noteCore.CaseText : "00000000";

        try
        {
            var userSettings = _supportTool?.Settings?.UserSettings;
            var templates = _supportTool?.Enforcer?.Templates;
            var assembled = SelectedOutputModeIndex switch
            {
                0 => _templateAssemblyService.AssembleInternalNote(noteCore, includedEntries, userSettings: userSettings, actionPartials: templates),
                1 => _templateAssemblyService.AssembleDealerEmail(noteCore, includedEntries, userSettings: userSettings, actionPartials: templates),
                2 => _templateAssemblyService.AssemblePublishNote(noteCore, activeEntry, formEntries),
                3 => _templateAssemblyService.AssembleClosureRecap(noteCore, userSettings: userSettings),
                4 => _templateAssemblyService.AssembleLinkedCasesNote(noteCore, primaryCaseNumber),
                _ => _templateAssemblyService.AssembleInternalNote(noteCore, includedEntries, userSettings: userSettings, actionPartials: templates)
            };
            SetAssembledNoteProgrammatically(assembled);
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error assembling note text.", ex);
            SetAssembledNoteProgrammatically($"Error generating preview: {ex.Message}");
        }
    }

    private void SetAssembledNoteProgrammatically(string text)
    {
        _isProgrammaticAssembledUpdate = true;
        try
        {
            AssembledNoteText = text;
            IsAssembledOutputDirty = false;
        }
        finally
        {
            _isProgrammaticAssembledUpdate = false;
        }
    }

    private void UpdateAvailablePublishForms(List<ICaseLogEntry> includedEntries)
    {
        var forms = includedEntries
            .Select(e => e.FormName?.Trim())
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        AvailablePublishForms.Clear();
        foreach (var f in forms)
        {
            AvailablePublishForms.Add(f);
        }

        if (string.IsNullOrWhiteSpace(SelectedPublishForm) || !forms.Contains(SelectedPublishForm, StringComparer.OrdinalIgnoreCase))
        {
            SelectedPublishForm = forms.LastOrDefault();
        }
        OnPropertyChanged(nameof(IsPublishFormPickerVisible));
    }

    [RelayCommand]
    private void OpenAutoCloseDialog()
    {
        if (SelectedNote == null) return;

        var dialog = new AutoCloseDialog(
            SelectedNote,
            _templateAssemblyService,
            _supportTool?.Settings?.UserSettings,
            _supportTool?.Enforcer?.Templates);

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.ResultClosureComment))
        {
            SelectedOutputModeIndex = 3;
            SetAssembledNoteProgrammatically(dialog.ResultClosureComment);
        }
    }
    #endregion

    [RelayCommand]
    private void LoadCase()
    {
        var note = ParseCaseText(Clipboard.GetText());
        LoadCaseFromNote(note);
        _logger?.LogInfo("LoadCase command executed.");
    }

    public NoteModel? LoadCaseFromNote(NoteModel note)
    {
        var targetNote = ApplyParsedNote(note);
        targetNote.Select();

        if (targetNote.SelectedDealer == null)
        {
            var d = targetNote.Dealers.FirstOrDefault(d => !d.IsBlank) ?? targetNote.Dealers.FirstOrDefault();
            d?.Select();
        }
        if (targetNote.SelectedDealer?.SelectedCompany == null)
        {
            var c = targetNote.SelectedDealer?.Companies.FirstOrDefault(comp => !comp.IsBlank) ?? targetNote.SelectedDealer?.Companies.FirstOrDefault();
            c?.Select();
        }
        if (targetNote.SelectedContact == null)
        {
            var cont = targetNote.Contacts.FirstOrDefault(c => !c.IsBlank) ?? targetNote.Contacts.FirstOrDefault();
            cont?.Select();
        }
        if (targetNote.SelectedForm == null)
        {
            var f = targetNote.Forms.FirstOrDefault(form => !form.IsBlank) ?? targetNote.Forms.FirstOrDefault();
            f?.Select();
        }

        if (_supportTool is not null)
        {
            if (targetNote.CoreType is not null && !_supportTool.Notebook.Notes.Contains(targetNote.CoreType))
            {
                _supportTool.Notebook.Notes.Add(targetNote.CoreType);
            }
            _supportTool.Notebook.Notes.SelectedItem = targetNote.CoreType;

            if (_supportTool.Notebook.Notes.SelectedItem is not null && targetNote.SelectedDealer is not null)
            {
                if (targetNote.SelectedDealer.CoreType is not null && !_supportTool.Notebook.Notes.SelectedItem.Dealers.Contains(targetNote.SelectedDealer.CoreType))
                {
                    _supportTool.Notebook.Notes.SelectedItem.Dealers.Add(targetNote.SelectedDealer.CoreType);
                }
                _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem = targetNote.SelectedDealer.CoreType;

                if (targetNote.SelectedDealer.SelectedCompany is not null)
                {
                    if (targetNote.SelectedDealer.SelectedCompany.CoreType is not null && !_supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem.Companies.Contains(targetNote.SelectedDealer.SelectedCompany.CoreType))
                    {
                        _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem.Companies.Add(targetNote.SelectedDealer.SelectedCompany.CoreType);
                    }
                    _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem.Companies.SelectedItem = targetNote.SelectedDealer.SelectedCompany.CoreType;
                }
            }

            if (_supportTool.Notebook.Notes.SelectedItem is not null && targetNote.SelectedContact is not null)
            {
                if (targetNote.SelectedContact.CoreType is not null && !_supportTool.Notebook.Notes.SelectedItem.Contacts.Contains(targetNote.SelectedContact.CoreType))
                {
                    _supportTool.Notebook.Notes.SelectedItem.Contacts.Add(targetNote.SelectedContact.CoreType);
                }
                _supportTool.Notebook.Notes.SelectedItem.Contacts.SelectedItem = targetNote.SelectedContact.CoreType;
            }

            if (_supportTool.Notebook.Notes.SelectedItem is not null && targetNote.SelectedForm is not null)
            {
                if (targetNote.SelectedForm.CoreType is not null && !_supportTool.Notebook.Notes.SelectedItem.Forms.Contains(targetNote.SelectedForm.CoreType))
                {
                    _supportTool.Notebook.Notes.SelectedItem.Forms.Add(targetNote.SelectedForm.CoreType);
                }
                _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem = targetNote.SelectedForm.CoreType;
            }

            if (_supportTool.Notebook.Notes.SelectedItem is not null && targetNote.SelectedForm?.SelectedTestDeal is not null)
            {
                if (_supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem is not null &&
                    targetNote.SelectedForm.SelectedTestDeal.CoreType is not null &&
                    !_supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem.TestDeals.Contains(targetNote.SelectedForm.SelectedTestDeal.CoreType))
                {
                    _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem.TestDeals.Add(targetNote.SelectedForm.SelectedTestDeal.CoreType);
                }
                if (_supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem is not null)
                {
                    _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem.TestDeals.SelectedItem = targetNote.SelectedForm.SelectedTestDeal.CoreType;
                }
            }
        }

        UiRefreshCounter++;
        UpdateTemplatesVM(immediate: true);
        RefreshAssembledNote();
        return SelectedNote;
    }

    // Add to your DashboardViewModel
    [RelayCommand]
    private void FormNotesChanged()
    {
        SelectedNote?.SelectedForm?.UpdateCore();
        UpdateTemplatesVM();
    }

    [RelayCommand]
    private void GeneralNotesChanged()
    {
        SelectedNote?.UpdateCore();
        UpdateTemplatesVM();
    }

    public static bool IsMatchingLabel(string? text, params string[] labels)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        var t = text.Trim().TrimEnd(':');
        foreach (var label in labels)
        {
            if (t.Equals(label, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public NoteModel ParseCaseText(string caseText)
    {
        var extSep = _supportTool?.Settings?.UserSettings?.ExtSeparator ?? " ";
        var note = new NoteModel(extSep, _logger);
        var textLines = (caseText ?? string.Empty).Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        try
        {
            var lines = textLines.ToList();

            string GetValueAfter(string key)
            {
                var index = lines.FindIndex(l =>
                    l.Trim().Equals(key, StringComparison.OrdinalIgnoreCase) ||
                    l.Trim().Equals(key + ":", StringComparison.OrdinalIgnoreCase));
                if (index != -1 && index + 1 < lines.Count)
                    return lines[index + 1].Trim();

                var inline = lines.FirstOrDefault(l => l.Trim().StartsWith(key + ":", StringComparison.OrdinalIgnoreCase));
                if (inline != null)
                    return string.Join(":", inline.Split(':').Skip(1)).Trim();

                return string.Empty;
            }

            string GetValueFromKeyValue(string key, List<string> section)
            {
                var line = section.FirstOrDefault(l => l.Trim().StartsWith(key, StringComparison.OrdinalIgnoreCase))
                        ?? lines.FirstOrDefault(l => l.Trim().StartsWith(key, StringComparison.OrdinalIgnoreCase));
                return line != null ? string.Join(":", line.Split(':').Skip(1)).Trim() : string.Empty;
            }

            var submittedValuesIdx = lines.FindIndex(l => l.Trim().Equals("Submitted Values:", StringComparison.OrdinalIgnoreCase));
            var serverValuesIdx = lines.FindIndex(l => l.Trim().Equals("Server-Provided Values:", StringComparison.OrdinalIgnoreCase));

            var submittedValues = submittedValuesIdx != -1
                ? lines.Skip(submittedValuesIdx + 1).Take(serverValuesIdx != -1 ? serverValuesIdx - submittedValuesIdx - 1 : lines.Count).ToList()
                : [];

            var serverValues = serverValuesIdx != -1
                ? lines.Skip(serverValuesIdx + 1).ToList()
                : [];

            note.CaseNumber = GetValueAfter("Case Number");
            var subject = GetValueAfter("Subject");
            var description = GetValueAfter("Description:");

            // Handle multi-line description if separated by blank line or next key
            var descriptionIdx = lines.FindIndex(l =>
                l.Trim().Equals("Description", StringComparison.OrdinalIgnoreCase) ||
                l.Trim().Equals("Description:", StringComparison.OrdinalIgnoreCase));
            var searchStartIdx = descriptionIdx != -1 ? descriptionIdx + 1 : 0;
            var separatorIdx = lines.FindIndex(searchStartIdx, l =>
                l.Trim().StartsWith("Severity:", StringComparison.OrdinalIgnoreCase) ||
                l.Trim().StartsWith("====================") ||
                l.Trim().StartsWith("Submitted Values:", StringComparison.OrdinalIgnoreCase) ||
                l.Trim().StartsWith("Server-Provided Values:", StringComparison.OrdinalIgnoreCase));
            if (separatorIdx == -1) separatorIdx = lines.Count;

            if (descriptionIdx != -1 && descriptionIdx + 1 < lines.Count)
            {
                description = string.Join(Environment.NewLine, lines.Skip(descriptionIdx + 1).Take(separatorIdx - descriptionIdx - 1)).Trim();
            }
            else if (string.IsNullOrWhiteSpace(subject) && string.IsNullOrWhiteSpace(note.CaseNumber) && separatorIdx > 0)
            {
                // Fallback when user selects directly inside the Description text box without header labels
                description = string.Join(Environment.NewLine, lines.Take(separatorIdx)).Trim();
            }

            var doc = new FlowDocument();
            doc.Blocks.Add(new Paragraph(new Run($"{subject}{Environment.NewLine}{description}".Trim())));
            note.NotesXaml = XamlWriter.Save(doc);

            var contact = note.Contacts[0];
            var parsedContactName = GetValueAfter("Contact Name");
            if (string.IsNullOrWhiteSpace(parsedContactName))
            {
                parsedContactName = GetValueFromKeyValue("Name:", submittedValues);
            }
            if (string.IsNullOrWhiteSpace(parsedContactName))
            {
                parsedContactName = GetValueFromKeyValue("Contact Name:", lines);
            }
            if (string.IsNullOrWhiteSpace(parsedContactName))
            {
                parsedContactName = GetValueFromKeyValue("Name:", lines);
            }
            contact.Name = parsedContactName;
            contact.Email = GetValueFromKeyValue("Email:", submittedValues);
            if (string.IsNullOrWhiteSpace(contact.Email))
            {
                contact.Email = GetValueFromKeyValue("Contact Email:", lines);
                if (string.IsNullOrWhiteSpace(contact.Email))
                {
                    contact.Email = GetValueFromKeyValue("Email:", lines);
                }
            }

            var phoneRaw = GetValueFromKeyValue("Phone:", submittedValues);
            if (string.IsNullOrWhiteSpace(phoneRaw))
            {
                phoneRaw = GetValueFromKeyValue("Contact Phone:", lines);
                if (string.IsNullOrWhiteSpace(phoneRaw))
                {
                    phoneRaw = GetValueFromKeyValue("Phone:", lines);
                }
            }
            var phoneMatch = Regex.Match(phoneRaw, @"^(\S+)\s*(?:ext\.?|x)?\s*(\d+)?$");
            if (phoneMatch.Success)
            {
                contact.Phone = phoneMatch.Groups[1].Value;
                contact.PhoneExtension = phoneMatch.Groups[2].Value;
            }
            else
            {
                contact.Phone = phoneRaw;
            }

            var dealer = note.Dealers[0];
            dealer.Name = GetValueFromKeyValue("Company Name:", submittedValues);
            if (string.IsNullOrWhiteSpace(dealer.Name))
            {
                dealer.Name = GetValueFromKeyValue("Company Name:", lines);
                if (string.IsNullOrWhiteSpace(dealer.Name))
                {
                    dealer.Name = GetValueFromKeyValue("Dealership:", lines);
                }
            }
            dealer.ServerCode = GetValueFromKeyValue("Server ID:", serverValues);
            if (string.IsNullOrWhiteSpace(dealer.ServerCode))
            {
                dealer.ServerCode = GetValueFromKeyValue("Server ID:", lines);
                if (string.IsNullOrWhiteSpace(dealer.ServerCode))
                {
                    dealer.ServerCode = GetValueFromKeyValue("Server Code:", lines);
                }
            }

            var company = dealer.Companies[0];
            company.Name = dealer.Name;
            company.CompanyCode = GetValueFromKeyValue("Company Number:", submittedValues);
            if (string.IsNullOrWhiteSpace(company.CompanyCode))
            {
                company.CompanyCode = GetValueFromKeyValue("Company Number:", lines);
            }

            if (IsMatchingLabel(note.CaseNumber, "Case Number", "Case #", "CaseNumber")) note.CaseNumber = string.Empty;
            if (IsMatchingLabel(contact.Name, "Contact Name", "Contact", "Name")) contact.Name = string.Empty;
            if (IsMatchingLabel(contact.Email, "Contact Email", "Email")) contact.Email = string.Empty;
            if (IsMatchingLabel(contact.Phone, "Contact Phone", "Phone", "Mobile")) contact.Phone = string.Empty;
            if (IsMatchingLabel(contact.PhoneExtension, "Ext", "Extension")) contact.PhoneExtension = string.Empty;
            if (IsMatchingLabel(dealer.Name, "Account Name", "Company Name")) dealer.Name = string.Empty;
            if (IsMatchingLabel(dealer.ServerCode, "Server ID", "Server Code", "Server", "Automate Server Code")) dealer.ServerCode = string.Empty;
            if (IsMatchingLabel(company.Name, "Account Name", "Company Name")) company.Name = string.Empty;
            if (IsMatchingLabel(company.CompanyCode, "Company Number", "Company Code")) company.CompanyCode = string.Empty;

            // Bi-Directional recall: Parse prior action log entries from Salesforce text
            var parsedEntries = CaseWorkLogParser.ParseWorkLogEntries(caseText, markAsHistorical: true);
            foreach (var pe in parsedEntries)
            {
                var item = new CaseLogEntryItem(pe, _logger) { Parent = note };
                note.LogEntries.Add(item);
            }

            // Parse linked cases
            var parsedLinkedCases = CaseWorkLogParser.ParseLinkedCases(caseText);
            foreach (var lc in parsedLinkedCases)
            {
                if (!note.LinkedCases.Contains(lc))
                    note.LinkedCases.Add(lc);
            }

            _logger?.LogInfo("Case text parsed into NoteModel.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error parsing case text.", ex);
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
        return note;
    }

    private NoteModel ApplyParsedNote(NoteModel newNote)
    {
        var recipient = Notes.LastOrDefault(n => n.IsBlank);
        if (recipient is null)
        {
            Notes.Add(newNote);
            newNote.UpdateCore();
            return newNote;
        }

        recipient.CaseNumber = newNote.CaseNumber;
        recipient.NotesXaml = newNote.NotesXaml;

        var recipientContact = recipient.Contacts.FirstOrDefault(c => c.IsBlank) ?? recipient.Contacts.FirstOrDefault();
        var sourceContact = newNote.Contacts.FirstOrDefault();
        if (recipientContact is not null && sourceContact is not null)
        {
            recipientContact.Name = sourceContact.Name;
            recipientContact.Email = sourceContact.Email;
            recipientContact.Phone = sourceContact.Phone;
            recipientContact.PhoneExtension = sourceContact.PhoneExtension;
            recipientContact.Role = sourceContact.Role;
            recipientContact.Notes = sourceContact.Notes;
            recipientContact.IsPrimary = sourceContact.IsPrimary;
            recipientContact.Select();
        }

        var recipientDealer = recipient.Dealers.FirstOrDefault(d => d.IsBlank) ?? recipient.Dealers.FirstOrDefault();
        var sourceDealer = newNote.Dealers.FirstOrDefault();
        if (recipientDealer is not null && sourceDealer is not null)
        {
            recipientDealer.Name = sourceDealer.Name;
            recipientDealer.ServerCode = sourceDealer.ServerCode;

            var recipientCompany = recipientDealer.Companies.FirstOrDefault(c => c.IsBlank) ?? recipientDealer.Companies.FirstOrDefault();
            var sourceCompany = sourceDealer.Companies.FirstOrDefault();
            if (recipientCompany is not null && sourceCompany is not null)
            {
                recipientCompany.Name = sourceCompany.Name;
                recipientCompany.CompanyCode = sourceCompany.CompanyCode;
                recipientCompany.Select();
            }
            recipientDealer.Select();
        }

        var recipientForm = recipient.Forms.FirstOrDefault(f => f.IsBlank) ?? recipient.Forms.FirstOrDefault();
        var sourceForm = newNote.Forms.FirstOrDefault();
        if (recipientForm is not null && sourceForm is not null)
        {
            recipientForm.Name = sourceForm.Name;
            recipientForm.Notes = sourceForm.Notes;
            recipientForm.Format = sourceForm.Format;
            recipientForm.Select();
        }

        if (newNote.LogEntries.Count > 0)
        {
            foreach (var entry in newNote.LogEntries.Where(e => !e.IsBlank))
            {
                if (!recipient.LogEntries.Any(re => re.Details == entry.Details && re.FormName == entry.FormName && re.Action == entry.Action))
                {
                    var clone = new CaseLogEntryItem(entry.CoreType ?? new CaseLogEntry(), _logger)
                    {
                        Parent = recipient,
                        Timestamp = entry.Timestamp,
                        Action = entry.Action,
                        FormName = entry.FormName,
                        IsCustomCopy = entry.IsCustomCopy,
                        Details = entry.Details,
                        IncludeInOutput = entry.IncludeInOutput,
                        IsHistorical = entry.IsHistorical
                    };
                    foreach (var s in entry.Stores) clone.Stores.Add(s.Clone());
                    foreach (var d in entry.Deals) clone.Deals.Add(d.Clone());
                    recipient.LogEntries.Add(clone);
                }
            }
        }

        if (newNote.LinkedCases.Count > 0)
        {
            foreach (var lc in newNote.LinkedCases)
            {
                if (!recipient.LinkedCases.Contains(lc))
                    recipient.LinkedCases.Add(lc);
            }
        }

        recipient.UpdateCore();
        return recipient;
    }
}

public partial class ComposerTagOption : ObservableObject
{
    public string Tag { get; }

    [ObservableProperty]
    private bool _isSelected;

    public ComposerTagOption(string tag)
    {
        Tag = tag;
    }
}
