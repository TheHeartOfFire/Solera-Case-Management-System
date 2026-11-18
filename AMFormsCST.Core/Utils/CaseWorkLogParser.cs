using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Types.Notebook;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AMFormsCST.Core.Utils;

public static class CaseWorkLogParser
{
    private static readonly Regex ActionLineRegex = new(
        @"•\s*\[(?<action>[^\]]+)\]\s*(?<form>.+?)(?:\s*(?:\[|\()Custom(?:\s*Copy)?(?:\]|\)))?(?:\s*(?:->|for)\s*Stores?:\s*(?<stores>[^\r\n]+))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DealsLineRegex = new(
        @"-\s*(?:Deals?|Tested with deals?):\s*(?<deals>[^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DetailsLineRegex = new(
        @"-\s*(?:Details?|Changes?):\s*(?<details>[^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LinkedCasesRegex = new(
        @"Linked Cases:\s*(?<cases>[^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static List<ICaseLogEntry> ParseWorkLogEntries(string rawText, bool markAsHistorical = true)
    {
        var result = new List<ICaseLogEntry>();
        if (string.IsNullOrWhiteSpace(rawText)) return result;

        var lines = rawText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        CaseLogEntry? currentEntry = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            var actionMatch = ActionLineRegex.Match(line);
            if (actionMatch.Success)
            {
                if (currentEntry != null)
                {
                    result.Add(currentEntry);
                }

                var actionStr = actionMatch.Groups["action"].Value.Trim();
                var formStr = actionMatch.Groups["form"].Value.Trim();
                var storesStr = actionMatch.Groups["stores"].Value.Trim();
                var isCustom = line.Contains("Custom", StringComparison.OrdinalIgnoreCase);

                var parenMatch = Regex.Match(formStr, @"\((?<inner>[^\)]*?(?:Store|Custom)[^\)]*?)\)$", RegexOptions.IgnoreCase);
                if (parenMatch.Success)
                {
                    var inner = parenMatch.Groups["inner"].Value.Trim();
                    if (inner.Contains("Custom", StringComparison.OrdinalIgnoreCase))
                    {
                        isCustom = true;
                        inner = Regex.Replace(inner, @"-?\s*Custom(?:\s*Copy)?", "", RegexOptions.IgnoreCase).Trim(' ', '-');
                    }
                    if (inner.StartsWith("Store", StringComparison.OrdinalIgnoreCase))
                    {
                        inner = Regex.Replace(inner, @"^Stores?\s*", "", RegexOptions.IgnoreCase).Trim();
                    }
                    if (!string.IsNullOrWhiteSpace(inner) && string.IsNullOrWhiteSpace(storesStr))
                    {
                        storesStr = inner;
                    }
                    formStr = formStr.Substring(0, parenMatch.Index).Trim();
                }

                currentEntry = new CaseLogEntry
                {
                    Action = ParseActionType(actionStr),
                    FormName = formStr,
                    IsCustomCopy = isCustom,
                    IsHistorical = markAsHistorical,
                    IncludeInOutput = !markAsHistorical
                };

                if (!string.IsNullOrWhiteSpace(storesStr))
                {
                    foreach (var s in storesStr.Split(','))
                    {
                        var trimmedStore = s.Trim();
                        if (string.IsNullOrWhiteSpace(trimmedStore)) continue;
                        var parts = trimmedStore.Split(new[] { " - ", "-" }, 2, StringSplitOptions.None);
                        currentEntry.Stores.Add(new CaseStoreAssignment
                        {
                            CompanyCode = parts[0].Trim(),
                            StoreName = parts.Length > 1 ? parts[1].Trim() : string.Empty
                        });
                    }
                }
                continue;
            }

            if (currentEntry != null)
            {
                var dealsMatch = DealsLineRegex.Match(line);
                if (dealsMatch.Success)
                {
                    var dealsStr = dealsMatch.Groups["deals"].Value.Trim();
                    var dealMatches = Regex.Matches(dealsStr, @"#?(?<num>\d+)(?:\s*\((?<purpose>[^\)]*)\))?");
                    foreach (Match m in dealMatches)
                    {
                        var num = m.Groups["num"].Value.Trim();
                        if (!string.IsNullOrWhiteSpace(num))
                        {
                            currentEntry.Deals.Add(new CaseDealAssignment
                            {
                                DealNumber = num,
                                Purpose = m.Groups["purpose"].Value.Trim()
                            });
                        }
                    }
                    continue;
                }

                var detailsMatch = DetailsLineRegex.Match(line);
                if (detailsMatch.Success)
                {
                    currentEntry.Details = detailsMatch.Groups["details"].Value.Trim();
                    continue;
                }
            }
        }

        if (currentEntry != null)
        {
            result.Add(currentEntry);
        }

        return result;
    }

    public static List<string> ParseLinkedCases(string rawText)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(rawText)) return result;

        var match = LinkedCasesRegex.Match(rawText);
        if (match.Success)
        {
            var casesStr = match.Groups["cases"].Value;
            foreach (var part in casesStr.Split(','))
            {
                var trimmed = part.Replace("#", "").Trim();
                if (!string.IsNullOrWhiteSpace(trimmed) && !result.Contains(trimmed))
                {
                    result.Add(trimmed);
                }
            }
        }
        return result;
    }

    private static CaseActionType ParseActionType(string action)
    {
        return action.ToLowerInvariant() switch
        {
            var a when a.Contains("new form") || a.Contains("menu") => CaseActionType.NewFormPublish,
            var a when a.Contains("programming") || a.Contains("code") || a.Contains("update") || a.Contains("mod") => CaseActionType.ProgrammingUpdate,
            var a when a.Contains("line adjust") => CaseActionType.LineAdjustments,
            var a when a.Contains("investigat") || a.Contains("no change") => CaseActionType.Investigation,
            var a when a.Contains("republish") => CaseActionType.Republish,
            _ => CaseActionType.CustomNote
        };
    }
}

