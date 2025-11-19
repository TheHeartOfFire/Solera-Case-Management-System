using AMFormsCST.Core.Interfaces.BestPractices;
using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Core.Interfaces;
using System.Text.Json.Serialization;
using static AMFormsCST.Core.Interfaces.Notebook.IForm;

namespace AMFormsCST.Core.Types.UserSettings;
public class AutomateFormsOrgVariables : IOrgVariables
{
    private readonly ILogService? _logger;

    private Func<ISupportTool?> _supportToolFactory;
    private readonly Lazy<List<ITextTemplateVariable>> _variables;


    public Dictionary<string, string> LooseVariables { get; set; } =
        new ()
        {
            { "AMMailingName", "Attn: A/M Forms (Sue)" },
            { "AMStreetAddress", "131 Griffis Rd" },
            { "AMCity", "Gloversville" },
            { "AMState", "NY" },
            { "AMZip", "12078" },
            { "AMCityStateZip", "Gloversville, NY 12078" },
            { "AMMailingAddress", "Attn: A/M Forms (Sue)\n131 Griffis Rd\nGloversville, NY 12078" },
        };

    [JsonIgnore]
    public List<ITextTemplateVariable> Variables => _variables.Value;
    [JsonConstructor]
    public AutomateFormsOrgVariables(Func<ISupportTool?> supportToolFactory)
    {
        _supportToolFactory = supportToolFactory;
        _variables = new Lazy<List<ITextTemplateVariable>>(RegisterVariables);
    }

    public AutomateFormsOrgVariables(Func<ISupportTool?> supportToolFactory, ILogService? logger = null)
    {
        _logger = logger;
        _supportToolFactory = supportToolFactory;
        _variables = new Lazy<List<ITextTemplateVariable>>(RegisterVariables);
        _logger?.LogInfo("AutomateFormsOrgVariables initialized.");
    }

    public void InstantiateVariables(ISupportTool? supportTool)
    {
        if (supportTool is null)
        {
            var ex = new ArgumentNullException(nameof(supportTool), "SupportTool cannot be null.");
            _logger?.LogError("Attempted to instantiate variables with null SupportTool.", ex);
            throw ex;
        }
        _supportToolFactory = () => supportTool;
        // The Lazy instance will be re-evaluated on next access
        _logger?.LogInfo("Variables instantiated.");
    }

    #region Variable Registration
    private List<ITextTemplateVariable> RegisterVariables()
    {
        var supportTool = _supportToolFactory?.Invoke();
        if (supportTool is null)
        {
            _logger?.LogWarning("RegisterVariables called with null SupportTool. Returning empty variable list.");
            return [];
        }

        var variables = new List<ITextTemplateVariable>
        {
            new TextTemplateVariable(
             properName: "SelectedDealer:ServerID",
             name: "serverid",
             prefix: "selecteddealer:",
             description: "Server ID#",
             aliases: ["server", "serv", "code", "id"],
             getValue: () =>
             _supportToolFactory()?.Notebook.Notes.SelectedItem?.Dealers.SelectedItem?.ServerCode
             ?? string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedCompany:CompanyCode",
             name: "companycode",
             prefix: "selectedcompany:",
             description: "Company#(s)",
             aliases: ["code"],
             getValue: () =>
             _supportToolFactory()?.Notebook.Notes.SelectedItem?.Dealers.SelectedItem?.Companies.SelectedItem?.CompanyCode
             ?? string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedDealer:Name",
             name: "name",
             prefix: "selecteddealer:",
             description: "Dealership Name",
             aliases: [],
             getValue: () =>
             _supportToolFactory()?.Notebook.Notes.SelectedItem?.Dealers.SelectedItem?.Companies.SelectedItem?.Name
             ?? string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedContact:Name",
             name: "name",
             prefix: "selectedcontact:",
             description: "Contact Name",
             aliases: [],
             getValue: () =>
             _supportToolFactory()?.Notebook.Notes.SelectedItem?.Contacts.SelectedItem?.Name
             ?? string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedContact:EmailAddress",
             name: "emailaddress",
             prefix: "selectedcontact:",
             description: "E-Mail Address",
             aliases: ["email"],
             getValue: () =>
             _supportToolFactory()?.Notebook.Notes.SelectedItem?.Contacts.SelectedItem?.Email
             ?? string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedContact:Phone",
             name: "phone",
             prefix: "selectedcontact:",
             description: "Phone#",
             aliases: [],
             getValue: () =>
             {
                 var contact = _supportToolFactory()?.Notebook.Notes.SelectedItem?.Contacts.SelectedItem;
                 return contact?.Phone +
                 (!string.IsNullOrWhiteSpace(contact?.PhoneExtension) ? $" {contact.PhoneExtensionDelimiter}" : string.Empty) +
                 contact?.PhoneExtension;
             }
            ),
            new TextTemplateVariable(
             properName: "SelectedNote:Notes",
             name: "notes",
             prefix: "selectednote:",
             description: "Notes",
             aliases: [],
             getValue: () =>
             !string.IsNullOrWhiteSpace(_supportToolFactory()?.Notebook.Notes.SelectedItem?.NotesText)
                ? _supportToolFactory()!.Notebook.Notes.SelectedItem!.NotesText
                : string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedNote:CaseNumber",
             name: "casenumber",
             prefix: "selectednote:",
             description: "Case#",
             aliases: ["caseno", "case"],
             getValue: () =>
             _supportToolFactory()?.Notebook.Notes.SelectedItem?.CaseText
             ?? string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedNote:Forms",
             name: "forms",
             prefix: "selectednote:",
             description: "All Forms",
             aliases: ["form"],
             getValue: () =>
             ">" +
             string.Join("\n>", _supportToolFactory()?.Notebook.Notes.SelectedItem?
                .Forms.Where(f => !string.IsNullOrEmpty(f.Name)).Select(f => f.Name) ?? [])
            ),
            new TextTemplateVariable(
             properName: "SelectedNote:NotableForms",
             name: "notableforms",
             prefix: "selectednote:",
             description: "Notable Forms",
             aliases: ["notable"],
             getValue: () =>
             ">" +
             string.Join("\n>", _supportToolFactory()?.Notebook.Notes.SelectedItem?
                .Forms.Where(f => f.Notable).Where(f => !string.IsNullOrEmpty(f.Name)).Select(f => f.Name) ?? [])
            ),
            new TextTemplateVariable(
             properName: "SelectedContact:FirstName",
             name: "firstname",
             prefix: "selectedcontact:",
             description: "First Name",
             aliases: [],
             getValue: () =>
             {
                var contactName = _supportToolFactory()?.Notebook?.Notes.SelectedItem?.Contacts.SelectedItem?.Name;
                if (string.IsNullOrEmpty(contactName)) return string.Empty;
                var spaceIndex = contactName.IndexOf(' ');
                return spaceIndex > -1 ? contactName[..spaceIndex] : contactName;
            }
            ),
            new TextTemplateVariable(
             properName: "AMMail:FullAddress",
             name: "fulladdress",
             prefix: "ammail:",
             description: "AutoMate Forms Mailing Address",
             aliases: ["all", "full", "mailingaddress", "mailto"],
             getValue: () =>
             LooseVariables.TryGetValue("AMMailingAddress", out var address) ? address : string.Empty
            ),
            new TextTemplateVariable(
             properName: "AMMail:Name",
             name: "name",
             prefix: "ammail:",
             description: "AutoMate Forms Mailing Address - Name",
             aliases: [],
             getValue: () =>
             LooseVariables.TryGetValue("AMMailingName", out var address) ? address : string.Empty
            ),
            new TextTemplateVariable(
             properName: "AMMail:Street",
             name: "streetaddress",
             prefix: "ammail:",
             description: "AutoMate Forms Mailing Address - Street Address",
             aliases: ["street", "line1"],
             getValue: () =>
             LooseVariables.TryGetValue("AMStreetAddress", out var address) ? address : string.Empty
            ),
            new TextTemplateVariable(
             properName: "AMMail:City",
             name: "city",
             prefix: "ammail:",
             description: "AutoMate Forms Mailing Address - City",
             aliases: [],
             getValue: () =>
             LooseVariables.TryGetValue("AMCity", out var address) ? address : string.Empty
            ),
            new TextTemplateVariable(
             properName: "AMMail:State",
             name: "state",
             prefix: "ammail:",
             description: "AutoMate Forms Mailing Address - State",
             aliases: [],
             getValue: () =>
             LooseVariables.TryGetValue("AMState", out var address) ? address : string.Empty
            ),
            new TextTemplateVariable(
             properName: "AMMail:ZipCode",
             name: "zipcode",
             prefix: "ammail:",
             description: "AutoMate Forms Mailing Address - Zip Code",
             aliases: ["postalcode", "zip"],
             getValue: () =>
             LooseVariables.TryGetValue("AMZip", out var address) ? address : string.Empty
            ),
            new TextTemplateVariable(
             properName: "AMMail:CSZ",
             name: "csz",
             prefix: "ammail:",
             description: "AutoMate Forms Mailing Address - City, State Zip",
             aliases: ["csz", "line2"],
             getValue: () =>
             LooseVariables.TryGetValue("AMCityStateZip", out var address) ? address : string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedTestDeal:DealNumber",
             name: "dealnumber",
             prefix: "selectedtestdeal:",
             description: "Test Deal#",
             aliases: ["dealno", "deal"],
             getValue: () =>
             _supportToolFactory()?.Notebook.Notes.SelectedItem?.Forms.SelectedItem?.TestDeals.SelectedItem?.DealNumber
             ?? string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedForm:Notes",
             name: "notes",
             prefix: "selectedform:",
             description: "Selected form notes",
             aliases: [],
             getValue: () =>
             _supportToolFactory()?.Notebook.Notes.SelectedItem?.Forms.SelectedItem?.Notes
             ?? string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedForm:Name",
             name: "name",
             prefix: "selectedform:",
             description: "Selected form name",
             aliases: [],
             getValue: () =>
             _supportToolFactory()?.Notebook.Notes.SelectedItem?.Forms.SelectedItem?.Name
             ?? string.Empty
            ),
            new TextTemplateVariable(
             properName: "SelectedNote:SummarizeNotableForms",
             name: "summarizenotableforms",
             prefix: "selectednote:",
             description: "Names and notes for notable forms",
             aliases: [],
             getValue: () =>
             string.Join("\n\n", _supportToolFactory()?.Notebook.Notes.SelectedItem?
                .Forms.Where(f => f.Notable).Where(f => !string.IsNullOrEmpty(f.Name)).Select(f => "Name: " + f.Name +"\nFormat: " + f.Format.ToString() + "\nTest Deals: " + string.Join(", ", f.TestDeals.Select(td => td.DealNumber)) + "\nNotes: " + f.Notes) ?? [])
            ),
            new TextTemplateVariable(
             properName: "SelectedForm:Type",
             name: "formtype",
             prefix: "selectedform:",
             description: "Selected form type",
             aliases: [],
             getValue: () =>
             _supportToolFactory()?.Notebook.Notes.SelectedItem?.Forms.SelectedItem?.Format.ToString() ?? FormFormat.Pdf.ToString()
            ),
            new TextTemplateVariable(
             properName: "User:Input",
             name: "input",
             prefix: "user:",
             description: "User Input - No value",
             aliases: [],
             getValue: () => "[User Input]"
            ),
            new TextTemplateVariable(
             properName: "SelectedNote:NotableDealersAndCompanies",
             name: "selectednote",
             prefix: "notabledealersandcompanies:",
             description: "Notable dealers and companies in d1_1,2, d2_3,4 format.",
             aliases: ["serversandcompanies"],
             getValue: () =>
             {
                var dealers = _supportToolFactory()?.Notebook?.Notes.SelectedItem?.Dealers;
                 foreach(var dealer in dealers?
                 .Where(d => d.Notable)
                 .Where(d => !string.IsNullOrWhiteSpace(d.ServerCode)) ?? [])
                 {
                    var companies = dealer.Companies
                     .Where(c => c.Notable)
                     .Where(c => !string.IsNullOrEmpty(c.CompanyCode));
                    if (companies.Any())
                    {
                        return string.Join(", ", dealers
                            .Where(d => d.Notable)
                            .Where(d => !string.IsNullOrWhiteSpace(d.ServerCode))
                            .Select(d => $"{d.ServerCode}_{string.Join(',', d.Companies
                                .Where(c => c.Notable)
                                .Where(c => !string.IsNullOrEmpty(c.CompanyCode))
                                .Select(c => c.CompanyCode))}"));
                    }
                 }
                 return string.Empty;

             }
             
            ),
            new TextTemplateVariable(
             properName: "SelectedDealer:NotableCompanies",
             name: "notablecompanies",
             prefix: "selecteddealer:",
             description: "Notable companies for selected dealer",
             aliases: ["companies"],
             getValue: () =>
             string.Join(',', _supportToolFactory()?.Notebook.Notes.SelectedItem?.Dealers.SelectedItem?.Companies.Where(c => c.Notable).Where(c => !string.IsNullOrEmpty(c.CompanyCode)).Select(c => c.CompanyCode) ?? []))
        };

        _logger?.LogInfo($"Registered {variables.Count} text template variables.");
        return variables;
    }
    #endregion
}
