using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Notebook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Types.Notebook;

public enum CaseActionType
{
    NewFormPublish,
    ProgrammingUpdate,
    LineAdjustments,
    Investigation,
    Republish,
    CustomNote
}

public class CaseStoreAssignment
{
    public string CompanyCode { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;

    public CaseStoreAssignment() { }
    public CaseStoreAssignment(string companyCode, string storeName = "")
    {
        CompanyCode = companyCode;
        StoreName = storeName;
    }

    public string DisplayText => string.IsNullOrWhiteSpace(StoreName)
        ? CompanyCode
        : $"{CompanyCode} - {StoreName}";

    public CaseStoreAssignment Clone() => new()
    {
        CompanyCode = CompanyCode,
        StoreName = StoreName
    };
}

public class CaseDealAssignment
{
    public string DealNumber { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;

    public CaseDealAssignment() { }
    public CaseDealAssignment(string dealNumber, string purpose = "")
    {
        DealNumber = dealNumber;
        Purpose = purpose;
    }

    public string DisplayText => string.IsNullOrWhiteSpace(Purpose)
        ? DealNumber
        : $"{DealNumber} ({Purpose})";

    public CaseDealAssignment Clone() => new()
    {
        DealNumber = DealNumber,
        Purpose = Purpose
    };
}

[JsonDerivedType(typeof(CaseLogEntry), typeDiscriminator: "caseLogEntry")]
public class CaseLogEntry : ICaseLogEntry
{
    private readonly ILogService? _logger;
    private readonly Guid _id = Guid.NewGuid();

    public Guid Id => _id;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public CaseActionType Action { get; set; } = CaseActionType.LineAdjustments;
    public string FormName { get; set; } = string.Empty;
    public bool IsCustomCopy { get; set; }
    public List<CaseStoreAssignment> Stores { get; set; } = [];
    public List<CaseDealAssignment> Deals { get; set; } = [];
    public List<string> Tags { get; set; } = [];
    public string Details { get; set; } = string.Empty;
    public bool IncludeInOutput { get; set; } = true;
    public bool IsHistorical { get; set; }

    public CaseLogEntry() : this(null) { }

    public CaseLogEntry(ILogService? logger)
    {
        _logger = logger;
        _logger?.LogInfo($"CaseLogEntry initialized. Id: {_id}");
    }

    public CaseLogEntry(Guid id, ILogService? logger = null) : this(logger)
    {
        _id = id;
    }

    public string Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Id: {Id}");
        sb.AppendLine($"Timestamp: {Timestamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Action: {Action}");
        sb.AppendLine($"FormName: {FormName}");
        sb.AppendLine($"IsCustomCopy: {IsCustomCopy}");
        sb.AppendLine($"Stores: {string.Join(", ", Stores.Select(s => s.DisplayText))}");
        sb.AppendLine($"Deals: {string.Join(", ", Deals.Select(d => d.DisplayText))}");
        sb.AppendLine($"Tags: {string.Join(", ", Tags)}");
        sb.AppendLine($"Details: {Details}");
        sb.AppendLine($"IncludeInOutput: {IncludeInOutput}");
        sb.AppendLine($"IsHistorical: {IsHistorical}");
        return sb.ToString();
    }

    public ICaseLogEntry Clone()
    {
        return new CaseLogEntry(_logger)
        {
            Timestamp = Timestamp,
            Action = Action,
            FormName = FormName,
            IsCustomCopy = IsCustomCopy,
            Stores = Stores.Select(s => s.Clone()).ToList(),
            Deals = Deals.Select(d => d.Clone()).ToList(),
            Tags = [.. Tags],
            Details = Details,
            IncludeInOutput = IncludeInOutput,
            IsHistorical = IsHistorical
        };
    }

    public bool Equals(ICaseLogEntry? other)
    {
        if (other is null) return false;
        return _id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (obj is ICaseLogEntry entry)
            return Equals(entry);
        return false;
    }

    public override int GetHashCode() => _id.GetHashCode();
}

