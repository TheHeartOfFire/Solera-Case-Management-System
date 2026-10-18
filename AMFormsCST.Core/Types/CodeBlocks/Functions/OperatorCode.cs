using System.Globalization;

namespace AMFormsCST.Core.Types.CodeBlocks.Functions;

public class OperatorCode : CodeBase
{
    public static IReadOnlyList<string> SupportedOperators { get; } =
    [
        "+", "-", "*", "/", "%", "=", "!=", "<", ">", "<=", ">=", "AND", "OR"
    ];

    private string _operator = "+";
    public string Operator
    {
        get => _operator;
        set
        {
            _operator = string.IsNullOrWhiteSpace(value) ? "+" : value;
            Prefix = _operator;
        }
    }

    public OperatorCode() : this("+")
    {
    }

    public OperatorCode(string op)
    {
        Name = "Operator";
        Operator = op;
        Description = "Combines two expressions with an arithmetic, comparison, or logical operator.";
        AddInput("Left");
        AddInput("Right");
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

        switch (Operator)
        {
            case "+":
            {
                if (leftIsNum && rightIsNum)
                {
                    var sum = (dLeft + dRight).ToString(CultureInfo.InvariantCulture);
                    return new CodeEvaluationResult(true, sum, $"{dLeft} + {dRight} = {sum}", diagnostics);
                }
                var concat = leftVal + rightVal;
                return new CodeEvaluationResult(true, concat, $"Concatenated \"{leftVal}\" + \"{rightVal}\" -> \"{concat}\"", diagnostics);
            }

            case "-":
            case "*":
            case "/":
            case "%":
            {
                if (!leftIsNum || !rightIsNum)
                {
                    var msg = $"Operator '{Operator}' requires numeric values, but received Left=\"{leftVal}\" and Right=\"{rightVal}\".";
                    diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg));
                    return new CodeEvaluationResult(false, "NaN", msg, diagnostics);
                }

                if (Operator is "/" or "%" && Math.Abs(dRight) < 1e-12)
                {
                    var msg = $"Division or modulo by zero ({dLeft} {Operator} 0).";
                    diagnostics.Add(new CodeDiagnostic(CodeDiagnosticSeverity.Error, msg, 1));
                    return new CodeEvaluationResult(false, "DivideByZero", msg, diagnostics);
                }

                double res = Operator switch
                {
                    "-" => dLeft - dRight,
                    "*" => dLeft * dRight,
                    "/" => dLeft / dRight,
                    _ => dLeft % dRight
                };
                var formatted = res.ToString(CultureInfo.InvariantCulture);
                return new CodeEvaluationResult(true, formatted, $"{dLeft} {Operator} {dRight} = {formatted}", diagnostics);
            }

            case "=":
            {
                // If Left is an assignment target variable like FUELTYPE that wasn't substituted and Right is a block
                var rawLeft = Inputs[0].Value?.ToString()?.Trim() ?? string.Empty;
                if (!string.IsNullOrEmpty(rawLeft) && Inputs[1].Value is CodeBase && !LookupTestValue(testValues, rawLeft, out _))
                {
                    return new CodeEvaluationResult(
                        true,
                        $"{rawLeft} = {rightVal}",
                        $"Assigned \"{rightVal}\" to variable {rawLeft}.",
                        diagnostics);
                }

                bool eq = ValuesMatch(leftVal, rightVal);
                return new CodeEvaluationResult(true, eq ? "TRUE" : "FALSE", $"Checked {leftVal} = {rightVal} -> {(eq ? "TRUE" : "FALSE")}", diagnostics);
            }

            case "!=":
            {
                bool neq = !ValuesMatch(leftVal, rightVal);
                return new CodeEvaluationResult(true, neq ? "TRUE" : "FALSE", $"Checked {leftVal} != {rightVal} -> {(neq ? "TRUE" : "FALSE")}", diagnostics);
            }

            case "<":
            case ">":
            case "<=":
            case ">=":
            {
                if (!leftIsNum || !rightIsNum)
                {
                    var msg = $"Comparison '{Operator}' expects numeric operands, but received \"{leftVal}\" and \"{rightVal}\".";
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
                return new CodeEvaluationResult(true, cmp ? "TRUE" : "FALSE", $"Checked {dLeft} {Operator} {dRight} -> {(cmp ? "TRUE" : "FALSE")}", diagnostics);
            }

            case "AND":
            {
                bool res = EvaluateConditionString(leftVal) && EvaluateConditionString(rightVal);
                return new CodeEvaluationResult(true, res ? "TRUE" : "FALSE", $"Checked {leftVal} AND {rightVal} -> {(res ? "TRUE" : "FALSE")}", diagnostics);
            }

            case "OR":
            {
                bool res = EvaluateConditionString(leftVal) || EvaluateConditionString(rightVal);
                return new CodeEvaluationResult(true, res ? "TRUE" : "FALSE", $"Checked {leftVal} OR {rightVal} -> {(res ? "TRUE" : "FALSE")}", diagnostics);
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

