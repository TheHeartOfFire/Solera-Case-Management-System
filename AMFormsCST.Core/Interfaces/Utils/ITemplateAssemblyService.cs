using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using System.Collections.Generic;

namespace AMFormsCST.Core.Interfaces.Utils;

public interface ITemplateAssemblyService
{
    string AssembleInternalNote(INote note, IEnumerable<ICaseLogEntry>? selectedEntries = null, TextTemplate? headerPartial = null, TextTemplate? footerPartial = null, IUserSettings? userSettings = null, IEnumerable<TextTemplate>? actionPartials = null);
    string AssembleDealerEmail(INote note, IEnumerable<ICaseLogEntry>? selectedEntries = null, TextTemplate? headerPartial = null, TextTemplate? footerPartial = null, IUserSettings? userSettings = null, IEnumerable<TextTemplate>? actionPartials = null);
    string AssemblePublishNote(INote note, ICaseLogEntry? entry = null, IEnumerable<ICaseLogEntry>? formEntries = null);
    string AssembleClosureRecap(INote note, TextTemplate? closureTemplate = null, IUserSettings? userSettings = null);
    string AssembleLinkedCasesNote(INote note, string primaryCaseNumber);
    string ResolveActionBlock(ICaseLogEntry entry, TextTemplate? actionPartial = null);
}

