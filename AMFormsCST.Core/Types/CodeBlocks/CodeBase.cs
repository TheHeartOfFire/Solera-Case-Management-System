using AMFormsCST.Core.Interfaces.CodeBlocks;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AMFormsCST.Core.Types.CodeBlocks
{
    public abstract class CodeBase : ICodeBase
    {
        private static readonly Regex IdentifierTokenRegex = new(@"\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);

        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Prefix { get; set; }
        public List<CodeInput> Inputs { get; } = [];

        public bool HasNoInputs() => Inputs.Count == 0;

        public CodeBase AddInput(CodeInput input)
        {
            if (Inputs.Exists(x => x.Index == input.Index))
                Inputs[Inputs.IndexOf(Inputs.Find(x => x.Index == input.Index)!)] = input;
            else
                Inputs.Add(input);
            Inputs.Sort();

            return this;
        }

        public CodeBase AddInput(string description)
        {
            Inputs.Add(new CodeInput(string.Empty, description, Inputs.Count));
            Inputs.Sort();
            return this;
        }
        public CodeBase AddInput(int index, string description)
        {

            foreach (var input in Inputs.Where(input => input.Index >= index))
                input.Index++;

            Inputs.Add(new CodeInput(string.Empty, description, index));
            Inputs.Sort();

            return this;
        }
        public CodeBase RemoveInput(int index)
        {

            Inputs.RemoveAt(index);

            foreach (var input in Inputs.Where(input => input.Index >= index))
                input.Index--;

            return this;
        }
        public CodeBase AddInput(string value, string description)
        {
            Inputs.Add(new CodeInput(value, description, Inputs.Count));
            return this;
        }
        public CodeBase AddInput(CodeBase value, string description)
        {
            Inputs.Add(new CodeInput(value, description, Inputs.Count));
            return this;
        }

        public CodeBase SetInputValue(int index, string value)
        {
            Inputs[index].SetValue(value);
            return this;
        }

        public CodeBase SetInputValue(int index, CodeBase value)
        {
            Inputs[index].SetValue(value);
            return this;
        }
        public CodeBase SetInputDescription(int index, string value)
        {
            Inputs[index].Description = value;
            return this;
        }

        public CodeBase SetInputs(List<string> inputs)
        {
            if (inputs.Count != Inputs.Count)
                throw new ArgumentException("Inputs count does not match");

            foreach (var input in Inputs)
                input.SetValue(inputs[input.Index]);

            return this;
        }

        public List<CodeInput> GetInputs() => Inputs;

        public int InputCount() => Inputs.Count;

        public virtual object GetInput(int idx) => Inputs[idx].Value.Equals(string.Empty) ? Inputs[idx].Description : Inputs[idx].Value;
        public virtual object GetDescription(int idx) => Inputs[idx].Description;

        public virtual CodeBase? GetStructure() => null;
        public bool HasStructure => GetStructure() is not null;

        public bool WasDeconstructed { get; set; }
        public string? ReconstructName { get; set; }
        public string? ReconstructDescription { get; set; }

        public CodeBase SetInputObjectValue(int index, object value)
        {
            if (value is CodeBase cb)
                return SetInputValue(index, cb);
            return SetInputValue(index, value?.ToString() ?? string.Empty);
        }

        public CodeBase SetInputFrom(int index, CodeInput source, string? groupKeyOverride = null)
        {
            Inputs[index].Description = source.Description;
            Inputs[index].IsExposedInput = true;
            Inputs[index].ExposedGroupKey = !string.IsNullOrWhiteSpace(groupKeyOverride)
                ? groupKeyOverride
                : (!string.IsNullOrWhiteSpace(source.ExposedGroupKey)
                    ? source.ExposedGroupKey
                    : $"{source.Index}:{source.Description}");

            if (source.Value is CodeBase cb)
                return SetInputValue(index, cb.Clone());

            return SetInputValue(index, source.Value?.ToString() ?? string.Empty);
        }

        public virtual CodeBase Clone()
        {
            var clone = (CodeBase)Activator.CreateInstance(GetType())!;
            clone.Name = Name;
            clone.Description = Description;
            clone.Prefix = Prefix;
            clone.WasDeconstructed = WasDeconstructed;
            clone.ReconstructName = ReconstructName;
            clone.ReconstructDescription = ReconstructDescription;
            clone.Inputs.Clear();
            foreach (var input in Inputs)
            {
                CodeInput clonedInput = input.Value is CodeBase cb
                    ? new CodeInput(cb.Clone(), input.Description, input.Index)
                    : new CodeInput(input.Value?.ToString() ?? string.Empty, input.Description, input.Index);
                clonedInput.IsExposedInput = input.IsExposedInput;
                clonedInput.ExposedGroupKey = input.ExposedGroupKey;
                clone.Inputs.Add(clonedInput);
            }
            return clone;
        }

        public virtual string GetCode()
        {
            var structure = GetStructure();
            if (structure is not null)
            {
                return structure.GetCode();
            }

            if (HasNoInputs()) return string.Empty;

            StringBuilder ProcessFunctions(StringBuilder inputBuilder)
            {
                var step = 0;

                foreach (var input in Inputs)
                {
                    inputBuilder.Append(input.Value is CodeBase @base ? @base.GetCode() : ((string)input.Value).Equals(string.Empty) ? input.Description : input.Value);

                    if (step != Inputs.Count - 1)
                        inputBuilder.Append(", ");
                    step++;
                }

                return inputBuilder;
            }

            static string FormatParens(StringBuilder input) => input.ToString().Replace("( (", "((").Replace(") )", "))");

            StringBuilder output = new();

            output.Append(Prefix);
            output.Append("( ");

            output = ProcessFunctions(output);

            output.Append(" )");

            return FormatParens(output);
        }

        public virtual IReadOnlyList<string> GetVariables()
        {
            var variables = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddVar(string candidate)
            {
                var trimmed = candidate.Trim();
                if (string.IsNullOrEmpty(trimmed)) return;
                if (IsSingleQuoted(trimmed) || IsNumericLiteral(trimmed) || IsBooleanLiteral(trimmed) || IsReservedKeyword(trimmed))
                    return;
                if (seen.Add(trimmed))
                {
                    variables.Add(trimmed);
                }
            }

            void CollectFromBlock(CodeBase block)
            {
                foreach (var input in block.Inputs)
                {
                    if (input.Value is CodeBase childBlock)
                    {
                        CollectFromBlock(childBlock);
                    }
                    else
                    {
                        var strVal = input.Value?.ToString() ?? string.Empty;
                        if (string.IsNullOrWhiteSpace(strVal))
                        {
                            // Unfilled slot: expose its description placeholder so the user can test with it!
                            if (!string.IsNullOrWhiteSpace(input.Description))
                            {
                                AddVar(input.Description);
                            }
                        }
                        else if (!IsSingleQuoted(strVal.Trim()) && !IsNumericLiteral(strVal.Trim()))
                        {
                            foreach (Match match in IdentifierTokenRegex.Matches(strVal))
                            {
                                AddVar(match.Value);
                            }
                        }
                    }
                }
            }

            CollectFromBlock(this);
            var structure = GetStructure();
            if (structure is not null)
            {
                CollectFromBlock(structure);
            }

            return variables;
        }

        public virtual IReadOnlyList<CodeDiagnostic> Validate()
        {
            var diagnostics = new List<CodeDiagnostic>();

            foreach (var input in Inputs)
            {
                if (input.Value is CodeBase childBlock)
                {
                    foreach (var childDiag in childBlock.Validate())
                    {
                        diagnostics.Add(childDiag with { InputIndex = input.Index });
                    }
                }
                else
                {
                    var strVal = input.Value?.ToString() ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(strVal))
                    {
                        diagnostics.Add(new CodeDiagnostic(
                            CodeDiagnosticSeverity.Warning,
                            $"Input '{input.Description}' on {Name ?? Prefix ?? "Block"} is empty — FormGen will output the placeholder name '{input.Description}' until you type a field (e.g. F100), a single-quoted literal (e.g. 'text'), or snap in a block.",
                            input.Index));
                    }
                }
            }

            ValidateBlockSpecific(diagnostics);
            return diagnostics;
        }

        protected virtual void ValidateBlockSpecific(List<CodeDiagnostic> diagnostics)
        {
            if (Prefix is "SEPLIST" && Inputs.Count > 0 && Inputs[0].Value is string sepStr && !string.IsNullOrWhiteSpace(sepStr))
            {
                var trimmed = sepStr.Trim();
                if (!IsSingleQuoted(trimmed) && !IsLikelyFieldReference(trimmed))
                {
                    diagnostics.Add(new CodeDiagnostic(
                        CodeDiagnosticSeverity.Warning,
                        $"In SEPLIST, the Separator ({trimmed}) is not wrapped in single quotes. In FormGen, literal text like '{trimmed}' must be single-quoted, otherwise FormGen treats it as a variable name.",
                        0));
                }
            }
            else if (Prefix is "ROUND" && Inputs.Count > 1 && Inputs[1].Value is string decStr && !string.IsNullOrWhiteSpace(decStr))
            {
                var trimmed = decStr.Trim();
                if (IsSingleQuoted(trimmed) || (!int.TryParse(trimmed, out _) && !IsLikelyFieldReference(trimmed)))
                {
                    diagnostics.Add(new CodeDiagnostic(
                        CodeDiagnosticSeverity.Warning,
                        $"ROUND's 'Decimal Places' input ({trimmed}) should be an unquoted whole number (such as 0 or 2).",
                        1));
                }
            }
            else if (Prefix is "CONTAINS" && Inputs.Count > 1 && Inputs[1].Value is string subStr && !string.IsNullOrWhiteSpace(subStr))
            {
                var trimmed = subStr.Trim();
                if (!IsSingleQuoted(trimmed) && !IsLikelyFieldReference(trimmed))
                {
                    diagnostics.Add(new CodeDiagnostic(
                        CodeDiagnosticSeverity.Warning,
                        $"In CONTAINS, the search substring ({trimmed}) is not single-quoted. Wrap literal text in single quotes like '{trimmed}' unless it is a field name.",
                        1));
                }
            }
        }

        public virtual CodeEvaluationResult Evaluate(IReadOnlyDictionary<string, string>? testValues = null)
        {
            var values = testValues ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var diagnostics = Validate().ToList();

            var structure = GetStructure();
            if (structure is not null)
            {
                var structResult = structure.Evaluate(values);
                return structResult with
                {
                    Explanation = $"{Name}: {structResult.Explanation}",
                    Diagnostics = diagnostics.Concat(structResult.Diagnostics)
                        .DistinctBy(d => d.Message)
                        .ToList()
                };
            }

            return EvaluateCore(values, diagnostics);
        }

        protected virtual CodeEvaluationResult EvaluateCore(
            IReadOnlyDictionary<string, string> testValues,
            List<CodeDiagnostic> diagnostics)
        {
            var resolvedArgs = new List<(string Value, string Explanation)>();
            for (int i = 0; i < Inputs.Count; i++)
            {
                resolvedArgs.Add(ResolveInput(i, testValues, diagnostics));
            }

            switch (Prefix)
            {
                case "SEPLIST":
                {
                    if (resolvedArgs.Count < 1)
                        return new CodeEvaluationResult(false, string.Empty, "SEPLIST requires a separator and items.", diagnostics);

                    var sep = resolvedArgs[0].Value;
                    var nonEmptyItems = resolvedArgs.Skip(1)
                        .Select(a => a.Value)
                        .Where(v => !string.IsNullOrEmpty(v))
                        .ToList();
                    var joined = string.Join(sep, nonEmptyItems);
                    return new CodeEvaluationResult(
                        true,
                        joined,
                        $"SEPLIST joined {nonEmptyItems.Count} non-empty item(s) using separator \"{sep}\" (empty items are automatically skipped in FormGen).",
                        diagnostics);
                }

                case "IF":
                {
                    if (resolvedArgs.Count < 3)
                        return new CodeEvaluationResult(false, string.Empty, "IF requires Condition, ResultIfTrue, and ResultIfFalse.", diagnostics);

                    bool cond = EvaluateConditionString(resolvedArgs[0].Value);
                    var chosen = cond ? resolvedArgs[1].Value : resolvedArgs[2].Value;
                    var branchName = cond ? "ResultIfTrue" : "ResultIfFalse";
                    return new CodeEvaluationResult(
                        true,
                        chosen,
                        $"IF condition evaluated to {cond.ToString().ToUpperInvariant()} (from \"{resolvedArgs[0].Value}\"), so {branchName} (\"{chosen}\") was returned.",
                        diagnostics);
                }

                case "CASE":
                {
                    if (resolvedArgs.Count < 4)
                        return new CodeEvaluationResult(false, string.Empty, "CASE requires a Comparison value, Case/Result pairs, and a Default.", diagnostics);

                    var comparison = resolvedArgs[0].Value;
                    for (int i = 1; i < resolvedArgs.Count - 1; i += 2)
                    {
                        var caseVal = resolvedArgs[i].Value;
                        var resultVal = resolvedArgs[i + 1].Value;
                        if (ValuesMatch(comparison, caseVal))
                        {
                            return new CodeEvaluationResult(
                                true,
                                resultVal,
                                $"CASE matched Comparison \"{comparison}\" == Case #{(i + 1) / 2} (\"{caseVal}\") -> returned \"{resultVal}\".",
                                diagnostics);
                        }
                    }

                    var defaultVal = resolvedArgs[^1].Value;
                    return new CodeEvaluationResult(
                        true,
                        defaultVal,
                        $"CASE: Comparison \"{comparison}\" did not match any Case value, so Default (\"{defaultVal}\") was returned.",
                        diagnostics);
                }

                case "CONTAINS":
                {
                    if (resolvedArgs.Count < 2)
                        return new CodeEvaluationResult(false, "FALSE", "CONTAINS requires two arguments.", diagnostics);

                    var haystack = resolvedArgs[0].Value;
                    var needle = resolvedArgs[1].Value;
                    bool exactMatch = !string.IsNullOrEmpty(needle) && haystack.Contains(needle, StringComparison.Ordinal);
                    bool caseInsensitiveMatch = !string.IsNullOrEmpty(needle) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

                    if (!exactMatch && caseInsensitiveMatch)
                    {
                        diagnostics.Add(new CodeDiagnostic(
                            CodeDiagnosticSeverity.Info,
                            $"Coach Tip: \"{haystack}\" contains \"{needle}\" with different capitalization, but FormGen's CONTAINS is case-sensitive!"));
                    }

                    return new CodeEvaluationResult(
                        true,
                        exactMatch ? "TRUE" : "FALSE",
                        $"CONTAINS checked if \"{haystack}\" contains \"{needle}\" -> {(exactMatch ? "TRUE" : "FALSE")}.",
                        diagnostics);
                }

                case "ROUND":
                {
                    if (resolvedArgs.Count < 2)
                        return new CodeEvaluationResult(false, string.Empty, "ROUND requires Number and Decimal Places.", diagnostics);

                    if (!double.TryParse(resolvedArgs[0].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double num))
                    {
                        var msg = $"ROUND could not parse Number \"{resolvedArgs[0].Value}\" as a numeric value. Enter a numeric test value for '{Inputs[0].Description}'.";
                        diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg, 0));
                        return new CodeEvaluationResult(false, "NaN", msg, diagnostics);
                    }

                    if (!int.TryParse(resolvedArgs[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int decimals) || decimals < 0 || decimals > 15)
                    {
                        var msg = $"ROUND could not parse Decimal Places \"{resolvedArgs[1].Value}\" as an integer (0–15).";
                        diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg, 1));
                        return new CodeEvaluationResult(false, "NaN", msg, diagnostics);
                    }

                    var rounded = Math.Round(num, decimals, MidpointRounding.AwayFromZero);
                    var formatted = rounded.ToString($"F{decimals}", CultureInfo.InvariantCulture);
                    return new CodeEvaluationResult(
                        true,
                        formatted,
                        $"ROUND({num}, {decimals}) rounded to {formatted}.",
                        diagnostics);
                }

                case "TEXT":
                {
                    var val = resolvedArgs.Count > 0 ? resolvedArgs[0].Value : string.Empty;
                    return new CodeEvaluationResult(
                        true,
                        val,
                        $"TEXT converted {val} into text \"{val}\".",
                        diagnostics);
                }

                case "MONTH":
                case "DAY":
                case "YEAR":
                {
                    var rawDate = resolvedArgs.Count > 0 ? resolvedArgs[0].Value : string.Empty;
                    if (!DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt) &&
                        !DateTime.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
                    {
                        var msg = $"{Prefix} requires a valid date value (such as 04/15/2026), but received \"{rawDate}\". Set a sample date in the Test Bench!";
                        diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg, 0));
                        return new CodeEvaluationResult(false, "Invalid Date", msg, diagnostics);
                    }

                    string partVal = Prefix switch
                    {
                        "MONTH" => dt.Month.ToString(CultureInfo.InvariantCulture),
                        "DAY" => dt.Day.ToString(CultureInfo.InvariantCulture),
                        _ => dt.Year.ToString(CultureInfo.InvariantCulture)
                    };

                    return new CodeEvaluationResult(
                        true,
                        partVal,
                        $"{Prefix}(\"{rawDate}\") extracted {partVal}.",
                        diagnostics);
                }

                default:
                    return new CodeEvaluationResult(true, GetCode(), $"Evaluated {Name ?? Prefix}.", diagnostics);
            }
        }

        protected (string Value, string Explanation) ResolveInput(
            int index,
            IReadOnlyDictionary<string, string> testValues,
            List<CodeDiagnostic> diagnostics)
        {
            if (index < 0 || index >= Inputs.Count)
                return (string.Empty, string.Empty);

            var input = Inputs[index];
            if (input.Value is CodeBase childBlock)
            {
                var childEval = childBlock.Evaluate(testValues);
                foreach (var d in childEval.Diagnostics)
                {
                    if (!diagnostics.Any(existing => existing.Message == d.Message))
                        diagnostics.Add(d);
                }
                return (childEval.Value, childEval.Explanation);
            }

            var raw = input.Value?.ToString() ?? string.Empty;
            var token = string.IsNullOrWhiteSpace(raw) ? input.Description : raw.Trim();

            if (IsSingleQuoted(token))
            {
                var unquoted = token[1..^1];
                return (unquoted, $"literal '{unquoted}'");
            }

            if (LookupTestValue(testValues, token, out var substituted))
            {
                var cleanSub = IsSingleQuoted(substituted.Trim())
                    ? substituted.Trim()[1..^1]
                    : substituted.Trim();
                return (cleanSub, $"{token} = \"{cleanSub}\"");
            }

            return (token, token);
        }

        protected static bool LookupTestValue(IReadOnlyDictionary<string, string> testValues, string key, out string value)
        {
            if (testValues.TryGetValue(key, out var direct) && direct != null)
            {
                value = direct;
                return true;
            }
            var match = testValues.FirstOrDefault(kvp => string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null)
            {
                value = match.Value;
                return true;
            }
            value = string.Empty;
            return false;
        }

        public static bool IsSingleQuoted(string text) =>
            text.Length >= 2 && text.StartsWith('\'') && text.EndsWith('\'');

        public static bool IsNumericLiteral(string text) =>
            double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _);

        public static bool IsBooleanLiteral(string text) =>
            string.Equals(text, "TRUE", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(text, "FALSE", StringComparison.OrdinalIgnoreCase);

        public static bool IsLikelyFieldReference(string text) =>
            text.Length >= 2 && (text[0] == 'F' || text[0] == 'f') && text[1..].All(char.IsDigit);

        private static bool IsReservedKeyword(string text) =>
            text.ToUpperInvariant() is "AND" or "OR" or "IF" or "CASE" or "SEPLIST" or "CONTAINS" or "ROUND" or "TEXT" or "MONTH" or "DAY" or "YEAR";

        protected static bool EvaluateConditionString(string conditionValue)
        {
            if (string.IsNullOrWhiteSpace(conditionValue)) return false;
            var trimmed = conditionValue.Trim();
            if (string.Equals(trimmed, "TRUE", StringComparison.OrdinalIgnoreCase) || trimmed == "1") return true;
            if (string.Equals(trimmed, "FALSE", StringComparison.OrdinalIgnoreCase) || trimmed == "0") return false;
            return !string.IsNullOrEmpty(trimmed);
        }

        protected static bool ValuesMatch(string a, string b)
        {
            if (double.TryParse(a, NumberStyles.Any, CultureInfo.InvariantCulture, out double da) &&
                double.TryParse(b, NumberStyles.Any, CultureInfo.InvariantCulture, out double db))
            {
                return Math.Abs(da - db) < 1e-9;
            }
            return string.Equals(a, b, StringComparison.Ordinal);
        }

        public static implicit operator string(CodeBase @base) => @base.GetCode();
        public static string operator +(CodeBase a, CodeBase b) => a.GetCode() + b.GetCode();
        public static string operator +(string a, CodeBase b) => a + b.GetCode();
        public static string operator +(CodeBase a, string b) => a.GetCode() + b;
    }
}

