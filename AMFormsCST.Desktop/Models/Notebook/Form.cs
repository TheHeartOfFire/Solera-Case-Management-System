using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Desktop.BaseClasses;
using AMFormsCST.Desktop.Types;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog.Context;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Documents;
using static AMFormsCST.Core.Interfaces.Notebook.IForm;

namespace AMFormsCST.Desktop.Models;
public partial class Form : ManagedObservableCollectionItem
{
    private bool _isInitializing;

    [ObservableProperty]
    private string? _name = string.Empty;
    [ObservableProperty]

    private FlowDocument? _notes = new();
    public ManagedObservableCollection<TestDeal> TestDeals { get; set; }
    [ObservableProperty]
    private bool _notable = true;
    [ObservableProperty]
    private FormFormat _format = FormFormat.Pdf;
    public bool IsImpact
    {
        get => Format == FormFormat.LegacyImpact;
        set
        {
            if (Format != FormFormat.Pdf)
            {
                Format = FormFormat.Pdf;
                
            }
            else if (Format != FormFormat.LegacyImpact)
            {
                Format = FormFormat.LegacyImpact;
            }
        }
    }
    internal IForm? CoreType { get; set; }
    internal NoteModel? Parent { get; set; }
    public override Guid Id { get; } = Guid.NewGuid();
    public override bool IsBlank
    {
        get
        {
            if (!string.IsNullOrEmpty(Name) || !string.IsNullOrWhiteSpace(GetFlowDocumentPlainText(Notes ?? new())))
                return false;
            if (TestDeals.Any(td => !td.IsBlank))
                return false;
            return true;
        }
    }
    public TestDeal? SelectedTestDeal => TestDeals.SelectedItem;

    partial void OnNameChanged(string? value)
    {
        OnPropertyChanged(nameof(IsBlank));
        UpdateCore();
        using (LogContext.PushProperty("FormId", Id))
        using (LogContext.PushProperty("Name", value))
        using (LogContext.PushProperty("Notes", Notes))
        using (LogContext.PushProperty("TestDeals", TestDeals.Count))
        {
            _logger?.LogInfo($"Form name changed: {value}");
        }
    }

    partial void OnNotesChanged(FlowDocument? value)
    {
        OnPropertyChanged(nameof(IsBlank));
        UpdateCore();
        using (LogContext.PushProperty("FormId", Id))
        using (LogContext.PushProperty("Name", Name))
        using (LogContext.PushProperty("Notes", value))
        using (LogContext.PushProperty("TestDeals", TestDeals.Count))
        {
            _logger?.LogInfo($"Form notes changed: {value}");
        }
    }

    partial void OnNotableChanged(bool value)
    {
        UpdateCore();
        using (LogContext.PushProperty("FormId", Id))
        using (LogContext.PushProperty("Notable", value))
        {
            _logger?.LogInfo($"Form notable changed: {value}");
        }
    }
    partial void OnFormatChanged(FormFormat value)
    {
        OnPropertyChanged(nameof(IsImpact));
        UpdateCore();
        using (LogContext.PushProperty("FormId", Id))
        using (LogContext.PushProperty("Format", value))
        {
            _logger?.LogInfo($"Form format changed: {value}");
        }
    }

    public Form(NoteModel parent, ILogService? logger = null) : base(logger)
    {
        _isInitializing = true;
        CoreType = new Core.Types.Notebook.Form();
        Parent = parent;
        InitTestDeals();

        TestDeals ??= new ManagedObservableCollection<TestDeal>(
            () => new TestDeal(_logger) { Parent = this },
            null,
            _logger
        );

        _logger?.LogInfo("Form initialized.");
        _isInitializing = false;
    }
    public Form(NoteModel parent, IForm form, ILogService? logger = null) : base(logger)
    {
        _isInitializing = true;
        CoreType = form;
        Parent = parent;

        InitTestDeals();

        TestDeals ??= new ManagedObservableCollection<TestDeal>(
            () => new TestDeal(_logger) { Parent = this },
            null,
            _logger
        );

        Name = form.Name ?? string.Empty;
        Notes =  new() { Blocks = { new Paragraph(new Run(form.Notes ?? string.Empty)) } };
        Notable = form.Notable;
        Format = form.Format;
        _logger?.LogInfo("Form loaded from core type.");
        _isInitializing = false;
        UpdateCore();
    }
    private void InitTestDeals()
    {
        var testDeals = CoreType?.TestDeals.ToList()
                .Select(coreTestDeal =>
                {
                    var testDeal = new TestDeal(coreTestDeal, _logger)
                    {
                        CoreType = coreTestDeal,
                        Parent = this
                    };
                    testDeal.PropertyChanged += OnTestDealPropertyChanged;
                    return testDeal;
                });

        TestDeals = new ManagedObservableCollection<TestDeal>(
            () => new TestDeal(_logger) { Parent = this },
            testDeals,
            _logger,
            (td) => td.PropertyChanged += OnTestDealPropertyChanged
        );
        TestDeals.PropertyChanged += OnTestDealsPropertyChanged;
        TestDeals.CollectionChanged += TestDeals_CollectionChanged;
        TestDeals.FirstOrDefault()?.Select();
    }

    private void OnTestDealsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManagedObservableCollection<TestDeal>.SelectedItem))
        {
            OnPropertyChanged(nameof(SelectedTestDeal));
        }
    }

    private void OnTestDealPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsBlank));
    }

    private void TestDeals_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (TestDeal td in e.NewItems)
            {
                td.Parent = this;
                td.PropertyChanged -= OnTestDealPropertyChanged;
                td.PropertyChanged += OnTestDealPropertyChanged;
            }
        if (e.OldItems != null)
            foreach (TestDeal td in e.OldItems)
                td.PropertyChanged -= OnTestDealPropertyChanged;
        UpdateCore();
        Parent?.Parent?.NotifyTestDealNavigationChanged();
        _logger?.LogDebug("TestDeals collection changed.");
    }

    internal void UpdateCore()
    {
        if (_isInitializing) return;

        if (CoreType == null && Parent?.CoreType != null)
            CoreType = Parent.CoreType.Forms.FirstOrDefault(f => f.Id == Id);
        if (CoreType == null) return;
        CoreType.Name = Name ?? string.Empty;
        CoreType.Notes = GetFlowDocumentPlainText(Notes ?? new()) ?? string.Empty;
        CoreType.Notable = Notable;
        CoreType.Format = Format;
        CoreType.TestDeals.Clear();
        CoreType.TestDeals.AddRange(TestDeals.Select(td => (Core.Types.Notebook.TestDeal)td));
        CoreType.TestDeals.SelectedItem = TestDeals?.SelectedItem?.CoreType;
        Parent?.UpdateCore();
        _logger?.LogDebug("Form core updated.");
    }
    public static string GetFlowDocumentPlainText(FlowDocument document)
    {
        // Create a TextRange from the beginning (ContentStart) to the end (ContentEnd) of the document.
        TextRange textRange = new(
            document.ContentStart,
            document.ContentEnd
        );

        // The Text property of the TextRange object returns the plain text content as a string.
        return textRange.Text;
    }

    public static implicit operator Core.Types.Notebook.Form(Form form)
    {
        if (form is null) return new Core.Types.Notebook.Form();
        return new Core.Types.Notebook.Form(form.Id)
        {
            Name = form.Name ?? string.Empty,
            Notes = GetFlowDocumentPlainText(form.Notes ?? new()) ?? string.Empty,
            Notable = form.Notable,
            Format = form.Format,
            TestDeals = [..form.TestDeals.Select(td => (Core.Types.Notebook.TestDeal)td)]
        };
    }
}
