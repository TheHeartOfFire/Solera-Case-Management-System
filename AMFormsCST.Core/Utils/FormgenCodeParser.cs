using AMFormsCST.Core.Types.CodeBlocks;
using AMFormsCST.Core.Types.CodeBlocks.Formulae;
using AMFormsCST.Core.Types.CodeBlocks.Functions;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AMFormsCST.Core.Utils;

public enum FormgenParseStatus
{
    Valid,
    MalformedSimilar,
    Unrecognized
}

public sealed record FormgenParseResult(
    FormgenParseStatus Status,
    string InputText,
    CodeBase? ParsedTree,
    CustomSnippetCode? CondensedBlock,
    string StatusTitle,
    string DiagnosticMessage,
    string? SuggestedFix = null,
    IReadOnlyList<string>? SuggestedFixes = null)
{
    public bool IsSuccess => Status == FormgenParseStatus.Valid && CondensedBlock != null;
    public bool HasSuggestedFix => !string.IsNullOrWhiteSpace(SuggestedFix);
    public IReadOnlyList<string> SuggestedFixes { get; init; } =
        SuggestedFixes ?? (!string.IsNullOrWhiteSpace(SuggestedFix) ? [SuggestedFix] : Array.Empty<string>());
    public bool HasMultipleSuggestedFixes => SuggestedFixes.Count > 1;
}

public static class FormgenCodeParser
{
    private sealed record FunctionSpec(
        string Prefix,
        string DisplayName,
        IReadOnlyList<string> InitialArgs,
        IReadOnlyList<string>? RepeatingArgs = null,
        bool KeepLastAsDefault = false,
        int MinArgs = 1,
        int? MaxArgs = 1,
        int ArgStep = 1,
        bool UsesParentheses = true);

    private static readonly Dictionary<string, FunctionSpec> KnownFunctions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Logic & Conditionals
        ["IF"] = new("IF", "If Statement", ["Condition", "ResultIfTrue", "ResultIfFalse"], MinArgs: 3, MaxArgs: 3),
        ["XIF"] = new("XIF", "Checkbox X-If (XIF)", ["Condition"], MinArgs: 1, MaxArgs: 1),
        ["XXIF"] = new("XXIF", "Checkbox Double-X (XXIF)", ["Condition"], MinArgs: 1, MaxArgs: 1),
        ["CASE"] = new("CASE", "Case Statement", ["Comparison", "Case", "Result", "Case", "Result", "Default"], ["Case", "Result"], KeepLastAsDefault: true, MinArgs: 4, MaxArgs: null, ArgStep: 2),
        ["CASELTE"] = new("CASELTE", "Case Less/Equal (CASELTE)", ["Comparison", "Threshold", "Result", "Threshold", "Result", "Default"], ["Threshold", "Result"], KeepLastAsDefault: true, MinArgs: 4, MaxArgs: null, ArgStep: 2),
        ["MATCHRANGE"] = new("MATCHRANGE", "Match Range (MATCHRANGE)", ["Comparison", "LowerBound", "UpperBound", "Result", "Default"], ["LowerBound", "UpperBound", "Result"], KeepLastAsDefault: true, MinArgs: 5, MaxArgs: null, ArgStep: 3),
        ["NOT"] = new("NOT", "Logical NOT", ["Condition"], MinArgs: 1, MaxArgs: 1),
        ["AND"] = new("AND", "Logical AND Function", ["Condition", "Condition"], ["Condition"], KeepLastAsDefault: false, MinArgs: 2, MaxArgs: null, ArgStep: 1),
        ["OR"] = new("OR", "Logical OR Function", ["Condition", "Condition"], ["Condition"], KeepLastAsDefault: false, MinArgs: 2, MaxArgs: null, ArgStep: 1),
        ["BLANK"] = new("BLANK", "Is Blank (BLANK)", ["FieldOrPrompt"], MinArgs: 1, MaxArgs: 1),
        ["NOTBLANK"] = new("NOTBLANK", "Is Not Blank (NOTBLANK)", ["FieldOrPrompt"], MinArgs: 1, MaxArgs: 1),
        ["CONTAINS"] = new("CONTAINS", "Contains", ["TextA", "TextB"], MinArgs: 2, MaxArgs: 2),

        // Text & Formatting
        ["SEPLIST"] = new("SEPLIST", "Seplist", ["Separator", "Item", "Item"], ["Item"], KeepLastAsDefault: false, MinArgs: 2, MaxArgs: null, ArgStep: 1),
        ["TEXT"] = new("TEXT", "Text", ["Value"], MinArgs: 1, MaxArgs: 1),
        ["STRIP"] = new("STRIP", "Strip Text (STRIP)", ["SourceText", "TextToRemove"], MinArgs: 2, MaxArgs: 2),
        ["REPLACE"] = new("REPLACE", "Replace Text (REPLACE)", ["SourceText", "UnwantedText", "ReplacementText"], MinArgs: 3, MaxArgs: 3),
        ["LEN"] = new("LEN", "Character Length (LEN)", ["Value"], MinArgs: 1, MaxArgs: 1),
        ["TRIM"] = new("TRIM", "Trim Spaces (TRIM)", ["Text"], MinArgs: 1, MaxArgs: 1),
        ["LTRIM"] = new("LTRIM", "Left Trim (LTRIM)", ["Text"], MinArgs: 1, MaxArgs: 1),
        ["RTRIM"] = new("RTRIM", "Right Trim (RTRIM)", ["Text"], MinArgs: 1, MaxArgs: 1),

        // Date
        ["TODAY"] = new("TODAY", "Current Date (TODAY)", [], MinArgs: 0, MaxArgs: 0, UsesParentheses: true),
        ["MONTH"] = new("MONTH", "Month", ["Date Field"], MinArgs: 1, MaxArgs: 1),
        ["DAY"] = new("DAY", "Day", ["Date Field"], MinArgs: 1, MaxArgs: 1),
        ["YEAR"] = new("YEAR", "Year", ["Date Field"], MinArgs: 1, MaxArgs: 1),
        ["DATE"] = new("DATE", "Text to Date (DATE)", ["DateText"], MinArgs: 1, MaxArgs: 1),
        ["ADDDAYS"] = new("ADDDAYS", "Add Days (ADDDAYS)", ["Date", "Days"], MinArgs: 2, MaxArgs: 2),
        ["ADDWEEKS"] = new("ADDWEEKS", "Add Weeks (ADDWEEKS)", ["Date", "Weeks"], MinArgs: 2, MaxArgs: 2),
        ["ADDMONTHS"] = new("ADDMONTHS", "Add Months (ADDMONTHS)", ["Date", "Months"], MinArgs: 2, MaxArgs: 2),
        ["ADDYEARS"] = new("ADDYEARS", "Add Years (ADDYEARS)", ["Date", "Years"], MinArgs: 2, MaxArgs: 2),

        // Math & Trig
        ["ROUND"] = new("ROUND", "Round", ["Number", "Decimal Places"], MinArgs: 2, MaxArgs: 2),
        ["NUM"] = new("NUM", "Convert to Number (NUM)", ["TextOrDate"], MinArgs: 1, MaxArgs: 1),
        ["FLOOR"] = new("FLOOR", "Round Down (FLOOR)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["CEILING"] = new("CEILING", "Round Up (CEILING)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["ABS"] = new("ABS", "Absolute Value (ABS)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["MAX"] = new("MAX", "Maximum (MAX)", ["Number", "Number"], ["Number"], KeepLastAsDefault: false, MinArgs: 2, MaxArgs: null, ArgStep: 1),
        ["MIN"] = new("MIN", "Minimum (MIN)", ["Number", "Number"], ["Number"], KeepLastAsDefault: false, MinArgs: 2, MaxArgs: null, ArgStep: 1),
        ["SQRT"] = new("SQRT", "Square Root (SQRT)", ["PositiveNumber"], MinArgs: 1, MaxArgs: 1),
        ["LOG"] = new("LOG", "Natural Log (LOG)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["LOG10"] = new("LOG10", "Base-10 Log (LOG10)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["RANDOM"] = new("RANDOM", "Random 0–1 (RANDOM)", [], MinArgs: 0, MaxArgs: 0, UsesParentheses: true),
        ["PI"] = new("PI", "Pi Constant (PI)", [], MinArgs: 0, MaxArgs: 0, UsesParentheses: false),
        ["SIN"] = new("SIN", "Sine (SIN)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["COS"] = new("COS", "Cosine (COS)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["TAN"] = new("TAN", "Tangent (TAN)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["ASIN"] = new("ASIN", "Arcsine (ASIN)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["ACOS"] = new("ACOS", "Arccosine (ACOS)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["ATAN"] = new("ATAN", "Arctangent (ATAN)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["SINH"] = new("SINH", "Hyperbolic Sine (SINH)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["COSH"] = new("COSH", "Hyperbolic Cosine (COSH)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["TANH"] = new("TANH", "Hyperbolic Tangent (TANH)", ["Number"], MinArgs: 1, MaxArgs: 1),
        ["RAD"] = new("RAD", "Degrees to Radians (RAD)", ["Degrees"], MinArgs: 1, MaxArgs: 1),
        ["DEG"] = new("DEG", "Radians to Degrees (DEG)", ["Radians"], MinArgs: 1, MaxArgs: 1),
    };

    private static readonly Dictionary<string, string> CommonFunctionAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["IFF"] = "IF",
        ["IIF"] = "IF",
        ["IFELSE"] = "IF",
        ["SWITCH"] = "CASE",
        ["CONCAT"] = "SEPLIST",
        ["CONCATENATE"] = "SEPLIST",
        ["JOIN"] = "SEPLIST",
        ["SEPLST"] = "SEPLIST",
        ["SEP"] = "SEPLIST",
        ["SUBSTR"] = "CONTAINS",
        ["SUBSTRING"] = "CONTAINS",
        ["CONTAIN"] = "CONTAINS",
        ["INCLUDES"] = "CONTAINS",
        ["LENGTH"] = "LEN",
        ["CEIL"] = "CEILING",
        ["INT"] = "FLOOR",
        ["STR"] = "TEXT",
        ["TOSTRING"] = "TEXT",
        ["TONUM"] = "NUM",
        ["VAL"] = "NUM",
        ["ISBLANK"] = "BLANK",
        ["ISNOTBLANK"] = "NOTBLANK",
        ["NOW"] = "TODAY"
    };

    private static readonly Regex FunctionCallHeaderRegex = new(@"\b([A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.Compiled);
    private static readonly Regex FFieldTokenRegex = new(@"\b[Ff]\d{1,4}\b", RegexOptions.Compiled);
    private static readonly Regex OperatorTokenRegex = new(@"(!=|<=|>=|==|<>|&&|\|\||[=+\-*/%<>]|\bAND\b|\bOR\b)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static FormgenParseResult Parse(string? rawInput)
    {
        var input = rawInput?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(input))
        {
            return new FormgenParseResult(
                FormgenParseStatus.Unrecognized,
                input,
                null,
                null,
                "Not Recognized as a FormGen Function",
                "The text is empty. Paste or type a FormGen function or expression (for example: IF(F72 = 'N', 'NEW', 'USED')).");
        }

        // Reject multi-line prose paragraphs quickly unless it looks like a multi-line FormGen formula
        var normalizedWhitespace = CollapseMultilineWhitespace(input);

        // Strip redundant matching outer parentheses for bare token/identifier inspection:
        // e.g. "(CashDeal)" -> "CashDeal", "(F72)" -> "F72"
        var normalizedCandidate = normalizedWhitespace;
        while (IsWrappedInMatchingParens(normalizedCandidate))
        {
            normalizedCandidate = normalizedCandidate[1..^1].Trim();
        }

        // A. Check for bare F-field token (e.g. F72, F10, f2490)
        var fFieldMatch = Regex.Match(normalizedCandidate, @"^[Ff](\d{1,5})$");
        if (fFieldMatch.Success)
        {
            var fNum = int.Parse(fFieldMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            var fToken = $"F{fNum}";
            var fieldInfo = FormgenReferenceCatalog.TryGetField(fToken);
            var leaf = new SmartFieldCode(fToken);
            var title = $"Field {fToken}";
            var desc = fieldInfo != null
                ? $"Valid FormGen field '{fToken}' ({fieldInfo.Description})."
                : $"Valid FormGen field '{fToken}'.";
            var condensed = new CustomSnippetCode(title, desc, leaf, exposeVariableIdentifiers: false);
            return new FormgenParseResult(FormgenParseStatus.Valid, input, leaf, condensed, title, desc);
        }

        // B. Check for single-quoted or double-quoted text literal (e.g. 'Debt Cancellation...', "N/A", ' ')
        if ((normalizedCandidate.StartsWith('\'') && normalizedCandidate.EndsWith('\'') && normalizedCandidate.Length >= 2 && !IsCharEscaped(normalizedCandidate, normalizedCandidate.Length - 1) && IsValidFormgenLeaf(normalizedCandidate, out _)) ||
            (normalizedCandidate.StartsWith('"') && normalizedCandidate.EndsWith('"') && normalizedCandidate.Length >= 2 && !IsCharEscaped(normalizedCandidate, normalizedCandidate.Length - 1)))
        {
            string? fix = null;
            if (normalizedCandidate.StartsWith('"'))
            {
                fix = $"'{normalizedCandidate[1..^1]}'";
            }
            var leaf = new SmartFieldCode(normalizedCandidate);
            var title = "Text Literal";
            var desc = $"Valid FormGen text literal ({normalizedCandidate}).";
            var condensed = new CustomSnippetCode(title, desc, leaf, exposeVariableIdentifiers: false);
            return new FormgenParseResult(FormgenParseStatus.Valid, input, leaf, condensed, title, desc, fix);
        }

        // C. Check for pure numeric literal (e.g. 0, 100, 1.05)
        if (decimal.TryParse(normalizedCandidate, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
        {
            var leaf = new SmartFieldCode(normalizedCandidate);
            var title = $"Number: {normalizedCandidate}";
            var desc = $"Valid FormGen numeric value ({normalizedCandidate}).";
            var condensed = new CustomSnippetCode(title, desc, leaf, exposeVariableIdentifiers: false);
            return new FormgenParseResult(FormgenParseStatus.Valid, input, leaf, condensed, title, desc);
        }

        // D. Check for pure static symbol/placeholder text (e.g. ***, ---, N/A)
        if (Regex.IsMatch(normalizedCandidate, @"^[\*\-_/\\#@!?+=]+$") ||
            string.Equals(normalizedCandidate, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            var leaf = new SmartFieldCode(normalizedCandidate);
            var title = $"Static Text: {normalizedCandidate}";
            var desc = $"Valid FormGen static text symbol ('{normalizedCandidate}').";
            var condensed = new CustomSnippetCode(title, desc, leaf, exposeVariableIdentifiers: false);
            return new FormgenParseResult(FormgenParseStatus.Valid, input, leaf, condensed, title, desc);
        }

        // E. Check for bare identifier (Smart Field, named variable, or function missing parens)
        if (Regex.IsMatch(normalizedCandidate, @"^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            var upper = normalizedCandidate.ToUpperInvariant();
            if (string.Equals(upper, "PI", StringComparison.OrdinalIgnoreCase))
            {
                var leaf = CreateFunctionInstance(KnownFunctions["PI"], 0);
                var title = "Constant PI";
                var desc = "Valid FormGen PI constant.";
                var condensed = new CustomSnippetCode(title, desc, leaf, exposeVariableIdentifiers: false);
                return new FormgenParseResult(FormgenParseStatus.Valid, input, leaf, condensed, title, desc);
            }

            if (string.Equals(upper, "TODAY", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(upper, "RANDOM", StringComparison.OrdinalIgnoreCase))
            {
                var fix = $"{upper}()";
                return new FormgenParseResult(
                    FormgenParseStatus.MalformedSimilar,
                    input,
                    null,
                    null,
                    "Malformed FormGen Syntax Detected",
                    $"In FormGen, {upper}() is a function and requires empty parentheses: write {upper}() instead of {upper}.",
                    fix);
            }

            if (KnownFunctions.TryGetValue(normalizedCandidate, out var spec) && spec.UsesParentheses)
            {
                var fix = $"{spec.Prefix}()";
                return new FormgenParseResult(
                    FormgenParseStatus.MalformedSimilar,
                    input,
                    null,
                    null,
                    "Malformed FormGen Syntax Detected",
                    $"Function '{spec.Prefix}' is missing opening and closing parentheses: write {spec.Prefix}(...).",
                    fix);
            }

            if (CommonFunctionAliases.TryGetValue(normalizedCandidate, out var canonicalFunc))
            {
                var fix = $"{canonicalFunc}()";
                return new FormgenParseResult(
                    FormgenParseStatus.MalformedSimilar,
                    input,
                    null,
                    null,
                    "Malformed FormGen Syntax Detected",
                    $"Unrecognized function '{normalizedCandidate}' (did you mean '{canonicalFunc}'?) and missing parentheses: write {canonicalFunc}(...).",
                    fix);
            }

            // Otherwise, check whether this identifier is a predefined FormGen smart field or a user-defined variable
            if (FormgenReferenceCatalog.TryGetSmartField(normalizedCandidate, out var smartField))
            {
                var sfLeaf = new SmartFieldCode(smartField!.Name, isPredefined: true, smartField.Category, smartField.Description);
                var sfTitle = $"Predefined Smart Field: {smartField.Name}";
                var sfDesc = $"{smartField.Description} (Category: {smartField.Category}). Predefined FormGen smart variable that reacts dynamically to prompts and deal data.";
                var sfCondensed = new CustomSnippetCode(sfTitle, sfDesc, sfLeaf, exposeVariableIdentifiers: false);
                return new FormgenParseResult(FormgenParseStatus.Valid, input, sfLeaf, sfCondensed, sfTitle, sfDesc);
            }

            var uvLeaf = new SmartFieldCode(normalizedCandidate, isPredefined: false, "User Variable", $"User-defined variable '{normalizedCandidate}'.");
            var uvTitle = $"User-Defined Variable: {normalizedCandidate}";
            var uvDesc = $"Valid FormGen user-defined calculation or prompt variable '{normalizedCandidate}'.";
            var uvCondensed = new CustomSnippetCode(uvTitle, uvDesc, uvLeaf, exposeVariableIdentifiers: false);
            return new FormgenParseResult(FormgenParseStatus.Valid, input, uvLeaf, uvCondensed, uvTitle, uvDesc);
        }

        // 1. Check whether the text has any similarity to a FormGen function or expression
        bool isSimilarToFormgen = LooksLikeFormgenAttempt(normalizedWhitespace, out var PreScanIssues, out var preSuggestedFix, out var altFixes);
        if (!isSimilarToFormgen)
        {
            return new FormgenParseResult(
                FormgenParseStatus.Unrecognized,
                input,
                null,
                null,
                "Not Recognized as a FormGen Function",
                "This text does not match any recognizable FormGen function or expression. Edit the text below — this box monitors your changes and will alert you as soon as a valid FormGen function is recognized.");
        }

        // 2. If pre-scan already found concrete syntax errors (e.g. double quotes, unbalanced parens, function name typo),
        // also check the repaired candidate for argument-count or operand issues so all syntax problems are reported together.
        if (PreScanIssues.Count > 0)
        {
            var secondaryDiagnostics = new List<string>();
            var candidateToTest = preSuggestedFix ?? normalizedWhitespace;
            TryParseExpression(candidateToTest, isTopLevel: true, secondaryDiagnostics, out var secondaryFix);
            foreach (var extra in secondaryDiagnostics)
            {
                if (!PreScanIssues.Contains(extra))
                {
                    PreScanIssues.Add(extra);
                }
            }

            var rawFixCandidate = secondaryFix ?? preSuggestedFix;
            var finalFix = rawFixCandidate != null ? TryRefineSuggestedFix(rawFixCandidate, input) : null;
            if (finalFix != null && string.Equals(finalFix.Trim(), input.Trim(), StringComparison.Ordinal))
            {
                finalFix = null;
            }

            var refinedAltFixes = altFixes?
                .Select(f => TryRefineSuggestedFix(f, input))
                .OfType<string>()
                .Where(f => !string.IsNullOrEmpty(f) && !string.Equals(f, finalFix, StringComparison.Ordinal))
                .Distinct()
                .ToList();

            List<string>? combinedFixes = null;
            if (finalFix != null)
            {
                combinedFixes = [finalFix];
                if (refinedAltFixes != null) combinedFixes.AddRange(refinedAltFixes);
            }
            else if (refinedAltFixes != null && refinedAltFixes.Count > 0)
            {
                combinedFixes = refinedAltFixes;
            }

            var message = string.Join(" ", PreScanIssues);
            return new FormgenParseResult(
                FormgenParseStatus.MalformedSimilar,
                input,
                null,
                null,
                "Malformed FormGen Syntax Detected",
                message,
                finalFix,
                combinedFixes);
        }

        // 3. Attempt full recursive AST parse
        var parseDiagnostics = new List<string>();
        var parsedTree = TryParseExpression(normalizedWhitespace, isTopLevel: true, parseDiagnostics, out var astSuggestedFix);

        if (parsedTree == null || parseDiagnostics.Count > 0)
        {
            var diagText = parseDiagnostics.Count > 0
                ? string.Join(" ", parseDiagnostics)
                : "The expression is similar to a FormGen function, but contains a syntax error. Check parentheses, commas, and single quotes.";

            var rawFixCandidate = astSuggestedFix ?? preSuggestedFix;
            var bestFix = rawFixCandidate != null ? TryRefineSuggestedFix(rawFixCandidate, input) : null;
            if (bestFix != null && string.Equals(bestFix.Trim(), input.Trim(), StringComparison.Ordinal))
            {
                bestFix = null;
            }

            var refinedAltFixes = altFixes?
                .Select(f => TryRefineSuggestedFix(f, input))
                .OfType<string>()
                .Where(f => !string.IsNullOrEmpty(f) && !string.Equals(f, bestFix, StringComparison.Ordinal))
                .Distinct()
                .ToList();

            List<string>? combinedFixes = null;
            if (bestFix != null)
            {
                combinedFixes = [bestFix];
                if (refinedAltFixes != null) combinedFixes.AddRange(refinedAltFixes);
            }
            else if (refinedAltFixes != null && refinedAltFixes.Count > 0)
            {
                combinedFixes = refinedAltFixes;
            }

            return new FormgenParseResult(
                FormgenParseStatus.MalformedSimilar,
                input,
                null,
                null,
                "Malformed FormGen Syntax Detected",
                diagText,
                bestFix,
                combinedFixes);
        }

        // 4. Build condensed block ready for expansion and examination
        var condensedTitle = InferCondensedTitle(parsedTree);
        var condensedDesc = $"Parsed from FormGen expression: {parsedTree.GetCode()}. Click Deconstruct (⤢) to expand into all nested blocks and examine how it works.";
        var condensedBlock = new CustomSnippetCode(condensedTitle, condensedDesc, parsedTree, exposeVariableIdentifiers: true);

        return new FormgenParseResult(
            FormgenParseStatus.Valid,
            input,
            parsedTree,
            condensedBlock,
            "Valid FormGen Function Recognized",
            $"Successfully parsed as '{condensedTitle}' ({condensedBlock.Inputs.Count} input slot(s)).");
    }

    private static string InferCondensedTitle(CodeBase parsedTree)
    {
        var code = parsedTree.GetCode();
        if (code.Contains("SEPLIST( ' ', SEPLIST( ', '", StringComparison.OrdinalIgnoreCase))
            return "City, State ZIP";
        if (code.StartsWith("TEXT(ROUND(", StringComparison.OrdinalIgnoreCase))
            return "Number to Text";
        if (parsedTree is OperatorCode { Operator: "=" } assignOp && assignOp.Inputs.Count > 0)
        {
            var lhs = assignOp.Inputs[0].Value?.ToString();
            if (!string.IsNullOrWhiteSpace(lhs))
                return $"Assign {lhs}";
        }
        if (parsedTree is OperatorCode op)
        {
            return $"Calculate: {op.GetCode()}";
        }
        if (parsedTree is SmartFieldCode sf)
        {
            return sf.IsPredefined
                ? $"Smart Field: {sf.FieldName}"
                : $"Variable: {sf.FieldName}";
        }
        if (!string.IsNullOrWhiteSpace(parsedTree.Name))
            return $"Parsed: {parsedTree.Name}";
        if (!string.IsNullOrWhiteSpace(parsedTree.Prefix))
            return $"Parsed: {parsedTree.Prefix}";
        return "Parsed FormGen Formula";
    }

    private static string CollapseMultilineWhitespace(string input)
    {
        var sb = new StringBuilder(input.Length);
        bool inSingleQuotes = false;
        bool inDoubleQuotes = false;
        bool lastWasSpace = false;

        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '\'' && !inDoubleQuotes && !IsCharEscaped(input, i)) inSingleQuotes = !inSingleQuotes;
            else if (c == '"' && !inSingleQuotes && !IsCharEscaped(input, i)) inDoubleQuotes = !inDoubleQuotes;

            if (!inSingleQuotes && !inDoubleQuotes && (c == '\r' || c == '\n' || c == '\t'))
            {
                if (!lastWasSpace)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasSpace = c == ' ';
            }
        }
        return sb.ToString().Trim();
    }

    private static bool LooksLikeFormgenAttempt(
        string text,
        out List<string> syntaxIssues,
        out string? suggestedFix,
        out List<string>? alternativeFixes)
    {
        syntaxIssues = [];
        suggestedFix = null;
        alternativeFixes = null;
        string workingFix = text;

        // Strip redundant matching outer parentheses: e.g. "(Registrant = 'BUYER' || ...)"
        while (IsWrappedInMatchingParens(workingFix))
        {
            workingFix = workingFix[1..^1].Trim();
        }

        // Strip redundant leading unary plus: e.g. "+ F2289 + F2302"
        if (workingFix.StartsWith('+') && workingFix.Length > 1 && !workingFix.StartsWith("++"))
        {
            syntaxIssues.Add("Stripped redundant leading '+' operator.");
            workingFix = workingFix[1..].TrimStart();
        }

        // Double quotes -> Single quotes (escaping any internal apostrophes to '' so "MANUFACTURER'S" -> 'MANUFACTURER''S')
        if (workingFix.Contains('"'))
        {
            syntaxIssues.Add("FormGen requires single quotes ('...') for text literals instead of double quotes (\"...\").");
            workingFix = Regex.Replace(workingFix, @"""([^""]*)""", m => "'" + m.Groups[1].Value.Replace("'", "''") + "'");
            if (workingFix.Contains('"'))
            {
                workingFix = workingFix.Replace('"', '\'');
            }
        }

        workingFix = RepairGluedFunctionsAndTokens(workingFix, syntaxIssues);
        bool hasRecognizedFunctionOrCloseMatch = false;

        // Check if text starts with a known function name but is missing '(' e.g. "IF F72 = 'N', 'A', 'B'" or "ROUND F11, 2" or "SEPLIST' '"
        var firstWordMatch = Regex.Match(workingFix, @"^([A-Za-z_][A-Za-z0-9_]*)(?:\s+|(?=['\(]))(?!\b(?:AND|OR)\b)(.+)$");
        if (firstWordMatch.Success)
        {
            var firstWord = firstWordMatch.Groups[1].Value;
            var rest = firstWordMatch.Groups[2].Value.TrimStart();
            if (!rest.StartsWith('(') && !rest.StartsWith('=') && !rest.StartsWith('!') &&
                !rest.StartsWith('<') && !rest.StartsWith('>') && !rest.StartsWith('+') &&
                !rest.StartsWith('-') && !rest.StartsWith('*') && !rest.StartsWith('/') && !rest.StartsWith('%'))
            {
                if (KnownFunctions.TryGetValue(firstWord, out var spec) && spec.UsesParentheses)
                {
                    hasRecognizedFunctionOrCloseMatch = true;
                    var canon = spec.Prefix;
                    syntaxIssues.Add($"Function '{canon}' is missing an opening parenthesis '(' after the function name.");
                    workingFix = rest.EndsWith(')')
                        ? $"{canon}({rest}"
                        : $"{canon}({rest})";
                }
                else if (TryFindClosestFunction(firstWord, out var closeFunc))
                {
                    hasRecognizedFunctionOrCloseMatch = true;
                    syntaxIssues.Add($"Unrecognized function '{firstWord}' (did you mean '{closeFunc}'?) and missing opening parenthesis '('.");
                    workingFix = rest.EndsWith(')')
                        ? $"{closeFunc}({rest}"
                        : $"{closeFunc}({rest})";
                }
            }
        }

        // Check all FUNC( headers in the expression
        foreach (Match m in FunctionCallHeaderRegex.Matches(workingFix))
        {
            var fnName = m.Groups[1].Value;
            if (KnownFunctions.TryGetValue(fnName, out var spec))
            {
                hasRecognizedFunctionOrCloseMatch = true;
                if (!spec.UsesParentheses)
                {
                    syntaxIssues.Add($"In FormGen, '{spec.Prefix}' is a constant and does NOT use parentheses: write '{spec.Prefix}' instead of '{spec.Prefix}()'.");
                    workingFix = Regex.Replace(workingFix, $@"\b{Regex.Escape(fnName)}\s*\(\s*\)", spec.Prefix, RegexOptions.IgnoreCase);
                }
            }
            else if (TryFindClosestFunction(fnName, out var closest))
            {
                hasRecognizedFunctionOrCloseMatch = true;
                syntaxIssues.Add($"Unknown function '{fnName}' — did you mean '{closest}'?");
                workingFix = ReplaceWordAt(workingFix, m.Groups[1].Index, m.Groups[1].Length, closest);
            }
        }

        // Check bare TODAY or RANDOM without parentheses
        if (string.Equals(workingFix.Trim(), "TODAY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(workingFix.Trim(), "RANDOM", StringComparison.OrdinalIgnoreCase))
        {
            hasRecognizedFunctionOrCloseMatch = true;
            var upper = workingFix.Trim().ToUpperInvariant();
            syntaxIssues.Add($"In FormGen, {upper}() is a function and requires empty parentheses: write {upper}() instead of {upper}.");
            workingFix = $"{upper}()";
        }
        else if (string.Equals(workingFix.Trim(), "PI", StringComparison.OrdinalIgnoreCase))
        {
            hasRecognizedFunctionOrCloseMatch = true;
        }

        // Check if it looks like an operator expression with F-fields or known functions
        bool hasFField = FFieldTokenRegex.IsMatch(workingFix);
        bool hasOperator = OperatorTokenRegex.IsMatch(workingFix);
        bool hasAssignment = Regex.IsMatch(workingFix, @"^[A-Za-z_][A-Za-z0-9_]*\s*=\s*.+");

        if (!hasRecognizedFunctionOrCloseMatch)
        {
            // If there is a function call like FOOBAR(F72, 'N') that has F-fields or single-quoted strings,
            // treat it as MalformedSimilar ("Unknown FormGen function 'FOOBAR'") if it looks like a formula call;
            // otherwise if it's plain words or a random unknown call like "hello(world)", treat as Unrecognized.
            var anyCall = FunctionCallHeaderRegex.Match(workingFix);
            if (anyCall.Success && (hasFField || workingFix.Contains('\'') || workingFix.Contains('"')))
            {
                var unknownFn = anyCall.Groups[1].Value;
                syntaxIssues.Add($"'{unknownFn}' is not a valid FormGen function name. Check the Functions list in the left panel for supported FormGen functions.");
                hasRecognizedFunctionOrCloseMatch = true;
            }
            else if ((hasFField && hasOperator) || (hasAssignment && (hasFField || workingFix.Contains('\''))))
            {
                hasRecognizedFunctionOrCloseMatch = true;
            }
            else if (TryParseSimpleArithmeticOrComparison(workingFix))
            {
                hasRecognizedFunctionOrCloseMatch = true;
            }
        }

        if (!hasRecognizedFunctionOrCloseMatch)
        {
            return false;
        }

        // Now inspect workingFix for common syntax mistakes:
        // 1. Semicolons outside quotes -> commas

        // 2. Semicolons outside quotes -> commas
        if (ContainsCharOutsideQuotes(workingFix, ';'))
        {
            syntaxIssues.Add("FormGen uses commas (,) to separate function arguments, not semicolons (;).");
            workingFix = ReplaceCharOutsideQuotes(workingFix, ';', ',');
        }

        // 3. '==' or '<>' outside quotes -> '=' or '!='
        if (ContainsSubstringOutsideQuotes(workingFix, "=="))
        {
            syntaxIssues.Add("FormGen uses a single '=' for equality comparison, not '=='.");
            workingFix = ReplaceSubstringOutsideQuotes(workingFix, "==", "=");
        }
        if (ContainsSubstringOutsideQuotes(workingFix, "<>"))
        {
            syntaxIssues.Add("FormGen uses '!=' for inequality comparison, not '<>'.");
            workingFix = ReplaceSubstringOutsideQuotes(workingFix, "<>", "!=");
        }

        // 3b. Single '&' outside quotes -> '&&'
        var singleAmpRegex = new Regex(@"(?<!&)&(?!&)");
        var ampMatches = singleAmpRegex.Matches(workingFix);
        if (ampMatches.Count > 0)
        {
            for (int i = ampMatches.Count - 1; i >= 0; i--)
            {
                var m = ampMatches[i];
                if (!IsIndexInsideQuotes(workingFix, m.Index))
                {
                    syntaxIssues.Add("FormGen uses '&&' for logical AND, not a single '&'.");
                    workingFix = workingFix[..m.Index] + "&&" + workingFix[(m.Index + 1)..];
                }
            }
        }

        // 4. SEPLIST malformed separator: assume standard enclosed single space ' '
        workingFix = RepairSeplistMalformedSeparator(workingFix, syntaxIssues);

        // 5. Unclosed single quotes (must balance quotes before detecting missing commas or parens)
        int singleQuoteCount = CountUnescapedSingleQuotes(workingFix);
        if (singleQuoteCount % 2 != 0)
        {
            workingFix = RepairUnbalancedSingleQuote(workingFix, out var quoteIssue, out var validFixes);
            syntaxIssues.Add(quoteIssue);
            if (validFixes.Count > 1)
            {
                alternativeFixes = validFixes;
            }
        }

        // 6. Missing comparison operator between adjacent terms (must run before missing commas!)
        workingFix = RepairMissingComparisonOperator(workingFix, syntaxIssues, ref alternativeFixes);

        // 7. Missing commas between adjacent arguments (quote parity is now accurate)
        workingFix = RepairMissingCommasBetweenAdjacentArguments(workingFix, syntaxIssues);

        // 8. Missing trailing default argument before ')' (e.g. ", )" at end of nested IF)
        workingFix = RepairMissingTrailingArgument(workingFix, syntaxIssues, ref alternativeFixes);

        // 5. Unbalanced parentheses outside quotes
        CountParenthesesOutsideQuotes(workingFix, out int openParens, out int closeParens, out int firstExtraCloseIndex);
        if (openParens > closeParens)
        {
            int missing = openParens - closeParens;
            workingFix = RepairMissingClosingParentheses(workingFix, missing, out var parenIssues);
            syntaxIssues.AddRange(parenIssues);
        }
        else if (closeParens > openParens)
        {
            int extra = closeParens - openParens;
            syntaxIssues.Add($"Found {extra} extra closing parenthesis ')' with no matching opening '('.");
            workingFix = RemoveExtraClosingParentheses(workingFix, extra);
        }
        else if (firstExtraCloseIndex >= 0)
        {
            syntaxIssues.Add("Parentheses '(' and ')' are out of order.");
        }

        if (syntaxIssues.Count > 0 && !string.Equals(workingFix, text, StringComparison.Ordinal))
        {
            suggestedFix = workingFix;
        }

        return true;
    }

    private static bool TryParseSimpleArithmeticOrComparison(string text)
    {
        if (!OperatorTokenRegex.IsMatch(text)) return false;

        // Strip string literals before prose checking so "'Literal...' + Var" is not rejected as prose
        string textWithoutQuotes = Regex.Replace(text, @"'[^']*'", "''");
        textWithoutQuotes = Regex.Replace(textWithoutQuotes, @"""[^""]*""", "''");

        // Check for adjacent words separated only by whitespace without operators (e.g. "this is a sentence + 1")
        var proseMatch = Regex.Match(textWithoutQuotes, @"\b(?!AND\b|OR\b)[A-Za-z]{2,}\s+(?!AND\b|OR\b)[A-Za-z]{2,}\b", RegexOptions.IgnoreCase);
        if (proseMatch.Success) return false;
        return FindTopLevelOperator(text, out _, out _, out _);
    }

    private static CodeBase? TryParseExpression(
        string expr,
        bool isTopLevel,
        List<string> diagnostics,
        out string? suggestedFix)
    {
        suggestedFix = null;
        var trimmed = expr.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            diagnostics.Add("Missing expression or empty argument.");
            return null;
        }

        // Strip redundant outer parentheses if they wrap the entire expression
        while (IsWrappedInMatchingParens(trimmed))
        {
            trimmed = trimmed[1..^1].Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                diagnostics.Add("Empty parentheses '()' found with no function name or expression inside.");
                return null;
            }
        }

        // 1. Check top-level operator (lowest precedence first)
        if (FindTopLevelOperator(trimmed, out string opSymbol, out string leftStr, out string rightStr))
        {
            if (string.IsNullOrWhiteSpace(leftStr) || string.IsNullOrWhiteSpace(rightStr))
            {
                var missingSide = string.IsNullOrWhiteSpace(leftStr) ? "left-hand" : "right-hand";
                diagnostics.Add($"Operator '{opSymbol}' is missing its {missingSide} operand.");
                if (string.IsNullOrWhiteSpace(rightStr) && !string.IsNullOrWhiteSpace(leftStr))
                {
                    suggestedFix = opSymbol is "=" or "!=" ? $"{leftStr.Trim()} {opSymbol} ''" : $"{leftStr.Trim()} {opSymbol} 0";
                }
                return null;
            }

            var opBlock = new OperatorCode(opSymbol);
            if (!PopulateOperandSlot(opBlock, 0, leftStr.Trim(), diagnostics, out var leftFix))
            {
                if (leftFix != null) suggestedFix = $"{leftFix} {opSymbol} {rightStr.Trim()}";
                return null;
            }
            if (!PopulateOperandSlot(opBlock, 1, rightStr.Trim(), diagnostics, out var rightFix))
            {
                if (rightFix != null) suggestedFix = $"{leftStr.Trim()} {opSymbol} {rightFix}";
                return null;
            }
            return opBlock;
        }

        // 2. Check zero-argument constant PI or TODAY()/RANDOM()
        if (string.Equals(trimmed, "PI", StringComparison.OrdinalIgnoreCase))
        {
            return CreateFunctionInstance(KnownFunctions["PI"], 0);
        }

        // 3. Check Function Call: IDENT(args...)
        if (TryMatchWholeFunctionCall(trimmed, out string funcName, out string argsInterior))
        {
            string resolvedFuncName = funcName;
            if (!KnownFunctions.TryGetValue(resolvedFuncName, out var spec))
            {
                if (TryFindClosestFunction(resolvedFuncName, out var closest))
                {
                    diagnostics.Add($"Unknown function '{resolvedFuncName}' — did you mean '{closest}'?");
                    suggestedFix = $"{closest}({argsInterior})";
                    return null;
                }

                diagnostics.Add($"'{resolvedFuncName}' is not a recognized FormGen function.");
                return null;
            }

            var argStrings = SplitTopLevelArguments(argsInterior, out bool hasEmptyArg);
            if (hasEmptyArg)
            {
                diagnostics.Add($"Function '{spec.Prefix}()' contains an empty argument slot (check for extra or trailing commas).");
                var cleanedArgs = argStrings.Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
                if (cleanedArgs.Count >= spec.MinArgs)
                {
                    suggestedFix = $"{spec.Prefix}({string.Join(", ", cleanedArgs)})";
                }
                return null;
            }

            // Validate argument count against FunctionSpec
            if (!ValidateArgumentCount(spec, argStrings, diagnostics, out var fixedArgs))
            {
                if (!isTopLevel && spec.MaxArgs.HasValue && argStrings.Count > spec.MaxArgs.Value)
                {
                    var closedCall = $"{spec.Prefix}({string.Join(", ", argStrings.Take(spec.MaxArgs.Value))})";
                    var remaining = string.Join(", ", argStrings.Skip(spec.MaxArgs.Value));
                    suggestedFix = $"{closedCall}, {remaining}";
                }
                else if (fixedArgs != null)
                {
                    suggestedFix = $"{spec.Prefix}({string.Join(", ", fixedArgs)})";
                }
                return null;
            }

            var fnBlock = CreateFunctionInstance(spec, argStrings.Count);
            var rebuiltArgsForFix = new List<string>(argStrings);
            bool anyChildFix = false;

            for (int i = 0; i < argStrings.Count; i++)
            {
                if (!PopulateOperandSlot(fnBlock, i, argStrings[i], diagnostics, out var childFix))
                {
                    if (childFix != null)
                    {
                        rebuiltArgsForFix[i] = childFix;
                        anyChildFix = true;
                    }
                    if (anyChildFix)
                    {
                        suggestedFix = $"{spec.Prefix}({string.Join(", ", rebuiltArgsForFix)})";
                    }
                    return null;
                }
            }

            return fnBlock;
        }

        // If at top level and it is neither an operator expression nor a function call
        if (isTopLevel)
        {
            diagnostics.Add($"'{trimmed}' is not a complete FormGen function call or operator expression.");
        }
        return null;
    }

    private static bool PopulateOperandSlot(
        CodeBase parent,
        int slotIndex,
        string rawArg,
        List<string> diagnostics,
        out string? childSuggestedFix)
    {
        childSuggestedFix = null;
        var trimmed = rawArg.Trim();

        // Strip outer parens if the entire argument is parenthesized
        var unwrapped = trimmed;
        while (IsWrappedInMatchingParens(unwrapped))
        {
            unwrapped = unwrapped[1..^1].Trim();
        }

        // Determine if this argument is a nested Function call or Operator expression
        bool isFunctionCall = TryMatchWholeFunctionCall(unwrapped, out _, out _) ||
                              string.Equals(unwrapped, "PI", StringComparison.OrdinalIgnoreCase);
        bool isOperatorExpr = !isFunctionCall && FindTopLevelOperator(unwrapped, out _, out _, out _);

        if (isFunctionCall || isOperatorExpr)
        {
            // Keep TODAY() and RANDOM() as nested blocks or leaf strings cleanly
            var childBlock = TryParseExpression(unwrapped, isTopLevel: false, diagnostics, out childSuggestedFix);
            if (childBlock == null)
            {
                return false;
            }
            parent.SetInputValue(slotIndex, childBlock);
            return true;
        }

        // Check if this argument is already a valid quoted string literal (which can contain parentheses in the text!)
        if (unwrapped.StartsWith('\'') && IsValidFormgenLeaf(unwrapped, out _))
        {
            parent.SetInputValue(slotIndex, unwrapped);
            return true;
        }

        // Check if it looks like a malformed function call inside an argument (e.g. contains '(' or unknown function call)
        if (unwrapped.Contains('(') || unwrapped.Contains(')'))
        {
            var nestedAttempt = TryParseExpression(unwrapped, isTopLevel: false, diagnostics, out childSuggestedFix);
            if (nestedAttempt == null)
            {
                return false;
            }
            parent.SetInputValue(slotIndex, nestedAttempt);
            return true;
        }

        // Check if bare TODAY or RANDOM was used as an argument without parentheses
        if (string.Equals(unwrapped, "TODAY", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(unwrapped, "RANDOM", StringComparison.OrdinalIgnoreCase))
        {
            var upper = unwrapped.ToUpperInvariant();
            diagnostics.Add($"In FormGen, {upper}() is a function and requires empty parentheses: write {upper}() instead of {upper}.");
            childSuggestedFix = $"{upper}()";
            return false;
        }

        // Leaf literal, F-field, or variable identifier
        if (!IsValidFormgenLeaf(unwrapped, out var leafError))
        {
            diagnostics.Add(leafError ?? $"Invalid operand '{unwrapped}'.");
            return false;
        }

        parent.SetInputValue(slotIndex, unwrapped);
        return true;
    }

    private static bool IsValidFormgenLeaf(string text, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "Argument cannot be blank or empty.";
            return false;
        }

        // 1. Quoted string literal: '...'
        if (text.StartsWith('\''))
        {
            if (!text.EndsWith('\'') || text.Length < 2 || IsCharEscaped(text, text.Length - 1))
            {
                error = $"Unclosed string literal in '{text}'.";
                return false;
            }

            // Inside the quotes (from index 1 to text.Length - 2), any internal quote must be escaped as \' or ''
            int i = 1;
            while (i < text.Length - 1)
            {
                if (text[i] == '\'')
                {
                    if (IsCharEscaped(text, i))
                    {
                        i++;
                        continue;
                    }

                    // Check for '' pair (both unescaped)
                    if (i + 1 < text.Length - 1 && text[i + 1] == '\'' && !IsCharEscaped(text, i + 1))
                    {
                        i += 2;
                        continue;
                    }

                    error = $"Malformed string literal or unescaped quotes in '{text}'.";
                    return false;
                }
                i++;
            }

            return true;
        }

        // String cannot contain single quotes if it didn't start with quote
        if (text.Contains('\''))
        {
            error = $"Misplaced or unclosed single quote in '{text}'.";
            return false;
        }

        // 2. Numeric literal (integer or floating point, optionally signed)
        if (decimal.TryParse(text, NumberStyles.Integer | NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))
        {
            return true;
        }

        // 3. Identifier / field reference / variable name:
        // Must start with optional sign followed by letter or underscore, and alphanumeric or underscores.
        // FormGen variables/fields (e.g. F340, WPAD, SYS_DATE, MyVar) cannot contain spaces or symbols.
        if (Regex.IsMatch(text, @"^[+-]?[A-Za-z_][A-Za-z0-9_]*$"))
        {
            return true;
        }

        error = $"Invalid token, field, or variable name '{text}'. Field and variable names must start with a letter and contain no spaces or special characters.";
        return false;
    }

    private static bool ValidateArgumentCount(
        FunctionSpec spec,
        List<string> args,
        List<string> diagnostics,
        out List<string>? suggestedFixedArgs)
    {
        suggestedFixedArgs = null;
        int count = args.Count;

        if (spec.MaxArgs == 0 && count > 0)
        {
            diagnostics.Add($"{spec.Prefix}() does not take any arguments, but {count} argument(s) were provided.");
            suggestedFixedArgs = [];
            return false;
        }

        if (count < spec.MinArgs)
        {
            int missing = spec.MinArgs - count;
            var expectedNames = string.Join(", ", spec.InitialArgs.Take(spec.MinArgs));
            diagnostics.Add($"{spec.Prefix}() requires at least {spec.MinArgs} argument(s) ({expectedNames}), but only {count} {(count == 1 ? "was" : "were")} provided.");

            var padded = new List<string>(args);
            for (int i = count; i < spec.MinArgs; i++)
            {
                var argLabel = i < spec.InitialArgs.Count ? spec.InitialArgs[i] : "Value";
                padded.Add(GetDefaultPlaceholderForArg(spec.Prefix, argLabel, i));
            }
            suggestedFixedArgs = padded;
            return false;
        }

        if (spec.MaxArgs.HasValue && count > spec.MaxArgs.Value)
        {
            var expectedNames = string.Join(", ", spec.InitialArgs);
            diagnostics.Add($"{spec.Prefix}() accepts at most {spec.MaxArgs.Value} argument(s) ({expectedNames}), but {count} were provided.");
            suggestedFixedArgs = args.Take(spec.MaxArgs.Value).ToList();
            return false;
        }

        if (spec.ArgStep == 2 && (count - spec.MinArgs) % 2 != 0)
        {
            // CASE or CASELTE requires Comparison + N*(Case, Result) + Default -> even argument count >= 4
            diagnostics.Add($"{spec.Prefix}() requires a Comparison value, one or more ({spec.RepeatingArgs![0]}, {spec.RepeatingArgs[1]}) pairs, and a final Default value (an even number of arguments >= 4), but {count} arguments were provided.");
            var padded = new List<string>(args) { "''" };
            suggestedFixedArgs = padded;
            return false;
        }

        if (spec.ArgStep == 3 && (count - 2) % 3 != 0)
        {
            // MATCHRANGE requires Comparison + N*(Lower, Upper, Result) + Default -> 5, 8, 11...
            diagnostics.Add($"MATCHRANGE() requires a Comparison value, one or more (LowerBound, UpperBound, Result) triplets, and a final Default value (5, 8, 11... arguments), but {count} arguments were provided.");
            var padded = new List<string>(args);
            while ((padded.Count - 2) % 3 != 0)
            {
                padded.Add("''");
            }
            suggestedFixedArgs = padded;
            return false;
        }

        return true;
    }

    private static string GetDefaultPlaceholderForArg(string prefix, string argLabel, int index)
    {
        if (prefix == "ROUND" && index == 1) return "2";
        if (prefix is "ADDDAYS" or "ADDWEEKS" or "ADDMONTHS" or "ADDYEARS" && index == 1) return "1";
        if (string.Equals(prefix, "SEPLIST", StringComparison.OrdinalIgnoreCase) && index == 0)
        {
            return "' '";
        }
        if (argLabel.Contains("Separator", StringComparison.OrdinalIgnoreCase))
        {
            return "' '";
        }
        if (argLabel.Contains("Result", StringComparison.OrdinalIgnoreCase) ||
            argLabel.Contains("Default", StringComparison.OrdinalIgnoreCase) ||
            argLabel.Contains("Text", StringComparison.OrdinalIgnoreCase))
        {
            return "''";
        }
        if (argLabel.Contains("Number", StringComparison.OrdinalIgnoreCase) ||
            argLabel.Contains("Decimal", StringComparison.OrdinalIgnoreCase) ||
            argLabel.Contains("Threshold", StringComparison.OrdinalIgnoreCase) ||
            argLabel.Contains("Bound", StringComparison.OrdinalIgnoreCase))
        {
            return "0";
        }
        return argLabel.Replace(" ", "");
    }

    private static CodeBase CreateFunctionInstance(FunctionSpec spec, int argCount)
    {
        switch (spec.Prefix.ToUpperInvariant())
        {
            case "IF":
                return new IfCode();

            case "CASE":
            {
                var caseBlock = new CaseCode();
                // CaseCode starts with 6 inputs (Comparison + 2 pairs + Default).
                // If argCount == 4, remove 1 pair; if argCount > 6, add extra pairs.
                if (argCount < caseBlock.InputCount())
                {
                    while (caseBlock.InputCount() > argCount && caseBlock.InputCount() >= 4)
                    {
                        caseBlock.RemoveInput(caseBlock.InputCount() - 2);
                        caseBlock.RemoveInput(caseBlock.InputCount() - 2);
                    }
                }
                else if (argCount > caseBlock.InputCount())
                {
                    int extraPairs = (argCount - caseBlock.InputCount()) / 2;
                    caseBlock.AddExtraInputs(extraPairs);
                }
                return caseBlock;
            }

            case "SEPLIST":
            {
                var sepBlock = new SeplistCode();
                if (argCount < sepBlock.InputCount())
                {
                    while (sepBlock.InputCount() > argCount && sepBlock.InputCount() >= 2)
                    {
                        sepBlock.RemoveInput(sepBlock.InputCount() - 1);
                    }
                }
                else if (argCount > sepBlock.InputCount())
                {
                    sepBlock.AddExtraInputs(argCount - sepBlock.InputCount());
                }
                return sepBlock;
            }

            case "CONTAINS":
                return new ContainsCode();

            case "ROUND":
                return new RoundCode();

            case "TEXT":
                return new TextCode();

            case "MONTH":
                return new MonthCode();

            case "DAY":
                return new DayCode();

            case "YEAR":
                return new YearCode();

            default:
            {
                if (spec.RepeatingArgs != null)
                {
                    var ext = new ExtendableFormgenFunctionCode(
                        spec.Prefix,
                        spec.DisplayName,
                        spec.InitialArgs,
                        spec.RepeatingArgs,
                        spec.KeepLastAsDefault);

                    if (argCount < ext.InputCount())
                    {
                        while (ext.InputCount() > argCount && ext.InputCount() >= spec.MinArgs)
                        {
                            for (int r = 0; r < spec.ArgStep && ext.InputCount() > spec.MinArgs; r++)
                            {
                                int removeIdx = spec.KeepLastAsDefault && ext.InputCount() > 1
                                    ? ext.InputCount() - 2
                                    : ext.InputCount() - 1;
                                ext.RemoveInput(removeIdx);
                            }
                        }
                    }
                    else if (argCount > ext.InputCount())
                    {
                        int extraGroups = (argCount - ext.InputCount()) / spec.ArgStep;
                        ext.AddExtraInputs(extraGroups);
                    }
                    return ext;
                }

                return new FormgenFunctionCode(
                    spec.Prefix,
                    spec.DisplayName,
                    spec.InitialArgs,
                    spec.UsesParentheses);
            }
        }
    }

    private static bool TryMatchWholeFunctionCall(string expr, out string funcName, out string argsInterior)
    {
        funcName = string.Empty;
        argsInterior = string.Empty;

        int openIdx = expr.IndexOf('(');
        if (openIdx <= 0 || !expr.EndsWith(')')) return false;

        string candidateName = expr[..openIdx].Trim();
        if (string.IsNullOrEmpty(candidateName) || !Regex.IsMatch(candidateName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
        {
            return false;
        }

        // Verify that the '(' at openIdx matches the final ')' at expr.Length - 1
        bool inQuotes = false;
        int depth = 0;
        for (int i = openIdx; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == '\'' && !IsCharEscaped(expr, i))
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (inQuotes) continue;

            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0 && i < expr.Length - 1)
                {
                    // Closed before the end of the string (e.g., "TEXT(ROUND(F47, 0)) + 'th'")
                    return false;
                }
            }
        }

        if (depth != 0) return false;

        funcName = candidateName;
        argsInterior = expr[(openIdx + 1)..^1];
        return true;
    }

    private static List<string> SplitTopLevelArguments(string argsInterior, out bool hasEmptyArg)
    {
        hasEmptyArg = false;
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(argsInterior))
        {
            return results;
        }

        bool inQuotes = false;
        int parenDepth = 0;
        int lastStart = 0;

        for (int i = 0; i < argsInterior.Length; i++)
        {
            char c = argsInterior[i];
            if (c == '\'' && !IsCharEscaped(argsInterior, i))
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (inQuotes) continue;

            if (c == '(') parenDepth++;
            else if (c == ')') parenDepth--;
            else if (c == ',' && parenDepth == 0)
            {
                var segment = argsInterior[lastStart..i].Trim();
                if (string.IsNullOrEmpty(segment))
                {
                    hasEmptyArg = true;
                }
                results.Add(segment);
                lastStart = i + 1;
            }
        }

        var finalSegment = argsInterior[lastStart..].Trim();
        if (string.IsNullOrEmpty(finalSegment))
        {
            hasEmptyArg = true;
        }
        results.Add(finalSegment);
        return results;
    }

    private static bool FindTopLevelOperator(
        string expr,
        out string operatorSymbol,
        out string leftOperand,
        out string rightOperand)
    {
        operatorSymbol = string.Empty;
        leftOperand = string.Empty;
        rightOperand = string.Empty;

        // Precedence levels from lowest (split first) to highest (split last):
        // 1. Logical OR
        // 2. Logical AND
        // 3. Comparisons / Assignment: !=, <=, >=, =, <, >
        // 4. Additive: +, -
        // 5. Multiplicative: *, /, %
        // 6. Scientific notation: e
        string[][] precedenceGroups =
        [
            ["||", "OR"],
            ["&&", "AND"],
            ["!=", "<=", ">=", "=", "<", ">"],
            ["+", "-"],
            ["*", "/", "%"]
        ];

        foreach (var group in precedenceGroups)
        {
            int matchPos = -1;
            string matchedOp = string.Empty;

            bool inQuotes = false;
            int depth = 0;

            for (int i = 0; i < expr.Length; i++)
            {
                char c = expr[i];
                if (c == '\'' && !IsCharEscaped(expr, i))
                {
                    inQuotes = !inQuotes;
                    continue;
                }
                if (inQuotes) continue;

                if (c == '(') { depth++; continue; }
                if (c == ')') { depth--; continue; }
                if (depth != 0) continue;

                foreach (var candidateOp in group)
                {
                    if (MatchesOperatorAt(expr, i, candidateOp))
                    {
                        // A binary operator cannot split if there is no left-hand operand
                        if (i == 0 || string.IsNullOrWhiteSpace(expr[..i]))
                        {
                            continue;
                        }

                        // Ignore unary minus immediately after another operator
                        if (candidateOp == "-" && IsUnaryMinusAt(expr, i))
                        {
                            continue;
                        }

                        // For left-associative binary operators, keep the last match at top level
                        // except for '=' assignment where the first '=' splits LHS = RHS
                        if (candidateOp == "=" && matchPos >= 0)
                        {
                            continue;
                        }

                        matchPos = i;
                        matchedOp = candidateOp;
                        break;
                    }
                }
            }

            if (matchPos >= 0)
            {
                operatorSymbol = matchedOp.ToUpperInvariant();
                leftOperand = expr[..matchPos];
                rightOperand = expr[(matchPos + matchedOp.Length)..];
                return true;
            }
        }

        return false;
    }

    private static bool MatchesOperatorAt(string expr, int index, string op)
    {
        if (index + op.Length > expr.Length) return false;
        if (!expr.AsSpan(index, op.Length).Equals(op, StringComparison.OrdinalIgnoreCase)) return false;

        if (char.IsLetter(op[0]))
        {
            // Word operator (AND, OR) must have word boundaries
            bool leftBoundary = index == 0 || !char.IsLetterOrDigit(expr[index - 1]) && expr[index - 1] != '_';
            bool rightBoundary = index + op.Length == expr.Length || !char.IsLetterOrDigit(expr[index + op.Length]) && expr[index + op.Length] != '_';
            return leftBoundary && rightBoundary;
        }

        if (op == "=" && index > 0 && (expr[index - 1] == '!' || expr[index - 1] == '<' || expr[index - 1] == '>' || expr[index - 1] == '='))
            return false;
        if (op == "=" && index + 1 < expr.Length && expr[index + 1] == '=')
            return false;
        if ((op == "<" || op == ">") && index + 1 < expr.Length && (expr[index + 1] == '=' || expr[index + 1] == '>'))
            return false;

        return true;
    }

    private static bool IsUnaryMinusAt(string expr, int index)
    {
        int prev = index - 1;
        while (prev >= 0 && char.IsWhiteSpace(expr[prev])) prev--;
        if (prev < 0) return true;
        char p = expr[prev];
        return p is '(' or ',' or '=' or '<' or '>' or '+' or '-' or '*' or '/' or '%';
    }

    private static bool IsWrappedInMatchingParens(string expr)
    {
        if (expr.Length < 2 || expr[0] != '(' || expr[^1] != ')') return false;
        bool inQuotes = false;
        int depth = 0;
        for (int i = 0; i < expr.Length; i++)
        {
            char c = expr[i];
            if (c == '\'' && !IsCharEscaped(expr, i))
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (inQuotes) continue;
            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0 && i < expr.Length - 1)
                {
                    return false;
                }
            }
        }
        return depth == 0;
    }

    private static bool TryFindClosestFunction(string candidate, out string matchedFunction)
    {
        matchedFunction = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate)) return false;

        if (candidate.Length == 1)
        {
            if (candidate.Equals("I", StringComparison.OrdinalIgnoreCase) || candidate.Equals("F", StringComparison.OrdinalIgnoreCase))
            {
                matchedFunction = "IF";
                return true;
            }
            return false;
        }

        if (CommonFunctionAliases.TryGetValue(candidate, out var aliasTarget))
        {
            matchedFunction = aliasTarget;
            return true;
        }

        int bestDistance = int.MaxValue;
        string? bestKey = null;

        foreach (var known in KnownFunctions.Keys)
        {
            int maxAllowed = known.Length <= 3 ? 1 : 2;
            if (Math.Abs(known.Length - candidate.Length) > maxAllowed) continue;

            int dist = ComputeLevenshteinDistance(candidate.ToUpperInvariant(), known.ToUpperInvariant());
            if (dist <= maxAllowed && dist < bestDistance)
            {
                bestDistance = dist;
                bestKey = known;
            }
        }

        if (bestKey != null)
        {
            matchedFunction = bestKey;
            return true;
        }
        return false;
    }

    private static int ComputeLevenshteinDistance(string a, string b)
    {
        int n = a.Length;
        int m = b.Length;
        var dp = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) dp[i, 0] = i;
        for (int j = 0; j <= m; j++) dp[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }
        return dp[n, m];
    }

    private static bool IsCharEscaped(string text, int index)
    {
        if (index <= 0 || index >= text.Length) return false;
        int backslashCount = 0;
        int j = index - 1;
        while (j >= 0 && text[j] == '\\')
        {
            backslashCount++;
            j--;
        }
        return (backslashCount % 2) != 0;
    }

    private static int CountUnescapedSingleQuotes(string text)
    {
        int count = 0;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\'' && !IsCharEscaped(text, i))
            {
                count++;
            }
        }
        return count;
    }

    private static bool ContainsCharOutsideQuotes(string text, char target)
    {
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !IsCharEscaped(text, i)) inQuotes = !inQuotes;
            else if (!inQuotes && c == target) return true;
        }
        return false;
    }

    private static string ReplaceCharOutsideQuotes(string text, char target, char replacement)
    {
        var sb = new StringBuilder(text.Length);
        bool inQuotes = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !IsCharEscaped(text, i)) inQuotes = !inQuotes;
            sb.Append(!inQuotes && c == target ? replacement : c);
        }
        return sb.ToString();
    }

    private static bool ContainsSubstringOutsideQuotes(string text, string sub)
    {
        bool inQuotes = false;
        for (int i = 0; i <= text.Length - sub.Length; i++)
        {
            if (text[i] == '\'' && !IsCharEscaped(text, i)) inQuotes = !inQuotes;
            else if (!inQuotes && text.AsSpan(i, sub.Length).Equals(sub, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string ReplaceSubstringOutsideQuotes(string text, string target, string replacement)
    {
        var sb = new StringBuilder(text.Length);
        bool inQuotes = false;
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] == '\'' && !IsCharEscaped(text, i))
            {
                inQuotes = !inQuotes;
                sb.Append(text[i++]);
                continue;
            }
            if (!inQuotes && i + target.Length <= text.Length &&
                text.AsSpan(i, target.Length).Equals(target, StringComparison.Ordinal))
            {
                sb.Append(replacement);
                i += target.Length;
                continue;
            }
            sb.Append(text[i++]);
        }
        return sb.ToString();
    }

    private static bool IsIndexInsideQuotes(string text, int index)
    {
        bool inQuotes = false;
        for (int i = 0; i < index && i < text.Length; i++)
        {
            if (text[i] == '\'' && !IsCharEscaped(text, i)) inQuotes = !inQuotes;
        }
        return inQuotes;
    }

    private static string RepairGluedFunctionsAndTokens(string text, List<string> syntaxIssues)
    {
        // A. Known function followed directly by another known function without '(': e.g. IFNOTBLANK -> IF(NOTBLANK
        var gluedFuncRegex = new Regex(@"\b(IF)(NOTBLANK|BLANK|CONTAINS|NOT|ROUND|TRUNC|FLOOR|CEILING|ABS|MIN|MAX|SUM|AVG|LEN|UPPER|LOWER|TRIM|SUBSTR|REPLACE|TEXT|NUM|DATE|SEPLIST|CASE)\b", RegexOptions.IgnoreCase);
        var matches1 = gluedFuncRegex.Matches(text);
        if (matches1.Count > 0)
        {
            for (int i = matches1.Count - 1; i >= 0; i--)
            {
                var m = matches1[i];
                if (!IsIndexInsideQuotes(text, m.Index))
                {
                    syntaxIssues.Add("Missing opening parenthesis '(' between nested function calls.");
                    text = text[..m.Index] + m.Groups[1].Value + "(" + m.Groups[2].Value + text[(m.Index + m.Length)..];
                }
            }
        }

        // B. Known function followed directly by F-field without '(': e.g. NOTBLANKF340 -> NOTBLANK(F340
        var gluedFieldRegex = new Regex(@"\b(NOTBLANK|BLANK|CONTAINS|NOT|ROUND|TRUNC|FLOOR|CEILING|ABS|MIN|MAX|SUM|AVG|LEN|UPPER|LOWER|TRIM|SUBSTR|REPLACE|TEXT|NUM|DATE|YEAR|MONTH|DAY|SEPLIST|CASE|XIF|XXIF|IF)([Ff]\d{1,5}\b)", RegexOptions.IgnoreCase);
        var matches2 = gluedFieldRegex.Matches(text);
        if (matches2.Count > 0)
        {
            for (int i = matches2.Count - 1; i >= 0; i--)
            {
                var m = matches2[i];
                if (!IsIndexInsideQuotes(text, m.Index))
                {
                    syntaxIssues.Add("Missing opening parenthesis '(' between function name and field argument.");
                    text = text[..m.Index] + m.Groups[1].Value + "(" + m.Groups[2].Value + text[(m.Index + m.Length)..];
                }
            }
        }

        // C. Known function followed directly by string quote without '(': e.g. SEPLIST' ' -> SEPLIST(' '
        var gluedQuoteRegex = new Regex(@"\b(NOTBLANK|BLANK|CONTAINS|NOT|ROUND|TRUNC|FLOOR|CEILING|ABS|MIN|MAX|SUM|AVG|LEN|UPPER|LOWER|TRIM|LTRIM|RTRIM|SUBSTR|REPLACE|TEXT|NUM|DATE|YEAR|MONTH|DAY|SEPLIST|CASE|XIF|XXIF|IF)\s*(')", RegexOptions.IgnoreCase);
        var matches3 = gluedQuoteRegex.Matches(text);
        if (matches3.Count > 0)
        {
            for (int i = matches3.Count - 1; i >= 0; i--)
            {
                var m = matches3[i];
                if (!IsIndexInsideQuotes(text, m.Index))
                {
                    syntaxIssues.Add($"Missing opening parenthesis '(' between function '{m.Groups[1].Value}' and string literal.");
                    text = text[..m.Index] + m.Groups[1].Value + "(" + m.Groups[2].Value + text[(m.Index + m.Length)..];
                }
            }
        }

        // D. IF or CONTAINS followed by an identifier without '(':
        // 1) Spaced: e.g. "IF WPAD = 'Y'", "IF WPAD, 'Y', 'N'"
        var spacedIfVarRegex = new Regex(@"\b(IF|CONTAINS)\s+([A-Za-z_][A-Za-z0-9_]*)\b(?=\s*(?:[=!<>]|\bCONTAINS\b|\bAND\b|\bOR\b|&&|\|\||,|\)))", RegexOptions.IgnoreCase);
        var matchesSpaced = spacedIfVarRegex.Matches(text);
        if (matchesSpaced.Count > 0)
        {
            for (int i = matchesSpaced.Count - 1; i >= 0; i--)
            {
                var m = matchesSpaced[i];
                if (!IsIndexInsideQuotes(text, m.Index))
                {
                    var varName = m.Groups[2].Value;
                    if (string.Equals(varName, "F", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(varName, "ELSE", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    syntaxIssues.Add($"Missing opening parenthesis '(' after function '{m.Groups[1].Value}'.");
                    text = text[..m.Index] + m.Groups[1].Value + "(" + varName + text[(m.Index + m.Length)..];
                }
            }
        }

        // 2) Glued without space: e.g. "IFWPAD = 'Y'". Must NOT match "IfOther, 'val'" where IfOther is an argument to an enclosing function.
        var gluedIfVarRegex = new Regex(@"\b(IF|CONTAINS)([A-Za-z_][A-Za-z0-9_]*)\b(?=\s*(?:[=!<>]|\bCONTAINS\b|\bAND\b|\bOR\b|&&|\|\|))", RegexOptions.IgnoreCase);
        var matchesGlued = gluedIfVarRegex.Matches(text);
        if (matchesGlued.Count > 0)
        {
            for (int i = matchesGlued.Count - 1; i >= 0; i--)
            {
                var m = matchesGlued[i];
                if (!IsIndexInsideQuotes(text, m.Index))
                {
                    var varName = m.Groups[2].Value;
                    if (string.Equals(varName, "F", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(varName, "ELSE", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    syntaxIssues.Add($"Missing opening parenthesis '(' after function '{m.Groups[1].Value}'.");
                    text = text[..m.Index] + m.Groups[1].Value + "(" + varName + text[(m.Index + m.Length)..];
                }
            }
        }

        return text;
    }

    private static string RepairMissingComparisonOperator(
        string text,
        List<string> syntaxIssues,
        ref List<string>? alternativeFixes)
    {
        var missingOpRegexes = new[]
        {
            new Regex(@"(?<=\(|,|^|\bAND\b|\bOR\b|&&|\|\|)\s*(?<left>(?!\b(?:AND|OR|IF|NOT|BLANK|NOTBLANK|SEPLIST|CASE|ROUND|TEXT|DATE)\b)(?:[Ff]\d{1,5}|[A-Za-z_][A-Za-z0-9_]*))\s+(?<right>'[^']*'|\d+(?:\.\d+)?)\s*(?=(?:,|\)|&&|\|\||\bAND\b|\bOR\b))", RegexOptions.IgnoreCase),
            new Regex(@"(?<=\(|,|^|\bAND\b|\bOR\b|&&|\|\|)\s*(?<left>'[^']*'|\d+(?:\.\d+)?)\s+(?<right>(?!\b(?:AND|OR|IF|NOT|BLANK|NOTBLANK|SEPLIST|CASE|ROUND|TEXT|DATE)\b)(?:[Ff]\d{1,5}|[A-Za-z_][A-Za-z0-9_]*))\s*(?=(?:,|\)|&&|\|\||\bAND\b|\bOR\b))", RegexOptions.IgnoreCase),
            new Regex(@"(?<=\(|,|^|\bAND\b|\bOR\b|&&|\|\|)\s*(?<left>[Ff]\d{1,5})\s+(?<right>[Ff]\d{1,5})\s*(?=(?:,|\)|&&|\|\||\bAND\b|\bOR\b))", RegexOptions.IgnoreCase)
        };

        List<string> generatedOptions = [];

        foreach (var regex in missingOpRegexes)
        {
            var matches = regex.Matches(text);
            if (matches.Count == 0) continue;

            for (int i = matches.Count - 1; i >= 0; i--)
            {
                var m = matches[i];
                if (!IsIndexInsideQuotes(text, m.Index))
                {
                    var left = m.Groups["left"].Value;
                    var right = m.Groups["right"].Value;
                    int leftEnd = m.Groups["left"].Index + m.Groups["left"].Length;
                    int rightStart = m.Groups["right"].Index;

                    bool isStringComparison = left.StartsWith('\'') || right.StartsWith('\'');
                    string[] validOps = isStringComparison
                        ? ["=", "!="]
                        : ["=", "!=", ">", "<", ">=", "<="];

                    syntaxIssues.Add($"Missing comparison operator between '{left}' and '{right}'; assumed '='. Did you mean '=', '!=', or another operator?");

                    // Generate alternative choices for the user before mutating text
                    foreach (var op in validOps)
                    {
                        string candidate = text[..leftEnd] + $" {op} " + text[rightStart..];
                        generatedOptions.Add(candidate);
                    }

                    // Apply default '=' operator to workingFix
                    text = text[..leftEnd] + " = " + text[rightStart..];
                }
            }
        }

        if (generatedOptions.Count > 0)
        {
            if (alternativeFixes == null)
            {
                alternativeFixes = generatedOptions;
            }
            else
            {
                alternativeFixes.AddRange(generatedOptions);
            }
        }

        return text;
    }

    private static string RepairMissingTrailingArgument(
        string text,
        List<string> syntaxIssues,
        ref List<string>? alternativeFixes)
    {
        // Matches a comma followed by whitespace and a closing paren: e.g. ", )"
        var missingTrailingArgRegex = new Regex(@",\s*(?=\))");
        var matches = missingTrailingArgRegex.Matches(text);
        if (matches.Count == 0) return text;

        // Check if formula appears to output numbers or strings
        // If it has result branches returning strings ('...'), default to ' '; otherwise default to 0
        bool appearsNumeric = !Regex.IsMatch(text, @",\s*'[^']*'\s*[,)]");

        string defaultVal = appearsNumeric ? "0" : "' '";
        string altVal = appearsNumeric ? "' '" : "0";

        List<string> generatedOptions = [];

        for (int i = matches.Count - 1; i >= 0; i--)
        {
            var m = matches[i];
            if (!IsIndexInsideQuotes(text, m.Index))
            {
                syntaxIssues.Add($"Missing default/fallback argument before ')' in function call; assumed {defaultVal}.");

                string candidateDefault = text[..m.Index] + ", " + defaultVal + text[(m.Index + m.Length)..];
                string candidateAlt = text[..m.Index] + ", " + altVal + text[(m.Index + m.Length)..];

                generatedOptions.Add(candidateDefault);
                generatedOptions.Add(candidateAlt);

                text = candidateDefault;
            }
        }

        if (generatedOptions.Count > 0)
        {
            if (alternativeFixes == null)
            {
                alternativeFixes = generatedOptions;
            }
            else
            {
                alternativeFixes.AddRange(generatedOptions);
            }
        }

        return text;
    }

    private static string RepairMissingCommasBetweenAdjacentArguments(string text, List<string> syntaxIssues)
    {
        // 1. Closing paren ')' followed by whitespace and then another argument (quote, field, number, function, or nested paren)
        var closeParenArgRegex = new Regex(@"\)\s+(?=(?:'|[Ff]\d{1,5}\b|\d+\b|(?!AND\b|OR\b)[A-Za-z_][A-Za-z0-9_]*|\())");
        var matches1 = closeParenArgRegex.Matches(text);
        if (matches1.Count > 0)
        {
            for (int i = matches1.Count - 1; i >= 0; i--)
            {
                var m = matches1[i];
                if (!IsIndexInsideQuotes(text, m.Index))
                {
                    syntaxIssues.Add("Missing comma (,) separating arguments.");
                    text = text[..m.Index] + "), " + text[(m.Index + m.Length)..];
                }
            }
        }

        // 2. String literal closing quote followed by whitespace and then another argument (quote, field, number, or identifier)
        var quoteArgRegex = new Regex(@"('\s*)\s+(?=(?:'|[Ff]\d{1,5}\b|\d+\b|(?!AND\b|OR\b)[A-Za-z_][A-Za-z0-9_]*|\())");
        var matches2 = quoteArgRegex.Matches(text);
        if (matches2.Count > 0)
        {
            for (int i = matches2.Count - 1; i >= 0; i--)
            {
                var m = matches2[i];
                if (!IsIndexInsideQuotes(text, m.Index + m.Groups[1].Length))
                {
                    syntaxIssues.Add("Missing comma (,) separating arguments.");
                    text = text[..m.Index] + m.Groups[1].Value + ", " + text[(m.Index + m.Length)..];
                }
            }
        }

        // 3. F-field followed by whitespace and then a function header, quote, or other argument
        var fieldArgRegex = new Regex(@"\b([Ff]\d{1,5})\s+(?=(?:'|[Ff]\d{1,5}\b|\d+\b|(?!AND\b|OR\b)[A-Za-z_][A-Za-z0-9_]*\s*\())");
        var matches3 = fieldArgRegex.Matches(text);
        if (matches3.Count > 0)
        {
            for (int i = matches3.Count - 1; i >= 0; i--)
            {
                var m = matches3[i];
                if (!IsIndexInsideQuotes(text, m.Index))
                {
                    syntaxIssues.Add("Missing comma (,) separating arguments.");
                    text = text[..m.Index] + m.Groups[1].Value + ", " + text[(m.Index + m.Length)..];
                }
            }
        }

        // 4. Number literal followed by whitespace and then a quote, F-field, or function header (e.g. "12 'ANNUALLY'", "12 F10")
        var numberArgRegex = new Regex(@"\b(\d+)\s+(?=(?:'|[Ff]\d{1,5}\b|(?!AND\b|OR\b)[A-Za-z_][A-Za-z0-9_]*\s*\())");
        var matches4 = numberArgRegex.Matches(text);
        if (matches4.Count > 0)
        {
            for (int i = matches4.Count - 1; i >= 0; i--)
            {
                var m = matches4[i];
                if (!IsIndexInsideQuotes(text, m.Index))
                {
                    syntaxIssues.Add("Missing comma (,) separating arguments.");
                    text = text[..m.Index] + m.Groups[1].Value + ", " + text[(m.Index + m.Length)..];
                }
            }
        }

        return text;
    }

    private static string RepairSeplistMalformedSeparator(string text, List<string> syntaxIssues)
    {
        var matches = Regex.Matches(text, @"\bSEPLIST\s*\(", RegexOptions.IgnoreCase);
        if (matches.Count == 0) return text;

        for (int m = matches.Count - 1; m >= 0; m--)
        {
            var match = matches[m];
            int start = match.Index + match.Length;

            // Check if separator is a valid 2-quote literal: '...',
            var validLiteralMatch = Regex.Match(text[start..], @"^\s*'([^'()]{0,10})'\s*,");
            if (validLiteralMatch.Success)
            {
                // Already a well-formed separator like '', ' ', ', ', '-', etc.
                continue;
            }

            // Check if separator has malformed quotes with no delimiter characters (e.g. " '", "' ", "'", or just whitespace) before the next comma
            var malformedMatch = Regex.Match(text[start..], @"^(\s*'?\s*'?\s*),");
            if (malformedMatch.Success)
            {
                var sepText = malformedMatch.Groups[1].Value;
                if (sepText.Count(c => c == '\'') < 2 && string.IsNullOrWhiteSpace(sepText.Replace("'", "")))
                {
                    syntaxIssues.Add("SEPLIST() separator argument has malformed quotes; assuming standard single-space separator (' ').");
                    int replaceLen = malformedMatch.Length;
                    text = text[..start] + "' ', " + text[(start + replaceLen)..].TrimStart();
                }
            }
        }

        return text;
    }

    private static string RepairUnbalancedSingleQuote(string text, out string issue, out List<string> validFixes)
    {
        issue = "Unclosed single-quoted text literal — missing an opening or closing single quote (').";
        validFixes = new List<string>();
        if (string.IsNullOrEmpty(text)) return text;

        var zeroErrorCandidates = new List<(string Candidate, string Description, int Score)>();
        var fallbackCandidates = new List<(string Candidate, string Description, int DiagCount)>();

        // Phase 1: Test inserting a single quote (') at every plausible position.
        // For a single-character deletion of a quote, inserting "'" at the deletion point restores the exact original.
        for (int k = 0; k <= text.Length; k++)
        {
            // Skip inserting inside an identifier (e.g. between letters or digits)
            if (k > 0 && k < text.Length && char.IsLetterOrDigit(text[k - 1]) && char.IsLetterOrDigit(text[k]))
            {
                continue;
            }

            var cand = text.Insert(k, "'");
            var testDiags = new List<string>();
            var tree = TryParseExpression(cand, isTopLevel: true, testDiags, out _);

            if (tree != null && testDiags.Count == 0)
            {
                // Score candidates: prefer inserting quote immediately adjacent to delimiters like ',' or ')' or '('
                // rather than leaving floating spaces (e.g. "'TRADE #2: ', " is preferred over "'TRADE #2:' , ")
                int score = 0;
                if (k < text.Length && (text[k] is ',' or ')' or '=' or '!' or '<' or '>')) score += 10;
                if (k > 0 && (text[k - 1] is ',' or '(' or '=' or '!' or '<' or '>')) score += 10;
                // Penalize leaving whitespace between quote and delimiter outside quotes (e.g. "'foo' ,")
                if (k < text.Length && text[k] == ' ' && k + 1 < text.Length && (text[k + 1] is ',' or ')')) score -= 15;
                if (k > 0 && text[k - 1] == ' ' && k - 2 >= 0 && (text[k - 2] is ',' or '(')) score -= 15;

                string snippet = k < text.Length ? text[k..Math.Min(text.Length, k + 8)].Trim() : "";
                string desc = !string.IsNullOrEmpty(snippet)
                    ? $"Inserted missing single quote (') before '{snippet}'."
                    : "Inserted missing closing single quote (') at the end.";

                zeroErrorCandidates.Add((cand, desc, score));
            }
            else
            {
                fallbackCandidates.Add((cand, "Missing single quote (').", testDiags.Count));
            }
        }

        // Phase 2: If no insertion worked, test removing an errant extra single quote
        if (zeroErrorCandidates.Count == 0)
        {
            for (int q = 0; q < text.Length; q++)
            {
                if (text[q] != '\'') continue;
                var cand = text.Remove(q, 1);
                var testDiags = new List<string>();
                var tree = TryParseExpression(cand, isTopLevel: true, testDiags, out _);
                if (tree != null && testDiags.Count == 0)
                {
                    zeroErrorCandidates.Add((cand, "Removed extra single quote (').", 0));
                }
            }
        }

        if (zeroErrorCandidates.Count > 0)
        {
            var sortedCandidates = zeroErrorCandidates
                .OrderByDescending(c => c.Score)
                .Select(c => c.Candidate)
                .Distinct()
                .ToList();

            validFixes = sortedCandidates;
            var best = zeroErrorCandidates.OrderByDescending(c => c.Score).First();
            issue = best.Description;
            return best.Candidate;
        }

        // Phase 3: Fallback if compound errors exist
        if (fallbackCandidates.Count > 0)
        {
            var bestFallback = fallbackCandidates.OrderBy(c => c.DiagCount).First();
            issue = bestFallback.Description;
            return bestFallback.Candidate;
        }

        return text + "'";
    }

    private static void CountParenthesesOutsideQuotes(
        string text,
        out int openParens,
        out int closeParens,
        out int firstExtraCloseIndex)
    {
        openParens = 0;
        closeParens = 0;
        firstExtraCloseIndex = -1;

        bool inQuotes = false;
        int depth = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !IsCharEscaped(text, i))
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (inQuotes) continue;

            if (c == '(')
            {
                openParens++;
                depth++;
            }
            else if (c == ')')
            {
                closeParens++;
                depth--;
                if (depth < 0 && firstExtraCloseIndex < 0)
                {
                    firstExtraCloseIndex = i;
                }
            }
        }
    }

    private static string RemoveExtraClosingParentheses(string text, int extraToRemove)
    {
        var sb = new StringBuilder(text.Length);
        bool inQuotes = false;
        int depth = 0;
        int removed = 0;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !IsCharEscaped(text, i))
            {
                inQuotes = !inQuotes;
                sb.Append(c);
                continue;
            }
            if (!inQuotes)
            {
                if (c == '(')
                {
                    depth++;
                }
                else if (c == ')')
                {
                    if (depth == 0 && removed < extraToRemove)
                    {
                        removed++;
                        continue;
                    }
                    depth = Math.Max(0, depth - 1);
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    private static string ReplaceWordAt(string text, int index, int length, string replacement) =>
        string.Concat(text.AsSpan(0, index), replacement, text.AsSpan(index + length));

    private static string? TryRefineSuggestedFix(string? candidateFix, string originalInput)
    {
        if (string.IsNullOrWhiteSpace(candidateFix)) return null;
        if (string.Equals(candidateFix.Trim(), originalInput.Trim(), StringComparison.Ordinal)) return null;

        var diags = new List<string>();
        var tree = TryParseExpression(candidateFix, isTopLevel: true, diags, out var secondStageFix);
        if (tree != null && diags.Count == 0)
        {
            return candidateFix;
        }
        if (!string.IsNullOrWhiteSpace(secondStageFix) && !string.Equals(secondStageFix.Trim(), originalInput.Trim(), StringComparison.Ordinal))
        {
            var thirdDiags = new List<string>();
            var thirdTree = TryParseExpression(secondStageFix, isTopLevel: true, thirdDiags, out _);
            if (thirdTree != null && thirdDiags.Count == 0)
            {
                return secondStageFix;
            }
            return null;
        }
        return null;
    }

    private sealed class ParenFrame
    {
        public string FunctionName { get; }
        public int? MaxArgs { get; }
        public int ArgCount { get; set; } = 1;
        public int OpenParenIndex { get; }

        public ParenFrame(string functionName, int? maxArgs, int openParenIndex)
        {
            FunctionName = functionName;
            MaxArgs = maxArgs;
            OpenParenIndex = openParenIndex;
        }
    }

    private static string RepairMissingClosingParentheses(string text, int missingCount, out List<string> issues)
    {
        issues = [];
        if (missingCount <= 0 || string.IsNullOrEmpty(text))
        {
            return text;
        }

        var sb = new StringBuilder(text.Length + missingCount + 8);
        var stack = new Stack<ParenFrame>();
        bool inQuotes = false;
        int remainingMissing = missingCount;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\'' && !IsCharEscaped(text, i))
            {
                inQuotes = !inQuotes;
                sb.Append(c);
                continue;
            }

            if (inQuotes)
            {
                sb.Append(c);
                continue;
            }

            if (c == '(')
            {
                string fnName = ExtractPrecedingIdentifier(text, i);
                int? maxArgs = null;
                if (!string.IsNullOrEmpty(fnName))
                {
                    if (KnownFunctions.TryGetValue(fnName, out var spec))
                    {
                        maxArgs = spec.MaxArgs;
                    }
                    else if (CommonFunctionAliases.TryGetValue(fnName, out var alias) &&
                             KnownFunctions.TryGetValue(alias, out var aliasSpec))
                    {
                        maxArgs = aliasSpec.MaxArgs;
                    }
                }

                stack.Push(new ParenFrame(fnName, maxArgs, sb.Length));
                sb.Append(c);
                continue;
            }

            if (c == ')')
            {
                if (stack.Count > 0)
                {
                    stack.Pop();
                }
                sb.Append(c);
                continue;
            }

            if (c == ',')
            {
                if (stack.Count > 0)
                {
                    var top = stack.Peek();
                    top.ArgCount++;

                    // If the current function has a known maximum argument count,
                    // has reached that maximum (top.ArgCount > top.MaxArgs.Value),
                    // there is an enclosing function on the stack to receive subsequent arguments (stack.Count > 1),
                    // and there are missing closing parentheses to place:
                    if (remainingMissing > 0 && stack.Count > 1 && top.MaxArgs.HasValue && top.ArgCount > top.MaxArgs.Value)
                    {
                        // Trim trailing whitespace from sb before inserting ')'
                        int insertPos = sb.Length;
                        while (insertPos > 0 && char.IsWhiteSpace(sb[insertPos - 1]))
                        {
                            insertPos--;
                        }

                        sb.Insert(insertPos, ')');
                        stack.Pop();
                        remainingMissing--;

                        var fnDisplayName = !string.IsNullOrEmpty(top.FunctionName) ? $"'{top.FunctionName}()'" : "function";
                        issues.Add($"Missing closing parenthesis ')' for {fnDisplayName} after argument {top.MaxArgs.Value}.");

                        // Now the comma belongs to the parent frame on the stack (if any)
                        if (stack.Count > 0)
                        {
                            stack.Peek().ArgCount++;
                        }
                    }
                }

                sb.Append(c);
                continue;
            }

            sb.Append(c);
        }

        // Any remaining unclosed parentheses belong at the end of the expression
        if (remainingMissing > 0)
        {
            sb.Append(new string(')', remainingMissing));
            issues.Add($"Missing {remainingMissing} closing parenthesis '{new string(')', remainingMissing)}' at the end of the expression.");
        }

        return sb.ToString();
    }

    private static string ExtractPrecedingIdentifier(string text, int parenIndex)
    {
        int end = parenIndex - 1;
        while (end >= 0 && char.IsWhiteSpace(text[end]))
        {
            end--;
        }
        if (end < 0) return string.Empty;

        int start = end;
        while (start >= 0 && (char.IsLetterOrDigit(text[start]) || text[start] == '_'))
        {
            start--;
        }
        start++;
        if (start <= end)
        {
            return text[start..(end + 1)];
        }
        return string.Empty;
    }
}
