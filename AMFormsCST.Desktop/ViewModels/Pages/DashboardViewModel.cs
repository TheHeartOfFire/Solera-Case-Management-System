using AMFormsCST.Core;
using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Desktop.BaseClasses;
using AMFormsCST.Desktop.Models;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.Types;
using AMFormsCST.Desktop.ViewModels.Dialogs;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using AMFormsCST.Desktop.Views.Dialogs;
using AMFormsCST.Desktop.Views.Pages.Tools;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
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

    private NoteModel? _lastSelectedNote;
    private Models.Dealer? _lastSelectedDealer;
    private Models.Form? _lastSelectedForm;

    public DashboardViewModel()
    {
        _debounceService = new DesignTimeDebounceService();

        var note1 = new NoteModel("x")
        {
            CaseNumber = "00123456",
            NotesXaml = XamlWriter.Save(new FlowDocument(new Paragraph(new Run("This is the first sample note.")))),
        };
        var note2 = new NoteModel("x")
        {
            CaseNumber = "00234567",
            NotesXaml = XamlWriter.Save(new FlowDocument(new Paragraph(new Run("This is the second sample note.")))),
        };

        var contact1 = new Models.Contact("x") { Name = "John Doe", Email = "john.doe@email.com", Phone = "888-555-1234", PhoneExtension = "1234" };
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

        _notes = new ManagedObservableCollection<NoteModel>(() => new NoteModel("x"), [note1, note2]);
        note1.Select();
    }

    public DashboardViewModel(ISupportTool supportTool, IDialogService dialogService, IFileSystem fileSystem, IDebounceService debounceService, ILogService? logger = null)
    {
        _supportTool = supportTool ?? throw new ArgumentNullException(nameof(supportTool));
        _dialogService = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        _fileSystem = fileSystem;
        _logger = logger;
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
        }

        OnPropertyChanged(nameof(SelectedNote));
        SelectPreviousNoteCommand.NotifyCanExecuteChanged();
        SelectNextNoteCommand.NotifyCanExecuteChanged();

        if (SelectedNote != null)
        {
            SelectedNote.Dealers.PropertyChanged += SelectedNote_Dealers_PropertyChanged;
            SelectedNote.Contacts.PropertyChanged += SelectedNote_Contacts_PropertyChanged;
            SelectedNote.Forms.PropertyChanged += SelectedNote_Forms_PropertyChanged;
        }

        // Manually trigger property changed for sub-collections to update CanExecute
        SelectedNote_Dealers_PropertyChanged(this, new PropertyChangedEventArgs(nameof(ManagedObservableCollection<Models.Dealer>.SelectedItem)));
        SelectedNote_Contacts_PropertyChanged(this, new PropertyChangedEventArgs(nameof(ManagedObservableCollection<Models.Contact>.SelectedItem)));
        SelectedNote_Forms_PropertyChanged(this, new PropertyChangedEventArgs(nameof(ManagedObservableCollection<Models.Form>.SelectedItem)));

        _lastSelectedNote = SelectedNote;
        UpdateTemplatesVM();
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
    [RelayCommand]
    private void OpenTemplateDialog()
    {
        try
        {
            var vm = new TemplatesViewModel(_supportTool, _fileSystem);
            _templatesvm = vm;
            var page = new TemplatesPage(vm);
            var dialog = new PageHostDialog(page, "Templates");
            dialog.Show();
            dialog.Closed += (s, e) => _templatesvm = null;
            _logger?.LogInfo("Opened Template Dialog.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error opening Template Dialog.", ex);
        }
    }

    private void UpdateTemplatesVM()
    {
        try
        {
            _templatesvm?.Refresh();
            _logger?.LogInfo("TemplatesViewModel updated.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error updating TemplatesViewModel.", ex);
        }
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
                if (SelectedNote?.SelectedForm?.IsBlank == true)
                {
                    SelectedNote.SelectedForm.Name = name;
                }
                else if (SelectedNote is not null)
                {
                    SelectedNote.Forms.Last().Name = name ?? string.Empty;
                }
            dialog.Closed -= FormNameDialogClosed;
            _logger?.LogInfo("Form Name Dialog closed.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error in FormNameDialogClosed.", ex);
        }
    }

    [RelayCommand]
    private void LoadCase()
    {
        var note = ParseCaseText(Clipboard.GetText());
        ApplyParsedNote(note);
        Notes.LastOrDefault(n => n.IsBlank == false)?.Select();

        if (_supportTool is not null &&
            _supportTool.Notebook.Notes.SelectedItem?.Id != SelectedNote?.CoreType?.Id)
            _supportTool.Notebook.Notes.SelectedItem = SelectedNote?.CoreType;

        if (_supportTool is not null && _supportTool.Notebook.Notes.SelectedItem is not null &&
            _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem?.Id != SelectedNote.SelectedDealer.CoreType?.Id)
            _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem = SelectedNote.SelectedDealer.CoreType;

        if (_supportTool is not null &&
            _supportTool.Notebook.Notes.SelectedItem is not null &&
            _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem is not null &&
            _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem!.Companies.SelectedItem?.Id != SelectedNote.SelectedDealer.SelectedCompany.CoreType?.Id)
            _supportTool.Notebook.Notes.SelectedItem.Dealers.SelectedItem!.Companies.SelectedItem = SelectedNote.SelectedDealer.SelectedCompany.CoreType;

        if (_supportTool is not null &&
            _supportTool.Notebook.Notes.SelectedItem is not null &&
            _supportTool.Notebook.Notes.SelectedItem.Contacts.SelectedItem?.Id != SelectedNote.SelectedContact.CoreType?.Id)
            _supportTool.Notebook.Notes.SelectedItem.Contacts.SelectedItem = SelectedNote.SelectedContact.CoreType;

        if (_supportTool is not null &&
            _supportTool.Notebook.Notes.SelectedItem is not null &&
            _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem?.Id != SelectedNote.SelectedForm.CoreType?.Id)
            _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem = SelectedNote.SelectedForm.CoreType;

        if (_supportTool is not null &&
            _supportTool.Notebook.Notes.SelectedItem is not null &&
            _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem is not null &&
            _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem!.TestDeals.SelectedItem?.Id != SelectedNote.SelectedForm.SelectedTestDeal.CoreType?.Id)
            _supportTool.Notebook.Notes.SelectedItem.Forms.SelectedItem!.TestDeals.SelectedItem = SelectedNote.SelectedForm.SelectedTestDeal.CoreType;

        UpdateTemplatesVM();
        _logger?.LogInfo("LoadCase command executed.");
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

    public NoteModel ParseCaseText(string caseText)
    {
        var note = new NoteModel(_supportTool.Settings.UserSettings.ExtSeparator, _logger);
        var textLines = caseText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        try
        {
            var lines = textLines.ToList();

            string GetValueAfter(string key)
            {
                var index = lines.FindIndex(l => l.Trim().Equals(key, StringComparison.OrdinalIgnoreCase));
                return index != -1 && index + 1 < lines.Count ? lines[index + 1].Trim() : string.Empty;
            }

            string GetValueFromKeyValue(string key, List<string> section)
            {
                var line = section.FirstOrDefault(l => l.Trim().StartsWith(key, StringComparison.OrdinalIgnoreCase));
                return line?.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? string.Empty;
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
            var descriptionIdx = lines.FindIndex(l => l.Trim().Equals("Description", StringComparison.OrdinalIgnoreCase));
            var separatorIdx = lines.FindIndex(descriptionIdx + 1, l => l.Trim().StartsWith("Severity:") || string.IsNullOrWhiteSpace(l));
            if (separatorIdx == -1) separatorIdx = lines.Count;
            
            description = (descriptionIdx != -1 && descriptionIdx + 1 < lines.Count) 
                ? string.Join(Environment.NewLine, lines.Skip(descriptionIdx + 1).Take(separatorIdx - descriptionIdx - 1)).Trim()
                : string.Empty;

            var doc = new FlowDocument();
            doc.Blocks.Add(new Paragraph(new Run($"{subject}{Environment.NewLine}{description}".Trim())));
            note.NotesXaml = XamlWriter.Save(doc);

            var contact = note.Contacts[0];
            contact.Name = GetValueAfter("Contact Name");
            contact.Email = GetValueFromKeyValue("Email:", submittedValues);

            var phoneRaw = GetValueFromKeyValue("Phone:", submittedValues);
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
            dealer.ServerCode = GetValueFromKeyValue("Server ID:", serverValues);



            var company = dealer.Companies[0];
            company.Name = GetValueFromKeyValue("Company Name:", submittedValues);
            company.CompanyCode = GetValueFromKeyValue("Company Number:", submittedValues);

            _logger?.LogInfo("Case text parsed into NoteModel.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error parsing case text.", ex);
            ExceptionDispatchInfo.Capture(ex).Throw();
        }
        return note;
    }

    private void ApplyParsedNote(NoteModel newNote)
    {
        var recipient = Notes.LastOrDefault(n => n.IsBlank);
        if (recipient is null) return;

        recipient.CaseNumber = newNote.CaseNumber;
        recipient.NotesXaml = newNote.NotesXaml;

        var recipientContact = recipient.Contacts.FirstOrDefault(c => c.IsBlank);
        var sourceContact = newNote.Contacts.FirstOrDefault();
        if (recipientContact is not null && sourceContact is not null)
        {
            recipientContact.Name = sourceContact.Name;
            recipientContact.Email = sourceContact.Email;
            recipientContact.Phone = sourceContact.Phone;
        }

        var recipientDealer = recipient.Dealers.FirstOrDefault(d => d.IsBlank);
        var sourceDealer = newNote.Dealers.FirstOrDefault();
        if (recipientDealer is not null && sourceDealer is not null)
        {
            recipientDealer.Name = sourceDealer.Name;
            recipientDealer.ServerCode = sourceDealer.ServerCode;

            var recipientCompany = recipientDealer.Companies.FirstOrDefault(c => c.IsBlank);
            var sourceCompany = sourceDealer.Companies.FirstOrDefault();
            if (recipientCompany is not null && sourceCompany is not null)
            {
                recipientCompany.Name = sourceCompany.Name;
                recipientCompany.CompanyCode = sourceCompany.CompanyCode;
            }
        }
    }
}
