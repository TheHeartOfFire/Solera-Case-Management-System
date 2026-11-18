using AMFormsCST.Core.Interfaces.Notebook;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AMFormsCST.Core.Utils;

public enum CaseTargetStatus
{
    Resolved,
    Closed
}

public class CaseValidationRequest
{
    public INote? Note { get; set; }
    public CaseTargetStatus TargetStatus { get; set; } = CaseTargetStatus.Closed;
    public string ParentProduct { get; set; } = string.Empty;
    public string CaseReason { get; set; } = string.Empty;
    public string SubCategory { get; set; } = string.Empty;
    public string SupportCategory { get; set; } = string.Empty;
    public string ClosureComment { get; set; } = string.Empty;
}

public class CaseValidationIssue
{
    public string FieldName { get; }
    public string Message { get; }
    public bool IsWarningOnly { get; }

    public CaseValidationIssue(string fieldName, string message, bool isWarningOnly = false)
    {
        FieldName = fieldName;
        Message = message;
        IsWarningOnly = isWarningOnly;
    }
}

public class CaseValidationResult
{
    public List<CaseValidationIssue> Issues { get; } = [];
    public string? FallbackEmailFromDescription { get; set; }
    public bool IsValid => Issues.All(i => i.IsWarningOnly);
}

/// <summary>
/// Validates that a case has all required Salesforce fields, comments, and contact email (when Resolved).
/// </summary>
public static partial class CaseClosureValidator
{
    [GeneratedRegex(@"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    public static CaseValidationResult Validate(CaseValidationRequest request)
    {
        var result = new CaseValidationResult();
        var note = request.Note;

        var primaryDealer = note?.Dealers?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(primaryDealer?.Name))
        {
            result.Issues.Add(new CaseValidationIssue("Account Name", "Account / Dealership Name is required."));
        }

        var primaryContact = note?.Contacts?.FirstOrDefault(c => c.IsPrimary) ?? note?.Contacts?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(primaryContact?.Name))
        {
            result.Issues.Add(new CaseValidationIssue("Contact Name", "Primary Contact Name is required."));
        }

        if (string.IsNullOrWhiteSpace(request.ParentProduct))
        {
            result.Issues.Add(new CaseValidationIssue("Parent Product", "Parent Product is required."));
        }

        if (string.IsNullOrWhiteSpace(request.CaseReason))
        {
            result.Issues.Add(new CaseValidationIssue("Case Reason", "Case Reason is required."));
        }

        if (string.IsNullOrWhiteSpace(request.SubCategory))
        {
            result.Issues.Add(new CaseValidationIssue("Sub-Category", "Sub-Category is required."));
        }

        if (string.IsNullOrWhiteSpace(request.SupportCategory))
        {
            result.Issues.Add(new CaseValidationIssue("Support Category", "Support Category is required."));
        }

        var hasInternalLog = (note?.LogEntries?.Any(e => e.IncludeInOutput) ?? false) ||
                             !string.IsNullOrWhiteSpace(note?.NotesText);
        if (!hasInternalLog)
        {
            result.Issues.Add(new CaseValidationIssue("Internal Comment (Log)", "At least 1 internal comment detailing the events of the case is required."));
        }

        if (string.IsNullOrWhiteSpace(request.ClosureComment))
        {
            result.Issues.Add(new CaseValidationIssue("Closure Comment", "A closure comment is required."));
        }

        if (request.TargetStatus == CaseTargetStatus.Resolved)
        {
            var contactEmail = primaryContact?.Email?.Trim() ?? string.Empty;
            var hasValidContactEmail = !string.IsNullOrWhiteSpace(contactEmail) && EmailRegex().IsMatch(contactEmail);
            if (!hasValidContactEmail)
            {
                var descEmail = ExtractEmail(note?.NotesText);
                if (!string.IsNullOrWhiteSpace(descEmail))
                {
                    result.FallbackEmailFromDescription = descEmail;
                    result.Issues.Add(new CaseValidationIssue(
                        "Contact Email",
                        $"Contact email is missing, but '{descEmail}' was found in Description/Notes. Apply it to the contact record before resolving.",
                        isWarningOnly: false));
                }
                else
                {
                    result.Issues.Add(new CaseValidationIssue(
                        "Contact Email",
                        "Resolved cases require a valid Contact email address. Look up the contact record in AMPS (Auto/Mate) and add their email."));
                }
            }
        }

        return result;
    }

    public static string? ExtractEmail(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = EmailRegex().Match(text);
        return match.Success ? match.Value : null;
    }
}

