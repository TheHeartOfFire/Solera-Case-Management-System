using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Core.Types.Notebook;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Documents;
using System.Windows.Markup;

namespace AMFormsCST.Core.Utils;

public class TemplateAssemblyService : ITemplateAssemblyService
{
    public string AssembleInternalNote(
        INote note,
        IEnumerable<ICaseLogEntry>? selectedEntries = null,
        TextTemplate? headerPartial = null,
        TextTemplate? footerPartial = null,
        AMFormsCST.Core.Interfaces.UserSettings.IUserSettings? userSettings = null,
        IEnumerable<TextTemplate>? actionPartials = null)
    {
        var entries = (selectedEntries ?? note.LogEntries?.Where(e => e.IncludeInOutput) ?? []).ToList();
        var partials = actionPartials?.Where(t => t.Kind == TextTemplate.TemplateCategoryKind.ActionPartial).ToList();
        var primaryContact = note.Contacts?.FirstOrDefault(c => c.IsPrimary) ?? note.Contacts?.FirstOrDefault();
        var primaryDealer = note.Dealers?.FirstOrDefault();
        var caseNumber = GetCaseNumber(note);
        var serverId = primaryDealer?.ServerCode ?? string.Empty;
        var dealerName = primaryDealer?.Name ?? string.Empty;

        var sb = new StringBuilder();

        // 1. Header
        if (headerPartial != null && !string.IsNullOrWhiteSpace(headerPartial.TextXaml))
        {
            var headerText = ExtractPlainText(headerPartial.TextXaml);
            sb.AppendLine(ReplaceBasicTokens(headerText, note));
        }
        else
        {
            sb.AppendLine("=== SCMT Work Log ===");
            var metaParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(caseNumber)) metaParts.Add($"Case: #{caseNumber}");
            if (!string.IsNullOrWhiteSpace(serverId)) metaParts.Add($"Server: {serverId}");
            if (!string.IsNullOrWhiteSpace(dealerName)) metaParts.Add($"Dealer: {dealerName}");
            if (metaParts.Count > 0)
            {
                sb.AppendLine(string.Join(" | ", metaParts));
            }

            if (note.LinkedCases != null && note.LinkedCases.Count > 0)
            {
                sb.AppendLine($"Linked Cases: {string.Join(", ", note.LinkedCases.Select(c => $"#{c}"))}");
            }

            if (note.Contacts != null && note.Contacts.Count > 0)
            {
                var contactSummaries = note.Contacts
                    .Where(c => !string.IsNullOrWhiteSpace(c.Name))
                    .Select(c =>
                    {
                        var role = !string.IsNullOrWhiteSpace(c.Role) ? $" ({c.Role})" : "";
                        var noteText = !string.IsNullOrWhiteSpace(c.Notes) ? $" - {c.Notes}" : "";
                        return $"{c.Name}{role}{noteText}";
                    });
                var joined = string.Join(" | ", contactSummaries);
                if (!string.IsNullOrWhiteSpace(joined))
                {
                    sb.AppendLine($"Contacts: {joined}");
                }
            }
        }

        // 2. Dealer Request / Misc Notes
        if (!string.IsNullOrWhiteSpace(note.NotesText))
        {
            sb.AppendLine($"Dealer Request: {note.NotesText.Trim()}");
        }

        // 3. Action Entries
        if (entries.Count > 0)
        {
            sb.AppendLine();
            foreach (var entry in entries)
            {
                var matchedPartial = FindMatchingActionPartial(entry, partials);
                sb.AppendLine(ResolveActionBlock(entry, matchedPartial));
            }
        }

        // 3. Footer
        if (footerPartial != null && !string.IsNullOrWhiteSpace(footerPartial.TextXaml))
        {
            var footerText = ExtractPlainText(footerPartial.TextXaml);
            sb.AppendLine(ReplaceBasicTokens(footerText, note));
        }
        else
        {
            sb.AppendLine("====================");
        }

        return sb.ToString().TrimEnd();
    }

    public string AssembleDealerEmail(
        INote note,
        IEnumerable<ICaseLogEntry>? selectedEntries = null,
        TextTemplate? headerPartial = null,
        TextTemplate? footerPartial = null,
        AMFormsCST.Core.Interfaces.UserSettings.IUserSettings? userSettings = null,
        IEnumerable<TextTemplate>? actionPartials = null)
    {
        var entries = (selectedEntries ?? note.LogEntries?.Where(e => e.IncludeInOutput) ?? []).ToList();
        var partials = actionPartials?.Where(t => t.Kind == TextTemplate.TemplateCategoryKind.ActionPartial).ToList();
        var primaryContact = note.Contacts?.FirstOrDefault(c => c.IsPrimary) ?? note.Contacts?.FirstOrDefault();
        var firstName = GetFirstName(primaryContact?.Name);

        var sb = new StringBuilder();

        // 1. Greeting
        if (headerPartial != null && !string.IsNullOrWhiteSpace(headerPartial.TextXaml))
        {
            var headerText = ExtractPlainText(headerPartial.TextXaml);
            sb.AppendLine(ReplaceBasicTokens(headerText, note, userSettings));
        }
        else
        {
            sb.AppendLine($"Hello {firstName},");
            sb.AppendLine();
            sb.AppendLine("I have completed the requested updates for your dealership group:");
        }

        // 2. Action Bullets
        sb.AppendLine();
        if (entries.Count > 0)
        {
            foreach (var entry in entries)
            {
                var storeSummary = FormatStoreSummary(entry.Stores);
                var actionLabel = GetEntryActionLabel(entry);
                var customFlag = entry.IsCustomCopy ? " (Custom Copy)" : "";
                var formName = !string.IsNullOrWhiteSpace(entry.FormName) ? entry.FormName : "Form";

                if (!string.IsNullOrWhiteSpace(storeSummary))
                {
                    sb.AppendLine($"• Store(s) {storeSummary}: [{actionLabel}] {formName}{customFlag}");
                }
                else
                {
                    sb.AppendLine($"• [{actionLabel}] {formName}{customFlag}");
                }

                if (entry.Deals != null && entry.Deals.Count > 0)
                {
                    var dealsStr = string.Join(", ", entry.Deals.Select(d => d.DisplayText));
                    sb.AppendLine($"  - Tested on deal(s): {dealsStr}");
                }

                if (!string.IsNullOrWhiteSpace(entry.Details))
                {
                    sb.AppendLine($"  - Details: {entry.Details}");
                }
            }
        }
        else
        {
            sb.AppendLine("• Completed form programming updates as requested.");
        }

        // 3. Sign-off
        sb.AppendLine();
        if (footerPartial != null && !string.IsNullOrWhiteSpace(footerPartial.TextXaml))
        {
            var footerText = ExtractPlainText(footerPartial.TextXaml);
            sb.AppendLine(ReplaceBasicTokens(footerText, note, userSettings));
        }
        else
        {
            sb.AppendLine("Please test each of your forms and let me know if further adjustments are needed.");
            sb.AppendLine();
            sb.AppendLine(UserSignatureFormatter.FormatSignature(userSettings));
        }

        return sb.ToString().TrimEnd();
    }

    public string AssemblePublishNote(INote note, ICaseLogEntry? entry = null, IEnumerable<ICaseLogEntry>? formEntries = null)
    {
        var caseNumber = GetCaseNumber(note);
        var entries = formEntries?.Where(e => e.IncludeInOutput).ToList();

        if (entries != null && entries.Count > 1)
        {
            var isCustom = entries.Any(e => e.IsCustomCopy);
            var customPrefix = isCustom ? "***CUSTOMIZED*** " : "";
            var allTags = entries
                .Select(GetEntryActionLabel)
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var combinedActions = string.Join(" + ", allTags);
            var allStores = entries
                .SelectMany(e => e.Stores ?? [])
                .GroupBy(s => s.CompanyCode, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
            var storeSuffix = allStores.Count > 0 ? $" (Stores: {FormatStoreSummary(allStores)})" : "";
            var detailsList = entries
                .Select(e => e.Details?.Trim())
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct();
            var detailsSuffix = detailsList.Any() ? $" - {string.Join("; ", detailsList)}" : "";

            return $"{customPrefix}{combinedActions} per Case #{caseNumber}{storeSuffix}{detailsSuffix}".Trim();
        }

        var targetEntry = entries?.FirstOrDefault() ?? entry ?? note.LogEntries?.LastOrDefault(e => e.IncludeInOutput);

        if (targetEntry != null)
        {
            var actionLabel = GetEntryActionLabel(targetEntry);
            var customPrefix = targetEntry.IsCustomCopy ? "***CUSTOMIZED*** " : "";
            var detailsSuffix = !string.IsNullOrWhiteSpace(targetEntry.Details) ? $" - {targetEntry.Details}" : "";
            var storeSuffix = targetEntry.Stores.Count > 0 ? $" (Stores: {FormatStoreSummary(targetEntry.Stores)})" : "";

            return $"{customPrefix}{actionLabel} per Case #{caseNumber}{storeSuffix}{detailsSuffix}".Trim();
        }

        var formName = note.Forms?.FirstOrDefault(f => f.Notable)?.Name ?? note.Forms?.FirstOrDefault()?.Name ?? "Form";
        return $"{formName} per Case #{caseNumber}".Trim();
    }

    public string AssembleClosureRecap(INote note, TextTemplate? closureTemplate = null, AMFormsCST.Core.Interfaces.UserSettings.IUserSettings? userSettings = null)
    {
        var primaryContact = note.Contacts?.FirstOrDefault(c => c.IsPrimary) ?? note.Contacts?.FirstOrDefault();
        var firstName = GetFirstName(primaryContact?.Name);
        var caseNumber = GetCaseNumber(note);

        if (closureTemplate != null && !string.IsNullOrWhiteSpace(closureTemplate.TextXaml))
        {
            var templateText = ExtractPlainText(closureTemplate.TextXaml);
            return ReplaceBasicTokens(templateText, note, userSettings);
        }

        return $"Hi {firstName}!\nWe're happy to let you know your ticket is resolved and closed. If you get a survey and we didn’t earn a perfect score, please give us a chance to improve. For more help, reach out and reference case #{caseNumber}.\nThank you! Auto/Mate Forms";
    }

    public string AssembleLinkedCasesNote(INote note, string primaryCaseNumber)
    {
        var primaryContact = note.Contacts?.FirstOrDefault(c => c.IsPrimary) ?? note.Contacts?.FirstOrDefault();
        var firstName = GetFirstName(primaryContact?.Name);

        return $"Hello {firstName},\n\nThis request was consolidated and completed under primary case #{primaryCaseNumber}. All requested updates are now active for your dealership.\n\nThank You,\nAuto/Mate Forms";
    }

    public string ResolveActionBlock(ICaseLogEntry entry, TextTemplate? actionPartial = null)
    {
        var actionLabel = GetEntryActionLabel(entry);
        var customTag = entry.IsCustomCopy ? " (Custom Copy)" : "";
        var form = !string.IsNullOrWhiteSpace(entry.FormName) ? entry.FormName : "Form";
        var stores = entry.Stores != null && entry.Stores.Count > 0 ? FormatStoreSummary(entry.Stores) : "";
        var deals = entry.Deals != null && entry.Deals.Count > 0 ? string.Join(", ", entry.Deals.Select(d => d.DisplayText)) : "";

        if (actionPartial != null && !string.IsNullOrWhiteSpace(actionPartial.TextXaml))
        {
            var text = ExtractPlainText(actionPartial.TextXaml);
            return text
                .Replace("{FormName}", form, StringComparison.OrdinalIgnoreCase)
                .Replace("{Action}", actionLabel, StringComparison.OrdinalIgnoreCase)
                .Replace("{CustomFlag}", customTag, StringComparison.OrdinalIgnoreCase)
                .Replace("{StoreList}", stores, StringComparison.OrdinalIgnoreCase)
                .Replace("{Stores}", stores, StringComparison.OrdinalIgnoreCase)
                .Replace("{DealNumber}", deals, StringComparison.OrdinalIgnoreCase)
                .Replace("{AdjustmentsSummary}", entry.Details ?? "", StringComparison.OrdinalIgnoreCase)
                .Replace("{Details}", entry.Details ?? "", StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        var sb = new StringBuilder();
        var customTagBracket = entry.IsCustomCopy ? " [Custom Copy]" : "";
        sb.Append($"• [{actionLabel}] {form}{customTagBracket}");

        if (entry.Stores != null && entry.Stores.Count > 0)
        {
            sb.Append($" -> Stores: {FormatStoreSummary(entry.Stores)}");
        }

        sb.AppendLine();

        if (entry.Deals != null && entry.Deals.Count > 0)
        {
            sb.AppendLine($"  - Deals: {deals}");
        }

        if (!string.IsNullOrWhiteSpace(entry.Details))
        {
            sb.AppendLine($"  - Details: {entry.Details}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetCaseNumber(INote note)
    {
        if (string.IsNullOrWhiteSpace(note.CaseText)) return string.Empty;
        var lines = note.CaseText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        var idx = Array.FindIndex(lines, l => l.Trim().Equals("Case Number", StringComparison.OrdinalIgnoreCase) || l.Trim().Equals("Case Number:", StringComparison.OrdinalIgnoreCase));
        if (idx != -1 && idx + 1 < lines.Length)
        {
            return lines[idx + 1].Trim();
        }
        var inline = lines.FirstOrDefault(l => l.Trim().StartsWith("Case Number:", StringComparison.OrdinalIgnoreCase));
        if (inline != null)
        {
            return inline.Substring(inline.IndexOf(':') + 1).Trim();
        }
        return note.CaseText.Trim();
    }

    private static string GetFirstName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return "there";
        var parts = fullName.Trim().Split(' ');
        return parts.Length > 0 ? parts[0] : fullName;
    }

    private static string FormatStoreSummary(List<CaseStoreAssignment> stores)
    {
        if (stores == null || stores.Count == 0) return string.Empty;
        return string.Join(", ", stores.Select(s => s.DisplayText));
    }

    private static TextTemplate? FindMatchingActionPartial(ICaseLogEntry entry, List<TextTemplate>? partials)
    {
        if (partials == null || partials.Count == 0) return null;

        if (entry.Tags != null && entry.Tags.Count > 0)
        {
            foreach (var entryTag in entry.Tags)
            {
                var normalizedEntryTag = entryTag.Trim().TrimStart('#');
                var match = partials.FirstOrDefault(p =>
                    p.Tags != null && p.Tags.Any(pt =>
                        string.Equals(pt.Trim().TrimStart('#'), normalizedEntryTag, StringComparison.OrdinalIgnoreCase)));
                if (match != null) return match;
            }
        }

        return partials.FirstOrDefault(p => p.AssociatedAction == entry.Action);
    }

    private static string GetEntryActionLabel(ICaseLogEntry entry)
    {
        if (entry.Tags != null && entry.Tags.Count > 0)
        {
            return string.Join(" ", entry.Tags);
        }
        return GetActionDisplayName(entry.Action);
    }

    private static string GetActionDisplayName(CaseActionType action)
    {
        return action switch
        {
            CaseActionType.NewFormPublish => "New form publish",
            CaseActionType.ProgrammingUpdate => "Programming update",
            CaseActionType.LineAdjustments => "Line Adjustments",
            CaseActionType.Investigation => "Investigation",
            CaseActionType.Republish => "Republish",
            CaseActionType.CustomNote => "Note",
            _ => action.ToString()
        };
    }

    private static string ReplaceBasicTokens(string templateText, INote note, AMFormsCST.Core.Interfaces.UserSettings.IUserSettings? userSettings = null)
    {
        var primaryContact = note.Contacts?.FirstOrDefault(c => c.IsPrimary) ?? note.Contacts?.FirstOrDefault();
        var primaryDealer = note.Dealers?.FirstOrDefault();
        var caseNumber = GetCaseNumber(note);

        return templateText
            .Replace("{CaseNumber}", caseNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{PrimaryCaseNo}", caseNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("SelectedNote:CaseNumber", caseNumber, StringComparison.OrdinalIgnoreCase)
            .Replace("{Contact:FirstName}", GetFirstName(primaryContact?.Name), StringComparison.OrdinalIgnoreCase)
            .Replace("SelectedContact:FirstName", GetFirstName(primaryContact?.Name), StringComparison.OrdinalIgnoreCase)
            .Replace("{Contact:Name}", primaryContact?.Name ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("SelectedContact:Name", primaryContact?.Name ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{Contact:Email}", primaryContact?.Email ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("SelectedContact:EmailAddress", primaryContact?.Email ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{Dealer:Name}", primaryDealer?.Name ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("SelectedDealer:Name", primaryDealer?.Name ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{ServerID}", primaryDealer?.ServerCode ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("SelectedDealer:ServerID", primaryDealer?.ServerCode ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{DealerRequest}", note.NotesText ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{Notes}", note.NotesText ?? "", StringComparison.OrdinalIgnoreCase)
            .Replace("{Signature}", UserSignatureFormatter.FormatSignature(userSettings), StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractPlainText(string textXaml)
    {
        if (string.IsNullOrWhiteSpace(textXaml)) return string.Empty;
        try
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(textXaml));
            if (XamlReader.Load(stream) is FlowDocument doc)
            {
                var textRange = new TextRange(doc.ContentStart, doc.ContentEnd);
                return textRange.Text.Trim();
            }
        }
        catch { }
        return textXaml.Trim();
    }
}

