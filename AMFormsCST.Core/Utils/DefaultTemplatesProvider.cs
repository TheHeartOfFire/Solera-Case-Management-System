using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using System.Collections.Generic;
using System.Security;

namespace AMFormsCST.Core.Utils;

/// <summary>
/// Provides default closure, resolved, and duplicate-case templates.
/// </summary>
public static class DefaultTemplatesProvider
{
    private static string WrapAsFlowDocumentXaml(string plainText)
    {
        var lines = plainText.Replace("\r\n", "\n").Split('\n');
        var paragraphs = string.Join("", System.Linq.Enumerable.Select(lines, line =>
            $"<Paragraph>{SecurityElement.Escape(line)}</Paragraph>"));
        return $"<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">{paragraphs}</FlowDocument>";
    }

    public static List<TextTemplate> GetDefaultClosureTemplates()
    {
        return
        [
            new TextTemplate(
                "Closure - Solved (Work Complete)",
                "Corporate closure template when work is complete and no further customer follow-up is expected.",
                WrapAsFlowDocumentXaml(
                    "Hi {Contact:FirstName}!\n" +
                    "We're happy to let you know your ticket is resolved and closed. If you get a survey and we didn't earn a perfect score, please give us a chance to improve. For more help, reach out and reference case #{CaseNumber}.\n" +
                    "{Signature}"),
                TextTemplate.TemplateType.ClosureComments,
                TextTemplate.TemplateCategoryKind.CompleteTemplate,
                ["#Closure", "#Solved"]),

            new TextTemplate(
                "Closure - Unresolved (Dealer Unresponsive)",
                "Closure template when a case is closed unresolved due to dealer unresponsiveness.",
                WrapAsFlowDocumentXaml(
                    "Hi {Contact:FirstName},\n" +
                    "We have attempted to reach out regarding Case #{CaseNumber} but have not received a response. Because we have not heard back, we are closing this case as unresolved at this time. If you still need assistance, please reply to this email or open a new case referencing #{CaseNumber} and we will be happy to pick back up where we left off.\n" +
                    "{Signature}"),
                TextTemplate.TemplateType.ClosureComments,
                TextTemplate.TemplateCategoryKind.CompleteTemplate,
                ["#Closure", "#Unresolved"]),

            new TextTemplate(
                "Resolved - Work Complete",
                "Resolved status template when work has been completed and is awaiting dealer confirmation.",
                WrapAsFlowDocumentXaml(
                    "Hi {Contact:FirstName},\n" +
                    "We have completed the requested work for Case #{CaseNumber} and marked the case as Resolved. Please review and test the updates on your end and let us know if everything looks good or if any further adjustments are needed.\n" +
                    "{Signature}"),
                TextTemplate.TemplateType.ClosureComments,
                TextTemplate.TemplateCategoryKind.CompleteTemplate,
                ["#Closure", "#Resolved"]),

            new TextTemplate(
                "Resolved - Dealer Unresponsive",
                "Resolved status template when the dealer has been unresponsive.",
                WrapAsFlowDocumentXaml(
                    "Hi {Contact:FirstName},\n" +
                    "We have reached out several times regarding Case #{CaseNumber} without a response, so we are marking this case as Resolved for now. Please let us know if you still need assistance or have any questions, and reference case #{CaseNumber}.\n" +
                    "{Signature}"),
                TextTemplate.TemplateType.ClosureComments,
                TextTemplate.TemplateCategoryKind.CompleteTemplate,
                ["#Closure", "#Resolved", "#Unresponsive"]),

            new TextTemplate(
                "Duplicate Closure - Primary Case Complete",
                "Closure note for a duplicate/linked case when the primary case is complete.",
                WrapAsFlowDocumentXaml("This case is a duplicate of Case# {PrimaryCaseNo} which is complete."),
                TextTemplate.TemplateType.ClosureComments,
                TextTemplate.TemplateCategoryKind.CompleteTemplate,
                ["#Closure", "#Duplicate"]),

            new TextTemplate(
                "Duplicate Closure - Primary Case Incomplete",
                "Closure note for a duplicate/linked case when the primary case is still in progress.",
                WrapAsFlowDocumentXaml("This case is a duplicate of Case# {PrimaryCaseNo} which is in progress."),
                TextTemplate.TemplateType.ClosureComments,
                TextTemplate.TemplateCategoryKind.CompleteTemplate,
                ["#Closure", "#Duplicate"]),

            new TextTemplate(
                "Duplicate Closure - Primary Case Unresponsive",
                "Closure note for a duplicate/linked case when the primary case is in progress / awaiting customer response.",
                WrapAsFlowDocumentXaml("This case is a duplicate of Case# {PrimaryCaseNo} which is in progress. Please check your email for communications from that case."),
                TextTemplate.TemplateType.ClosureComments,
                TextTemplate.TemplateCategoryKind.CompleteTemplate,
                ["#Closure", "#Duplicate"])
        ];
    }
}

