using AMFormsCST.Core.Interfaces.CodeBlocks;
using AMFormsCST.Core.Utils;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AMFormsCST.Core.Types.CodeBlocks
{
    public abstract class CodeBase : ICodeBase
    {
        private static readonly Regex IdentifierTokenRegex = new(@"\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.Compiled);
        private static readonly Regex FFieldTokenRegex = new(@"\b[Ff](\d+)\b", RegexOptions.Compiled);

        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Prefix { get; set; }
        public List<CodeInput> Inputs { get; } = [];

        public virtual string DetailedExplanation
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Prefix))
                {
                    var funcDoc = FormgenReferenceCatalog.TryGetFunctionDoc(Prefix);
                    if (funcDoc != null)
                    {
                        return funcDoc.VerboseTooltip;
                    }

                    var opDoc = FormgenReferenceCatalog.TryGetOperatorDoc(Prefix);
                    if (opDoc != null)
                    {
                        return opDoc.VerboseTooltip;
                    }
                }

                var sb = new StringBuilder();
                sb.AppendLine($"{Name ?? Prefix ?? "Formula Snippet"}");
                if (!string.IsNullOrWhiteSpace(Description))
                {
                    sb.AppendLine();
                    sb.AppendLine($"How It Works: {Description}");
                }
                if (Inputs.Count > 0)
                {
                    sb.AppendLine($"Inputs ({Inputs.Count}): {string.Join(", ", Inputs.Select(i => i.Description))}");
                }
                var structure = GetStructure();
                if (structure != null)
                {
                    sb.AppendLine($"Expanded FormGen Structure: {structure.GetCode()}");
                    sb.Append("Tip: Click 'Deconstruct' to break this snippet apart into its primitive Function blocks, and 'Reconstruct' to fold it back into a single block.");
                }
                return sb.ToString().TrimEnd();
            }
        }

        public virtual string GetPseudoCode(int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * 2);
            var structure = GetStructure();
            if (structure != null)
            {
                if (indentLevel == 0 && !string.IsNullOrWhiteSpace(Name))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"{indent}FORMULA: {Name}");
                    if (!string.IsNullOrWhiteSpace(Description))
                    {
                        sb.AppendLine($"{indent}GOAL: {Description}");
                    }
                    sb.AppendLine($"{indent}STEP-BY-STEP LOGIC:");
                    sb.Append(structure.GetPseudoCode(indentLevel + 1));
                    return sb.ToString().TrimEnd();
                }
                return structure.GetPseudoCode(indentLevel);
            }

            return $"{indent}Evaluate {Name ?? Prefix ?? "Expression"}: {GetCode()}";
        }

        protected string DescribeInputInline(int idx)
        {
            if (idx < 0 || idx >= Inputs.Count) return "(missing)";
            var input = Inputs[idx];
            if (input.Value is CodeBase childBlock)
            {
                return childBlock.DescribeInlineSummary();
            }

            var raw = input.Value?.ToString() ?? string.Empty;
            var token = string.IsNullOrWhiteSpace(raw) ? $"<{input.Description}>" : raw.Trim();
            var fieldInfo = FormgenReferenceCatalog.TryGetField(token);
            if (fieldInfo != null)
            {
                return $"{token} [{fieldInfo.Description}]";
            }
            return token;
        }

        public virtual string DescribeInlineSummary()
        {
            var structure = GetStructure();
            if (structure != null)
            {
                return structure.DescribeInlineSummary();
            }
            return GetCode();
        }

        protected string FormatInputBranch(int idx, int indentLevel)
        {
            string indent = new(' ', indentLevel * 2);
            if (idx < 0 || idx >= Inputs.Count)
            {
                return $"{indent}➜ (none)";
            }

            var input = Inputs[idx];
            if (input.Value is CodeBase childBlock)
            {
                var childStructure = childBlock.GetStructure() ?? childBlock;
                if (childStructure.IsMultiLinePseudoCode)
                {
                    return childStructure.GetPseudoCode(indentLevel);
                }
                return $"{indent}➜ Return {childStructure.DescribeInlineSummary()}";
            }

            return $"{indent}➜ Return {DescribeInputInline(idx)}";
        }

        public virtual bool IsMultiLinePseudoCode =>
            GetStructure()?.IsMultiLinePseudoCode ?? false;

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
                    else
                    {
                        ValidateRawInputString(strVal.Trim(), input.Index, diagnostics);
                    }
                }
            }

            ValidateBlockSpecific(diagnostics);
            return diagnostics;
        }

        private void ValidateRawInputString(string trimmed, int inputIndex, List<CodeDiagnostic> diagnostics)
        {
            if (IsSingleQuoted(trimmed)) return;

            if (string.Equals(trimmed, "TODAY", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new CodeDiagnostic(
                    CodeDiagnosticSeverity.Warning,
                    "In FormGen, TODAY() is a function and must include empty parentheses: write TODAY() instead of TODAY.",
                    inputIndex));
            }
            else if (string.Equals(trimmed, "PI()", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new CodeDiagnostic(
                    CodeDiagnosticSeverity.Warning,
                    "In FormGen, PI is a constant and DOES NOT use parentheses: write PI instead of PI().",
                    inputIndex));
            }

            foreach (Match m in FFieldTokenRegex.Matches(trimmed))
            {
                var token = m.Value.ToUpperInvariant();
                var fieldInfo = FormgenReferenceCatalog.TryGetField(token);
                if (fieldInfo == null)
                {
                    diagnostics.Add(new CodeDiagnostic(
                        CodeDiagnosticSeverity.Warning,
                        $"Field '{token}' does not exist in the master FormGen field list (fields.json, F1–F3100). Check the field number for typos.",
                        inputIndex));
                }
                else if (fieldInfo.IsExplicitlyExcludedFromVm)
                {
                    diagnostics.Add(new CodeDiagnostic(
                        CodeDiagnosticSeverity.Warning,
                        $"Field {token} ('{fieldInfo.Description}' / {fieldInfo.VariableName}) is explicitly marked 'Vehicle Only = N' in the Vehicle Merchandising list and will NOT work on VM forms.",
                        inputIndex));
                }
            }
        }

        protected virtual void ValidateBlockSpecific(List<CodeDiagnostic> diagnostics)
        {
            if (Prefix is "SEPLIST")
            {
                if (Inputs.Count > 0 && Inputs[0].Value is string sepStr && !string.IsNullOrWhiteSpace(sepStr))
                {
                    var trimmed = sepStr.Trim();
                    if (!IsSingleQuoted(trimmed) && !IsLikelyFieldReference(trimmed))
                    {
                        diagnostics.Add(new CodeDiagnostic(
                            CodeDiagnosticSeverity.Warning,
                            $"In SEPLIST (Separated List), the Separator ({trimmed}) is not wrapped in single quotes. In FormGen, literal text like '{trimmed}' must be single-quoted, otherwise FormGen treats it as a variable name.",
                            0));
                    }
                }

                for (int i = 1; i < Inputs.Count; i++)
                {
                    if (Inputs[i].Value is string itemStr && !string.IsNullOrWhiteSpace(itemStr))
                    {
                        var fieldInfo = FormgenReferenceCatalog.TryGetField(itemStr);
                        if (fieldInfo != null && fieldInfo.IsNumeric)
                        {
                            diagnostics.Add(new CodeDiagnostic(
                                CodeDiagnosticSeverity.Warning,
                                $"Field F{fieldInfo.FieldNum} ('{fieldInfo.Description}', numeric {fieldInfo.CobolPicture}) is placed directly inside SEPLIST. In FormGen, numeric fields don't cooperate well with SEPLIST on their own — wrap it in 'Seplist Number' (IF(F{fieldInfo.FieldNum} != 0, ROUND(F{fieldInfo.FieldNum}, {fieldInfo.RightSize}), '')) or TEXT(ROUND(...)).",
                                i));
                        }
                    }
                }
            }
            else if (Prefix is "ROUND")
            {
                if (Inputs.Count > 0 && Inputs[0].Value is string numStr && !string.IsNullOrWhiteSpace(numStr))
                {
                    var fieldInfo = FormgenReferenceCatalog.TryGetField(numStr);
                    if (fieldInfo != null && !fieldInfo.IsNumeric)
                    {
                        diagnostics.Add(new CodeDiagnostic(
                            CodeDiagnosticSeverity.Warning,
                            $"Field F{fieldInfo.FieldNum} ('{fieldInfo.Description}', COBOL {fieldInfo.CobolPicture}) is a Text field, not a numeric field. ROUND expects a number — wrap with NUM(F{fieldInfo.FieldNum}) if it contains digits.",
                            0));
                    }
                }

                if (Inputs.Count > 1 && Inputs[1].Value is string decStr && !string.IsNullOrWhiteSpace(decStr))
                {
                    var trimmed = decStr.Trim();
                    if (IsSingleQuoted(trimmed) || (!int.TryParse(trimmed, out _) && !IsLikelyFieldReference(trimmed)))
                    {
                        diagnostics.Add(new CodeDiagnostic(
                            CodeDiagnosticSeverity.Warning,
                            $"ROUND's 'Decimal Places' input ({trimmed}) should be an unquoted whole number (such as 2 for currency or 0 for whole numbers).",
                            1));
                    }
                }
            }
            else if (Prefix is "CONTAINS" && Inputs.Count > 1 && Inputs[1].Value is string subStr && !string.IsNullOrWhiteSpace(subStr))
            {
                var trimmed = subStr.Trim();
                if (!IsSingleQuoted(trimmed) && !IsLikelyFieldReference(trimmed))
                {
                    diagnostics.Add(new CodeDiagnostic(
                        CodeDiagnosticSeverity.Warning,
                        $"In CONTAINS (Substring Search), the search substring ({trimmed}) is not single-quoted. Wrap literal text in single quotes like '{trimmed}' unless it is a field name.",
                        1));
                }
            }
            else if (Prefix is "MONTH" or "DAY" or "YEAR")
            {
                if (Inputs.Count > 0 && Inputs[0].Value is string dateStr && !string.IsNullOrWhiteSpace(dateStr))
                {
                    var trimmed = dateStr.Trim();
                    if (IsLikelyFieldReference(trimmed))
                    {
                        var fieldInfo = FormgenReferenceCatalog.TryGetField(trimmed);
                        var fieldDesc = fieldInfo != null ? $" ('{fieldInfo.Description}', COBOL {fieldInfo.CobolPicture})" : string.Empty;
                        diagnostics.Add(new CodeDiagnostic(
                            CodeDiagnosticSeverity.Warning,
                            $"FormGen Quirk: {Prefix}() only works directly with TODAY(), DATE(...), or Date Prompts! Passing {trimmed.ToUpperInvariant()}{fieldDesc} directly into {Prefix}() may fail in FormGen unless wrapped in DATE({trimmed.ToUpperInvariant()}) or replaced with its dedicated date-part F-field (e.g. F45 Year, F46 Month, F47 Day).",
                            0));
                    }
                }
            }
            else if (Prefix is "FLOOR" or "CEILING" or "ABS" or "SQRT" or "CASELTE" or "MATCHRANGE" or "LOG" or "LOG10" or "SIN" or "COS" or "TAN" or "ASIN" or "ACOS" or "ATAN" or "RAD" or "DEG")
            {
                if (Inputs.Count > 0 && Inputs[0].Value is string arg0 && !string.IsNullOrWhiteSpace(arg0))
                {
                    var trimmed = arg0.Trim();
                    var fieldInfo = FormgenReferenceCatalog.TryGetField(trimmed);
                    if (fieldInfo != null && !fieldInfo.IsNumeric)
                    {
                        diagnostics.Add(new CodeDiagnostic(
                            CodeDiagnosticSeverity.Warning,
                            $"Field F{fieldInfo.FieldNum} ('{fieldInfo.Description}', COBOL {fieldInfo.CobolPicture}) is a Text field, but {Prefix}() requires a numeric value.",
                            0));
                    }
                    else if (double.TryParse(trimmed, NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                    {
                        if (Prefix is "SQRT" && dVal < 0)
                        {
                            diagnostics.Add(new CodeDiagnostic(
                                CodeDiagnosticSeverity.Warning,
                                $"SQRT() requires a positive number (>= 0), but received {trimmed}.",
                                0));
                        }
                        else if (Prefix is "ASIN" or "ACOS" && (dVal < -1.0 || dVal > 1.0))
                        {
                            diagnostics.Add(new CodeDiagnostic(
                                CodeDiagnosticSeverity.Warning,
                                $"{Prefix}() requires a number between -1 and 1 inclusive, but received {trimmed}.",
                                0));
                        }
                    }
                }
            }
            else if (Prefix is "TRIM" or "LTRIM" or "RTRIM")
            {
                diagnostics.Add(new CodeDiagnostic(
                    CodeDiagnosticSeverity.Info,
                    $"FormGen Note: FormGen automatically trims leading and trailing spaces on fields, so {Prefix}() is functionally redundant in FormGen.",
                    0));
            }
            else if (Prefix is "DATE")
            {
                diagnostics.Add(new CodeDiagnostic(
                    CodeDiagnosticSeverity.Info,
                    "FormGen Note: DATE() is functionally useless on its own unless nested inside DAY(DATE(...)), MONTH(DATE(...)), or YEAR(DATE(...)).",
                    0));
            }
            else if (Prefix is "NUM")
            {
                diagnostics.Add(new CodeDiagnostic(
                    CodeDiagnosticSeverity.Info,
                    "FormGen Note: Using NUM() alone to format numbers for output produces strange results in FormGen. Use TEXT(ROUND(Number, Decimals)) for display.",
                    0));
            }
        }

        public virtual CodeEvaluationResult Evaluate(IReadOnlyDictionary<string, string>? testValues = null)
        {
            var values = testValues ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var diagnostics = Validate().ToList();

            return EvaluateRuntime(values, diagnostics);
        }

        internal CodeEvaluationResult EvaluateRuntime(
            IReadOnlyDictionary<string, string> values,
            List<CodeDiagnostic> diagnostics)
        {
            var structure = GetStructure();
            if (structure is not null)
            {
                var structDiagnostics = new List<CodeDiagnostic>();
                var structResult = structure.EvaluateRuntime(values, structDiagnostics);
                foreach (var d in structResult.Diagnostics)
                {
                    if (!diagnostics.Any(existing => existing.Message == d.Message))
                        diagnostics.Add(d);
                }

                return structResult with
                {
                    Explanation = $"{Name}: {structResult.Explanation}",
                    Diagnostics = diagnostics
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
                        $"SEPLIST (Separated List) joined {nonEmptyItems.Count} non-empty item(s) using separator \"{sep}\" (empty items are automatically skipped in FormGen).",
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

                case "XIF":
                case "XXIF":
                {
                    bool cond = resolvedArgs.Count > 0 && EvaluateConditionString(resolvedArgs[0].Value);
                    var mark = Prefix == "XXIF" ? "XX" : "X";
                    var output = cond ? mark : string.Empty;
                    return new CodeEvaluationResult(
                        true,
                        output,
                        $"{Prefix} condition evaluated to {cond.ToString().ToUpperInvariant()} -> returned \"{(cond ? mark : "(blank)")}\".",
                        diagnostics);
                }

                case "NOT":
                {
                    bool cond = resolvedArgs.Count > 0 && EvaluateConditionString(resolvedArgs[0].Value);
                    bool inverted = !cond;
                    return new CodeEvaluationResult(
                        true,
                        inverted ? "TRUE" : "FALSE",
                        $"NOT inverted {cond.ToString().ToUpperInvariant()} -> {(inverted ? "TRUE" : "FALSE")}.",
                        diagnostics);
                }

                case "AND":
                {
                    bool allTrue = resolvedArgs.Count > 0 && resolvedArgs.All(a => EvaluateConditionString(a.Value));
                    return new CodeEvaluationResult(
                        true,
                        allTrue ? "TRUE" : "FALSE",
                        $"AND checked {resolvedArgs.Count} condition(s) -> {(allTrue ? "TRUE (all true)" : "FALSE")}.",
                        diagnostics);
                }

                case "OR":
                {
                    bool anyTrue = resolvedArgs.Any(a => EvaluateConditionString(a.Value));
                    return new CodeEvaluationResult(
                        true,
                        anyTrue ? "TRUE" : "FALSE",
                        $"OR checked {resolvedArgs.Count} condition(s) -> {(anyTrue ? "TRUE (at least one true)" : "FALSE")}.",
                        diagnostics);
                }

                case "BLANK":
                case "NOTBLANK":
                {
                    var val = resolvedArgs.Count > 0 ? resolvedArgs[0].Value : string.Empty;
                    bool isBlank = string.IsNullOrWhiteSpace(val);
                    bool res = Prefix == "BLANK" ? isBlank : !isBlank;
                    return new CodeEvaluationResult(
                        true,
                        res ? "TRUE" : "FALSE",
                        $"{Prefix}(\"{val}\") -> {(res ? "TRUE" : "FALSE")}.",
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

                case "CASELTE":
                {
                    if (resolvedArgs.Count < 4 || !double.TryParse(resolvedArgs[0].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double cmpNum))
                        return new CodeEvaluationResult(false, string.Empty, "CASELTE requires a numeric Comparison value, Threshold/Result pairs, and a Default.", diagnostics);

                    for (int i = 1; i < resolvedArgs.Count - 1; i += 2)
                    {
                        if (double.TryParse(resolvedArgs[i].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double threshold) && cmpNum <= threshold)
                        {
                            var resVal = resolvedArgs[i + 1].Value;
                            return new CodeEvaluationResult(
                                true,
                                resVal,
                                $"CASELTE matched {cmpNum} <= {threshold} -> returned \"{resVal}\".",
                                diagnostics);
                        }
                    }

                    var defVal = resolvedArgs[^1].Value;
                    return new CodeEvaluationResult(true, defVal, $"CASELTE: {cmpNum} exceeded all thresholds -> returned Default \"{defVal}\".", diagnostics);
                }

                case "MATCHRANGE":
                {
                    if (resolvedArgs.Count < 5 || !double.TryParse(resolvedArgs[0].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double cmpNum))
                        return new CodeEvaluationResult(false, string.Empty, "MATCHRANGE requires numeric Comparison, (Lower, Upper, Result) triplets, and Default.", diagnostics);

                    for (int i = 1; i <= resolvedArgs.Count - 4; i += 3)
                    {
                        if (double.TryParse(resolvedArgs[i].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double low) &&
                            double.TryParse(resolvedArgs[i + 1].Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double high) &&
                            cmpNum >= low && cmpNum <= high)
                        {
                            var resVal = resolvedArgs[i + 2].Value;
                            return new CodeEvaluationResult(
                                true,
                                resVal,
                                $"MATCHRANGE matched {cmpNum} within [{low}, {high}] -> returned \"{resVal}\".",
                                diagnostics);
                        }
                    }

                    var defVal = resolvedArgs[^1].Value;
                    return new CodeEvaluationResult(true, defVal, $"MATCHRANGE: {cmpNum} was not in any range -> returned Default \"{defVal}\".", diagnostics);
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

                case "STRIP":
                {
                    if (resolvedArgs.Count < 2)
                        return new CodeEvaluationResult(false, string.Empty, "STRIP requires SourceText and TextToRemove.", diagnostics);
                    var stripped = string.IsNullOrEmpty(resolvedArgs[1].Value)
                        ? resolvedArgs[0].Value
                        : resolvedArgs[0].Value.Replace(resolvedArgs[1].Value, string.Empty, StringComparison.Ordinal);
                    return new CodeEvaluationResult(true, stripped, $"STRIP removed \"{resolvedArgs[1].Value}\" from \"{resolvedArgs[0].Value}\" -> \"{stripped}\".", diagnostics);
                }

                case "REPLACE":
                {
                    if (resolvedArgs.Count < 3)
                        return new CodeEvaluationResult(false, string.Empty, "REPLACE requires SourceText, UnwantedText, and ReplacementText.", diagnostics);
                    var replaced = string.IsNullOrEmpty(resolvedArgs[1].Value)
                        ? resolvedArgs[0].Value
                        : resolvedArgs[0].Value.Replace(resolvedArgs[1].Value, resolvedArgs[2].Value, StringComparison.Ordinal);
                    return new CodeEvaluationResult(true, replaced, $"REPLACE replaced \"{resolvedArgs[1].Value}\" with \"{resolvedArgs[2].Value}\" -> \"{replaced}\".", diagnostics);
                }

                case "LEN":
                {
                    var s = resolvedArgs.Count > 0 ? resolvedArgs[0].Value : string.Empty;
                    var len = s.Length.ToString(CultureInfo.InvariantCulture);
                    return new CodeEvaluationResult(true, len, $"LEN(\"{s}\") = {len} character(s).", diagnostics);
                }

                case "TRIM":
                case "LTRIM":
                case "RTRIM":
                {
                    var s = resolvedArgs.Count > 0 ? resolvedArgs[0].Value : string.Empty;
                    var trimmed = Prefix switch
                    {
                        "LTRIM" => s.TrimStart(),
                        "RTRIM" => s.TrimEnd(),
                        _ => s.Trim()
                    };
                    return new CodeEvaluationResult(true, trimmed, $"{Prefix} result -> \"{trimmed}\".", diagnostics);
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

                case "FLOOR":
                case "CEILING":
                case "ABS":
                case "SQRT":
                case "LOG":
                case "LOG10":
                case "SIN":
                case "COS":
                case "TAN":
                case "ASIN":
                case "ACOS":
                case "ATAN":
                case "SINH":
                case "COSH":
                case "TANH":
                case "RAD":
                case "DEG":
                {
                    var rawNum = resolvedArgs.Count > 0 ? resolvedArgs[0].Value : string.Empty;
                    if (!double.TryParse(rawNum, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                    {
                        var msg = $"{Prefix} requires a numeric argument, but received \"{rawNum}\".";
                        diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg, 0));
                        return new CodeEvaluationResult(false, "NaN", msg, diagnostics);
                    }

                    double calc = Prefix switch
                    {
                        "FLOOR" => Math.Floor(d),
                        "CEILING" => Math.Ceiling(d),
                        "ABS" => Math.Abs(d),
                        "SQRT" => Math.Sqrt(d),
                        "LOG" => Math.Log(d),
                        "LOG10" => Math.Log10(d),
                        "SIN" => Math.Sin(d),
                        "COS" => Math.Cos(d),
                        "TAN" => Math.Tan(d),
                        "ASIN" => Math.Asin(d),
                        "ACOS" => Math.Acos(d),
                        "ATAN" => Math.Atan(d),
                        "SINH" => Math.Sinh(d),
                        "COSH" => Math.Cosh(d),
                        "TANH" => Math.Tanh(d),
                        "RAD" => d * (Math.PI / 180.0),
                        _ => d * (180.0 / Math.PI)
                    };
                    var formatted = calc.ToString(CultureInfo.InvariantCulture);
                    return new CodeEvaluationResult(true, formatted, $"{Prefix}({d}) = {formatted}.", diagnostics);
                }

                case "MAX":
                case "MIN":
                {
                    var nums = new List<double>();
                    foreach (var a in resolvedArgs)
                    {
                        if (double.TryParse(a.Value, NumberStyles.Any, CultureInfo.InvariantCulture, out double n))
                            nums.Add(n);
                    }
                    if (nums.Count == 0)
                        return new CodeEvaluationResult(false, "NaN", $"{Prefix} requires numeric arguments.", diagnostics);
                    double best = Prefix == "MAX" ? nums.Max() : nums.Min();
                    var formatted = best.ToString(CultureInfo.InvariantCulture);
                    return new CodeEvaluationResult(true, formatted, $"{Prefix}({string.Join(", ", nums)}) = {formatted}.", diagnostics);
                }

                case "TEXT":
                case "NUM":
                case "DATE":
                {
                    var val = resolvedArgs.Count > 0 ? resolvedArgs[0].Value : string.Empty;
                    return new CodeEvaluationResult(
                        true,
                        val,
                        $"{Prefix} converted {val} -> \"{val}\".",
                        diagnostics);
                }

                case "TODAY":
                {
                    var todayStr = DateTime.Today.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
                    return new CodeEvaluationResult(true, todayStr, $"TODAY() returned current date {todayStr}.", diagnostics);
                }

                case "PI":
                {
                    var piStr = Math.PI.ToString("G17", CultureInfo.InvariantCulture);
                    return new CodeEvaluationResult(true, piStr, $"PI constant = {piStr}.", diagnostics);
                }

                case "RANDOM":
                {
                    return new CodeEvaluationResult(true, "0.262641", "RANDOM() returns a random decimal between 0 and 1.", diagnostics);
                }

                case "ADDDAYS":
                case "ADDWEEKS":
                case "ADDMONTHS":
                case "ADDYEARS":
                {
                    if (resolvedArgs.Count < 2)
                        return new CodeEvaluationResult(false, string.Empty, $"{Prefix} requires Date and WholeNumber.", diagnostics);

                    var rawDate = resolvedArgs[0].Value;
                    if (!DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt) &&
                        !DateTime.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
                    {
                        var msg = $"{Prefix} requires a valid date value, but received \"{rawDate}\".";
                        diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg, 0));
                        return new CodeEvaluationResult(false, "Invalid Date", msg, diagnostics);
                    }

                    if (!int.TryParse(resolvedArgs[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int delta))
                    {
                        var msg = $"{Prefix} requires a whole number for its 2nd argument, but received \"{resolvedArgs[1].Value}\".";
                        diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg, 1));
                        return new CodeEvaluationResult(false, "NaN", msg, diagnostics);
                    }

                    DateTime shifted = Prefix switch
                    {
                        "ADDDAYS" => dt.AddDays(delta),
                        "ADDWEEKS" => dt.AddDays(delta * 7),
                        "ADDMONTHS" => dt.AddMonths(delta),
                        _ => dt.AddYears(delta)
                    };
                    var outDate = shifted.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
                    return new CodeEvaluationResult(true, outDate, $"{Prefix}(\"{rawDate}\", {delta}) -> {outDate}.", diagnostics);
                }

                case "MONTH":
                case "DAY":
                case "YEAR":
                {
                    var rawDate = resolvedArgs.Count > 0 ? resolvedArgs[0].Value : string.Empty;
                    if (!DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt) &&
                        !DateTime.TryParse(rawDate, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
                    {
                        var msg = $"{Prefix} requires a valid date value (such as 04/15/2026 or TODAY()), but received \"{rawDate}\". Set a sample date in the Test Bench!";
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
                var childEval = childBlock.EvaluateRuntime(testValues, new List<CodeDiagnostic>());
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

            if (string.Equals(token, "TODAY()", StringComparison.OrdinalIgnoreCase))
            {
                var todayStr = DateTime.Today.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
                return (todayStr, $"TODAY() = \"{todayStr}\"");
            }

            if (string.Equals(token, "PI", StringComparison.OrdinalIgnoreCase))
            {
                var piStr = Math.PI.ToString("G17", CultureInfo.InvariantCulture);
                return (piStr, $"PI = {piStr}");
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
            text.ToUpperInvariant() is "AND" or "OR" or "NOT" or "IF" or "XIF" or "XXIF" or "CASE" or "CASELTE" or "MATCHRANGE"
                or "SEPLIST" or "CONTAINS" or "STRIP" or "REPLACE" or "BLANK" or "NOTBLANK" or "LEN" or "TRIM" or "LTRIM" or "RTRIM"
                or "ROUND" or "FLOOR" or "CEILING" or "ABS" or "MAX" or "MIN" or "SQRT" or "LOG" or "LOG10"
                or "TEXT" or "NUM" or "DATE" or "TODAY" or "RANDOM" or "PI"
                or "MONTH" or "DAY" or "YEAR" or "ADDDAYS" or "ADDWEEKS" or "ADDMONTHS" or "ADDYEARS";

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

