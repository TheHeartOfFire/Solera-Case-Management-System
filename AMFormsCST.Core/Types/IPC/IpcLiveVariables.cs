using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Types.IPC;

/// <summary>
/// Snapshot of live Dashboard and Templates variables available for quick-pasting into FormGen, Salesforce, and external tools.
/// </summary>
public class IpcLiveVariables
{
    [JsonPropertyName("serverId")]
    public string ServerId { get; set; } = string.Empty;

    [JsonPropertyName("dealershipName")]
    public string DealershipName { get; set; } = string.Empty;

    [JsonPropertyName("caseNumber")]
    public string CaseNumber { get; set; } = string.Empty;

    [JsonPropertyName("primaryCompanyNumber")]
    public string PrimaryCompanyNumber { get; set; } = string.Empty;

    [JsonPropertyName("notableCompanyNumbers")]
    public string NotableCompanyNumbers { get; set; } = string.Empty;

    [JsonPropertyName("companyNumbers")]
    public List<string> CompanyNumbers { get; set; } = [];

    [JsonPropertyName("contactName")]
    public string ContactName { get; set; } = string.Empty;

    [JsonPropertyName("contactEmail")]
    public string ContactEmail { get; set; } = string.Empty;

    [JsonPropertyName("contactPhone")]
    public string ContactPhone { get; set; } = string.Empty;

    [JsonPropertyName("formName")]
    public string FormName { get; set; } = string.Empty;

    [JsonPropertyName("dealNumber")]
    public string DealNumber { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("activeTemplateName")]
    public string ActiveTemplateName { get; set; } = string.Empty;

    [JsonPropertyName("activeTemplateText")]
    public string ActiveTemplateText { get; set; } = string.Empty;

    [JsonPropertyName("activeCodeSnippetText")]
    public string ActiveCodeSnippetText { get; set; } = string.Empty;

    [JsonPropertyName("activeCodeSnippetOutputType")]
    public string ActiveCodeSnippetOutputType { get; set; } = string.Empty;

    [JsonPropertyName("formGenPreserveUdfTab")]
    public string FormGenPreserveUdfTab { get; set; } = "true";

    [JsonPropertyName("formGenCanvasPasteField")]
    public string FormGenCanvasPasteField { get; set; } = "true";

    [JsonPropertyName("formGenAlreadyOpenToast")]
    public string FormGenAlreadyOpenToast { get; set; } = "true";

    [JsonPropertyName("formGenContextMenus")]
    public string FormGenContextMenus { get; set; } = "true";

    [JsonPropertyName("formGenEditorUndoRedo")]
    public string FormGenEditorUndoRedo { get; set; } = "true";

    [JsonPropertyName("formGenDialogAutoFill")]
    public string FormGenDialogAutoFill { get; set; } = "true";

    [JsonPropertyName("formGenAutoRestoreForms")]
    public string FormGenAutoRestoreForms { get; set; } = "true";

    public Dictionary<string, string> ToDictionary()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(ServerId)] = ServerId,
            [nameof(DealershipName)] = DealershipName,
            [nameof(CaseNumber)] = CaseNumber,
            [nameof(PrimaryCompanyNumber)] = PrimaryCompanyNumber,
            [nameof(NotableCompanyNumbers)] = NotableCompanyNumbers,
            [nameof(CompanyNumbers)] = string.Join(",", CompanyNumbers),
            [nameof(ContactName)] = ContactName,
            [nameof(ContactEmail)] = ContactEmail,
            [nameof(ContactPhone)] = ContactPhone,
            [nameof(FormName)] = FormName,
            [nameof(DealNumber)] = DealNumber,
            [nameof(Notes)] = Notes,
            [nameof(ActiveTemplateName)] = ActiveTemplateName,
            [nameof(ActiveTemplateText)] = ActiveTemplateText,
            [nameof(ActiveCodeSnippetText)] = ActiveCodeSnippetText,
            [nameof(ActiveCodeSnippetOutputType)] = ActiveCodeSnippetOutputType,
            [nameof(FormGenPreserveUdfTab)] = FormGenPreserveUdfTab,
            [nameof(FormGenCanvasPasteField)] = FormGenCanvasPasteField,
            [nameof(FormGenAlreadyOpenToast)] = FormGenAlreadyOpenToast,
            [nameof(FormGenContextMenus)] = FormGenContextMenus,
            [nameof(FormGenEditorUndoRedo)] = FormGenEditorUndoRedo,
            [nameof(FormGenDialogAutoFill)] = FormGenDialogAutoFill,
            [nameof(FormGenAutoRestoreForms)] = FormGenAutoRestoreForms
        };
    }
}

