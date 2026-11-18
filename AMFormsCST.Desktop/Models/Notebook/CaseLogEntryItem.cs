using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Types.Notebook;
using AMFormsCST.Desktop.BaseClasses;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace AMFormsCST.Desktop.Models;

public partial class CaseLogEntryItem : ManagedObservableCollectionItem
{
    private bool _isInitializing;

    [ObservableProperty]
    private DateTime _timestamp = DateTime.Now;

    [ObservableProperty]
    private CaseActionType _action = CaseActionType.LineAdjustments;

    [ObservableProperty]
    private string _formName = string.Empty;

    [ObservableProperty]
    private bool _isCustomCopy;

    [ObservableProperty]
    private string _details = string.Empty;

    [ObservableProperty]
    private bool _includeInOutput = true;

    [ObservableProperty]
    private bool _isHistorical;

    public ObservableCollection<CaseStoreAssignment> Stores { get; set; } = [];
    public ObservableCollection<CaseDealAssignment> Deals { get; set; } = [];
    public ObservableCollection<string> Tags { get; set; } = [];

    internal NoteModel? Parent { get; set; }
    internal ICaseLogEntry? CoreType { get; set; }

    public override Guid Id { get; } = Guid.NewGuid();

    public override bool IsBlank =>
        string.IsNullOrEmpty(FormName) &&
        string.IsNullOrEmpty(Details) &&
        Stores.Count == 0 &&
        Deals.Count == 0 &&
        Tags.Count == 0;

    public string StoresSummary => Stores.Count > 0
        ? string.Join(", ", Stores.Select(s => s.DisplayText))
        : "All Stores";

    public string DealsSummary => Deals.Count > 0
        ? string.Join(", ", Deals.Select(d => d.DisplayText))
        : "None";

    public string ActionDisplayName => Tags.Count > 0
        ? string.Join(" ", Tags)
        : Action switch
        {
            CaseActionType.NewFormPublish => "New form publish",
            CaseActionType.ProgrammingUpdate => "Programming update",
            CaseActionType.LineAdjustments => "Line Adjustments",
            CaseActionType.Investigation => "Investigation",
            CaseActionType.Republish => "Republish",
            CaseActionType.CustomNote => "Note",
            _ => Action.ToString()
        };

    public CaseLogEntryItem(ILogService? logger = null) : base(logger)
    {
        _isInitializing = true;
        _isInitializing = false;
    }

    public CaseLogEntryItem(ICaseLogEntry core, ILogService? logger = null) : base(logger)
    {
        _isInitializing = true;
        CoreType = core;
        Timestamp = core.Timestamp;
        Action = core.Action;
        FormName = core.FormName ?? string.Empty;
        IsCustomCopy = core.IsCustomCopy;
        Details = core.Details ?? string.Empty;
        IncludeInOutput = core.IncludeInOutput;
        IsHistorical = core.IsHistorical;

        Stores = new ObservableCollection<CaseStoreAssignment>(core.Stores?.Select(s => s.Clone()) ?? []);
        Deals = new ObservableCollection<CaseDealAssignment>(core.Deals?.Select(d => d.Clone()) ?? []);
        Tags = new ObservableCollection<string>(core.Tags ?? []);

        _isInitializing = false;
        UpdateCore();
    }

    partial void OnFormNameChanged(string value)
    {
        OnPropertyChanged(nameof(IsBlank));
        UpdateCore();
    }

    partial void OnDetailsChanged(string value)
    {
        OnPropertyChanged(nameof(IsBlank));
        UpdateCore();
    }

    partial void OnActionChanged(CaseActionType value)
    {
        OnPropertyChanged(nameof(ActionDisplayName));
        UpdateCore();
    }

    partial void OnIsCustomCopyChanged(bool value) => UpdateCore();
    partial void OnIncludeInOutputChanged(bool value) => UpdateCore();

    internal void UpdateCore()
    {
        if (_isInitializing) return;

        if (CoreType == null && Parent?.CoreType != null)
        {
            CoreType = Parent.CoreType.LogEntries.FirstOrDefault(e => e.Id == Id);
        }

        if (CoreType == null) return;

        CoreType.Timestamp = Timestamp;
        CoreType.Action = Action;
        CoreType.FormName = FormName ?? string.Empty;
        CoreType.IsCustomCopy = IsCustomCopy;
        CoreType.Details = Details ?? string.Empty;
        CoreType.IncludeInOutput = IncludeInOutput;
        CoreType.IsHistorical = IsHistorical;
        CoreType.Stores = Stores.Select(s => s.Clone()).ToList();
        CoreType.Deals = Deals.Select(d => d.Clone()).ToList();
        CoreType.Tags = [.. Tags];

        Parent?.UpdateCore();
    }
}

