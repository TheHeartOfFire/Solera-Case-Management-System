using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Desktop.BaseClasses;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog.Context;
using System;

namespace AMFormsCST.Desktop.Models;
public partial class Company : ManagedObservableCollectionItem
{
    private bool _isInitializing;

    [ObservableProperty]
    private string? _name = string.Empty;
    [ObservableProperty]
    private string? _companyCode = string.Empty;
    [ObservableProperty]
    private bool _notable = true;

    public override bool IsBlank { get { return string.IsNullOrEmpty(Name) && string.IsNullOrEmpty(CompanyCode); }}
    public override Guid Id { get; } = Guid.NewGuid();
    internal ICompany? CoreType { get; set; }
    internal Dealer? Parent { get; set; }

    public Company(ILogService? logger = null) : base(logger)
    {
        _isInitializing = true;
        _logger?.LogInfo("Company initialized.");
        _isInitializing = false;
    }
    public Company(ICompany company, ILogService? logger = null) : base(logger)
    {
        _isInitializing = true;
        CoreType = company;
        Name = company.Name;
        CompanyCode = company.CompanyCode;
        Notable = company.Notable;
        _logger?.LogInfo("Company loaded from core type.");
        _isInitializing = false;
        UpdateCore();
    }
    partial void OnNameChanged(string? value)
    {
        OnPropertyChanged(nameof(IsBlank));
        UpdateCore();
        using (LogContext.PushProperty("CompanyId", Id))
        using (LogContext.PushProperty("Name", value))
        using (LogContext.PushProperty("CompanyCode", CompanyCode))
        {
            _logger?.LogInfo($"Company name changed: {value}");
        }
    }

    partial void OnCompanyCodeChanged(string? value)
    {
        OnPropertyChanged(nameof(IsBlank));
        UpdateCore();
        using (LogContext.PushProperty("CompanyId", Id))
        using (LogContext.PushProperty("Name", Name))
        using (LogContext.PushProperty("CompanyCode", value))
        {
            _logger?.LogInfo($"Company code changed: {value}");
        }
    }

    partial void OnNotableChanged(bool value)
    {
        UpdateCore();
        using (LogContext.PushProperty("CompanyId", Id))
        using (LogContext.PushProperty("Notable", value))
        {
            _logger?.LogInfo($"Company notable changed: {value}");
        }
    }
    
    internal void UpdateCore()
    {
        if (_isInitializing) return;

        if (CoreType == null && Parent?.CoreType != null)
            CoreType = Parent.CoreType.Companies.FirstOrDefault(c => c.Id == Id);
        if (CoreType == null) return;
        CoreType.Name = Name ?? string.Empty;
        CoreType.CompanyCode = CompanyCode ?? string.Empty;
        CoreType.Notable = Notable;
        Parent?.UpdateCore();
        _logger?.LogDebug("Company core updated.");
    }

    public static implicit operator Core.Types.Notebook.Company(Company company)
    {
        if (company is null) return new Core.Types.Notebook.Company();

        return new Core.Types.Notebook.Company(company.Id)
        {
            Name = company.Name ?? string.Empty,
            CompanyCode = company.CompanyCode ?? string.Empty,
            Notable = company.Notable
        };
    }
}
