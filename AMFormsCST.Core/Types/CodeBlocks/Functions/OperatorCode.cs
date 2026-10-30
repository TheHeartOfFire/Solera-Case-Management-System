using AMFormsCST.Core.Utils;
using System.Globalization;

namespace AMFormsCST.Core.Types.CodeBlocks.Functions;

public class OperatorCode : CodeBase
{
    public static IReadOnlyList<string> SupportedOperators { get; } =
    [
        "+", "-", "*", "/", "%", "e", "=", "!=", "<", ">", "<=", ">=", "AND", "OR", "&&", "||"
    ];

    private string _operator = "+";
    public string Operator
    {
        get => _operator;
        set
        {
            _operator = string.IsNullOrWhiteSpace(value) ? "+" : value;
            Prefix = _operator;
            var doc = FormgenReferenceCatalog.TryGetOperatorDoc(_operator);
            if (doc != null)
            {
                Name = $"Operator: {doc.ShortName} ({doc.Symbol})";
                Description = $"{doc.FullName} — {doc.HowItWorks} Example: {doc.Example}";
            }
        }
    }

    public FormgenOperatorDoc? OperatorDoc => FormgenReferenceCatalog.TryGetOperatorDoc(_operator);

    public OperatorCode() : this("+")
    {
    }

    public OperatorCode(string op)
    {
        Name = "Operator";
        Description = "Combines two expressions with an arithmetic, comparison, or logical operator.";
        AddInput("Left");
        AddInput("Right");
        Operator = op;
    }

    public override string DetailedExplanation =>
        OperatorDoc?.VerboseTooltip ?? Description ?? "Operator block.";

    public override bool IsMultiLinePseudoCode =>
        (Inputs.Count > 0 && Inputs[0].Value is CodeBase cb0 && (cb0.GetStructure() ?? cb0).IsMultiLinePseudoCode) ||
        (Inputs.Count > 1 && Inputs[1].Value is CodeBase cb1 && (cb1.GetStructure() ?? cb1).IsMultiLinePseudoCode) ||
        Operator is "AND" or "OR" or "&&" or "||";

    public override string DescribeInlineSummary()
    {
        var left = DescribeInputInline(0);
        var right = DescribeInputInline(1);
        return Operator switch
        {
            "=" => $"{left} equals {right}",
            "!=" => $"{left} is not equal to {right}",
            "<" => $"{left} is less than {right}",
            ">" => $"{left} is greater than {right}",
            "<=" => $"{left} is less than or equal to {right}",
            ">=" => $"{left} is greater than or equal to {right}",
            "%" => $"({left} modulo {right} — remainder after division)",
            "AND" or "&&" => $"({left} AND {right})",
            "OR" or "||" => $"({left} OR {right})",
            _ => $"({left} {Operator} {right})"
        };
    }

    public override string GetPseudoCode(int indentLevel = 0)
    {
        string indent = new(' ', indentLevel * 2);
        var leftInline = DescribeInputInline(0);
        var rightInline = DescribeInputInline(1);

        // Init variable assignment pattern: VAR = <ComplexExpression>
        if (Operator == "=" && Inputs.Count > 1 && Inputs[1].Value is CodeBase rhsBlock)
        {
            var unwrappedRhs = rhsBlock.GetStructure() ?? rhsBlock;
            if (unwrappedRhs.IsMultiLinePseudoCode)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"{indent}SET VARIABLE ({leftInline}) TO THE RESULT OF:");
                sb.Append(unwrappedRhs.GetPseudoCode(indentLevel + 1));
                return sb.ToString().TrimEnd();
            }
        }

        if (Operator is "AND" or "OR" or "&&" or "||")
        {
            var isAnd = Operator is "AND" or "&&";
            var sb = new System.Text.StringBuilder();
            var rule = isAnd
                ? "CHECK THAT BOTH CONDITIONS ARE TRUE (AND):"
                : "CHECK IF EITHER CONDITION IS TRUE (OR):";
            sb.AppendLine($"{indent}{rule}");
            sb.AppendLine($"{indent}  • Left Condition: {leftInline}");
            sb.AppendLine($"{indent}  • Right Condition: {rightInline}");
            sb.Append(isAnd
                ? $"{indent}  ➜ TRUE only if both Left and Right are TRUE; otherwise FALSE"
                : $"{indent}  ➜ TRUE if at least one of Left or Right is TRUE; otherwise FALSE");
            return sb.ToString();
        }

        if (Operator is "=" or "!=" or "<" or ">" or "<=" or ">=")
        {
            return $"{indent}COMPARE ({DescribeInlineSummary()}):\n" +
                   $"{indent}  • IF {DescribeInlineSummary()} ➜ Return TRUE\n" +
                   $"{indent}  • OTHERWISE ➜ Return FALSE";
        }

        // If either side is a multi-line block (like DayAndSuffixCode: TEXT(ROUND(...)) + CASE(...))
        bool leftMulti = Inputs.Count > 0 && Inputs[0].Value is CodeBase lCb && (lCb.GetStructure() ?? lCb).IsMultiLinePseudoCode;
        bool rightMulti = Inputs.Count > 1 && Inputs[1].Value is CodeBase rCb && (rCb.GetStructure() ?? rCb).IsMultiLinePseudoCode;
        if (leftMulti || rightMulti)
        {
            var sb = new System.Text.StringBuilder();
            string opDesc = Operator == "+" ? "COMBINE / ADD LEFT AND RIGHT PARTS (+):" : $"APPLY OPERATOR ({Operator}) TO LEFT AND RIGHT PARTS:";
            sb.AppendLine($"{indent}{opDesc}");
            sb.AppendLine($"{indent}  1. Left Part:");
            if (Inputs[0].Value is CodeBase leftChild)
                sb.AppendLine((leftChild.GetStructure() ?? leftChild).GetPseudoCode(indentLevel + 2));
            else
                sb.AppendLine($"{indent}    ➜ {leftInline}");

            sb.AppendLine($"{indent}  2. Right Part:");
            if (Inputs[1].Value is CodeBase rightChild)
                sb.Append((rightChild.GetStructure() ?? rightChild).GetPseudoCode(indentLevel + 2));
            else
                sb.Append($"{indent}    ➜ {rightInline}");

            return sb.ToString().TrimEnd();
        }

        return Operator switch
        {
            "+" => $"{indent}ADD OR CONCATENATE (+):\n{indent}  ➜ Combine ({leftInline}) + ({rightInline}) (adds numbers or joins text strings)",
            "-" => $"{indent}SUBTRACT (-):\n{indent}  ➜ Calculate ({leftInline}) minus ({rightInline})",
            "*" => $"{indent}MULTIPLY (*):\n{indent}  ➜ Calculate ({leftInline}) multiplied by ({rightInline})",
            "/" => $"{indent}DIVIDE (/):\n{indent}  ➜ Calculate ({leftInline}) divided by ({rightInline})",
            "%" => $"{indent}MODULUS / REMAINDER (%):\n{indent}  ➜ Divide ({leftInline}) by ({rightInline}) and return the remainder",
            "e" => $"{indent}SCIENTIFIC NOTATION (e):\n{indent}  ➜ Multiply ({leftInline}) by 10 to the power of ({rightInline})",
            _ => $"{indent}CALCULATE: {DescribeInlineSummary()}"
        };
    }

    public override string GetCode()
    {
        if (HasNoInputs() || Inputs.Count < 2) return string.Empty;

        var left = Inputs[0].Value is CodeBase cb0
            ? cb0.GetCode()
            : ((string)Inputs[0].Value).Equals(string.Empty) ? Inputs[0].Description : Inputs[0].Value.ToString();

        var right = Inputs[1].Value is CodeBase cb1
            ? cb1.GetCode()
            : ((string)Inputs[1].Value).Equals(string.Empty) ? Inputs[1].Description : Inputs[1].Value.ToString();

        return $"{left} {Operator} {right}";
    }

    protected override void ValidateBlockSpecific(List<CodeDiagnostic> diagnostics)
    {
        base.ValidateBlockSpecific(diagnostics);

        if (Operator is "-" or "*" or "/" or "%" or "e" or "<" or ">" or "<=" or ">=")
        {
            for (int i = 0; i < Math.Min(2, Inputs.Count); i++)
            {
                if (Inputs[i].Value is string rawStr && !string.IsNullOrWhiteSpace(rawStr))
                {
                    var fieldInfo = FormgenReferenceCatalog.TryGetField(rawStr);
                    if (fieldInfo != null && !fieldInfo.IsNumeric)
                    {
                        var opName = OperatorDoc?.FullName ?? $"Operator '{Operator}'";
                        diagnostics.Add(new CodeDiagnostic(
                            CodeDiagnosticSeverity.Warning,
                            $"{opName} expects numeric values, but F{fieldInfo.FieldNum} ('{fieldInfo.Description}', COBOL {fieldInfo.CobolPicture}) is a Text/Alphanumeric field. Wrap it in NUM(F{fieldInfo.FieldNum}) if it contains digits.",
                            i));
                    }
                }
            }
        }
    }

    protected override CodeEvaluationResult EvaluateCore(
        IReadOnlyDictionary<string, string> testValues,
        List<CodeDiagnostic> diagnostics)
    {
        if (Inputs.Count < 2)
            return new CodeEvaluationResult(false, string.Empty, "Operator requires Left and Right operands.", diagnostics);

        var (leftVal, _) = ResolveInput(0, testValues, diagnostics);
        var (rightVal, _) = ResolveInput(1, testValues, diagnostics);

        bool leftIsNum = double.TryParse(leftVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dLeft);
        bool rightIsNum = double.TryParse(rightVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double dRight);

        var opTitle = OperatorDoc?.ShortName ?? Operator;

        switch (Operator)
        {
            case "+":
            {
                if (leftIsNum && rightIsNum)
                {
                    var sum = (dLeft + dRight).ToString(CultureInfo.InvariantCulture);
                    return new CodeEvaluationResult(true, sum, $"Plus (+): Added {dLeft} + {dRight} = {sum}", diagnostics);
                }
                var concat = leftVal + rightVal;
                return new CodeEvaluationResult(true, concat, $"Plus (+): Concatenated \"{leftVal}\" + \"{rightVal}\" -> \"{concat}\"", diagnostics);
            }

            case "-":
            case "*":
            case "/":
            case "%":
            case "e":
            {
                if (!leftIsNum || !rightIsNum)
                {
                    var msg = $"{opTitle} ('{Operator}') requires numeric values, but received Left=\"{leftVal}\" and Right=\"{rightVal}\".";
                    diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg));
                    return new CodeEvaluationResult(false, "NaN", msg, diagnostics);
                }

                if (Operator is "/" or "%" && Math.Abs(dRight) < 1e-12)
                {
                    var msg = $"{opTitle} ('{Operator}'): Division or modulus by zero ({dLeft} {Operator} 0).";
                    diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg, 1));
                    return new CodeEvaluationResult(false, "DivideByZero", msg, diagnostics);
                }

                double res = Operator switch
                {
                    "-" => dLeft - dRight,
                    "*" => dLeft * dRight,
                    "/" => dLeft / dRight,
                    "e" => dLeft * Math.Pow(10, dRight),
                    _ => dLeft % dRight
                };
                var formatted = res.ToString(CultureInfo.InvariantCulture);
                return new CodeEvaluationResult(true, formatted, $"{opTitle} ({Operator}): {dLeft} {Operator} {dRight} = {formatted}", diagnostics);
            }

            case "=":
            {
                var rawLeft = Inputs[0].Value?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(rawLeft) && Inputs[1].Value is CodeBase && !LookupTestValue(testValues, rawLeft, out _))
                {
                    return new CodeEvaluationResult(
                        true,
                        $"{rawLeft} = {rightVal}",
                        $"Equals (=): Assigned \"{rightVal}\" to variable {rawLeft}.",
                        diagnostics);
                }

                bool eq = ValuesMatch(leftVal, rightVal);
                return new CodeEvaluationResult(true, eq ? "TRUE" : "FALSE", $"Equals (=): Checked {leftVal} = {rightVal} -> {(eq ? "TRUE" : "FALSE")}", diagnostics);
            }

            case "!=":
            {
                bool neq = !ValuesMatch(leftVal, rightVal);
                return new CodeEvaluationResult(true, neq ? "TRUE" : "FALSE", $"Not Equal (!=): Checked {leftVal} != {rightVal} -> {(neq ? "TRUE" : "FALSE")}", diagnostics);
            }

            case "<":
            case ">":
            case "<=":
            case ">=":
            {
                if (!leftIsNum || !rightIsNum)
                {
                    var msg = $"{opTitle} ('{Operator}') expects numeric operands, but received \"{leftVal}\" and \"{rightVal}\".";
                    diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg));
                    return new CodeEvaluationResult(false, "FALSE", msg, diagnostics);
                }

                bool cmp = Operator switch
                {
                    "<" => dLeft < dRight,
                    ">" => dLeft > dRight,
                    "<=" => dLeft <= dRight,
                    _ => dLeft >= dRight
                };
                return new CodeEvaluationResult(true, cmp ? "TRUE" : "FALSE", $"{opTitle} ({Operator}): Checked {dLeft} {Operator} {dRight} -> {(cmp ? "TRUE" : "FALSE")}", diagnostics);
            }

            case "AND":
            case "&&":
            {
                bool res = EvaluateConditionString(leftVal) && EvaluateConditionString(rightVal);
                return new CodeEvaluationResult(true, res ? "TRUE" : "FALSE", $"Logical AND ({Operator}): Checked {leftVal} {Operator} {rightVal} -> {(res ? "TRUE" : "FALSE")}", diagnostics);
            }

            case "OR":
            case "||":
            {
                bool res = EvaluateConditionString(leftVal) || EvaluateConditionString(rightVal);
                return new CodeEvaluationResult(true, res ? "TRUE" : "FALSE", $"Logical OR ({Operator}): Checked {leftVal} {Operator} {rightVal} -> {(res ? "TRUE" : "FALSE")}", diagnostics);
            }

            default:
                return new CodeEvaluationResult(true, GetCode(), $"Evaluated operator {Operator}.", diagnostics);
        }
    }

    public override CodeBase Clone()
    {
        var clone = (OperatorCode)base.Clone();
        clone.Operator = Operator;
        return clone;
    }
}

