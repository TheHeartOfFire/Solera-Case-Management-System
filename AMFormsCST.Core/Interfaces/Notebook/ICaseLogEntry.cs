using AMFormsCST.Core.Types.Notebook;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Interfaces.Notebook;

[JsonDerivedType(typeof(CaseLogEntry), typeDiscriminator: "caseLogEntry")]
public interface ICaseLogEntry : INotebookItem<ICaseLogEntry>
{
    DateTime Timestamp { get; set; }
    CaseActionType Action { get; set; }
    string FormName { get; set; }
    bool IsCustomCopy { get; set; }
    List<CaseStoreAssignment> Stores { get; set; }
    List<CaseDealAssignment> Deals { get; set; }
    List<string> Tags { get; set; }
    string Details { get; set; }
    bool IncludeInOutput { get; set; }
    bool IsHistorical { get; set; }
}

