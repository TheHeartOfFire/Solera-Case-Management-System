using AMFormsCST.Core.Interfaces.CodeBlocks;
using AMFormsCST.Core.Interfaces.FormgenUtils;
using AMFormsCST.Core.Utils;

namespace AMFormsCST.Core.Types.CodeBlocks.Functions;

public class FormgenFunctionCode : CodeBase
{
    public bool UsesParentheses { get; set; } = true;

    public FormgenFunctionCode() : this("XIF", "Checkbox Single-X (XIF)", ["Condition"])
    {
    }

    public FormgenFunctionCode(
        string prefix,
        string name,
        IReadOnlyList<string> inputDescriptions,
        bool usesParentheses = true)
    {
        Prefix = prefix;
        Name = name;
        UsesParentheses = usesParentheses;
        Description = FormgenReferenceCatalog.TryGetFunctionDoc(prefix)?.HowItWorks
            ?? $"{name} ({prefix}) function.";

        foreach (var desc in inputDescriptions)
        {
            AddInput(desc);
        }
    }

    public override string GetCode()
    {
        if (HasNoInputs())
        {
            return UsesParentheses ? $"{Prefix}()" : (Prefix ?? string.Empty);
        }
        return base.GetCode();
    }

    public override bool IsMultiLinePseudoCode =>
        Prefix is "XIF" or "XXIF" or "BLANK" or "NOTBLANK" or "NOT" or "REPLACE" or "STRIP";

    public override string DescribeInlineSummary()
    {
        return Prefix switch
        {
            "TODAY" => "TODAY's current date (MM/DD/YYYY)",
            "PI" => "PI (3.14159...)",
            "RANDOM" => "random decimal between 0 and 1",
            "BLANK" => $"{DescribeInputInline(0)} is blank",
            "NOTBLANK" => $"{DescribeInputInline(0)} is not blank",
            "NOT" => $"NOT ({DescribeInputInline(0)})",
            "LEN" => $"character length of ({DescribeInputInline(0)})",
            "NUM" => $"numeric value of ({DescribeInputInline(0)})",
            "DATE" => $"date value of ({DescribeInputInline(0)})",
            "FLOOR" => $"round ({DescribeInputInline(0)}) down to whole number",
            "CEILING" => $"round ({DescribeInputInline(0)}) up to whole number",
            "ABS" => $"absolute value of ({DescribeInputInline(0)})",
            "SQRT" => $"square root of ({DescribeInputInline(0)})",
            "ADDDAYS" => $"add {DescribeInputInline(1)} day(s) to {DescribeInputInline(0)}",
            "ADDWEEKS" => $"add {DescribeInputInline(1)} week(s) to {DescribeInputInline(0)}",
            "ADDMONTHS" => $"add {DescribeInputInline(1)} month(s) to {DescribeInputInline(0)}",
            "ADDYEARS" => $"add {DescribeInputInline(1)} year(s) to {DescribeInputInline(0)}",
            _ => GetCode()
        };
    }

    public override string GetPseudoCode(int indentLevel = 0)
    {
        string indent = new(' ', indentLevel * 2);
        return Prefix switch
        {
            "XIF" =>
                $"{indent}IF ({DescribeInputInline(0)}) IS TRUE:\n" +
                $"{indent}  ➜ Return 'X' (marks checkbox)\n" +
                $"{indent}OTHERWISE:\n" +
                $"{indent}  ➜ Return '' (leaves checkbox blank)",

            "XXIF" =>
                $"{indent}IF ({DescribeInputInline(0)}) IS TRUE:\n" +
                $"{indent}  ➜ Return 'XX' (marks checkbox with double-X)\n" +
                $"{indent}OTHERWISE:\n" +
                $"{indent}  ➜ Return '' (leaves checkbox blank)",

            "BLANK" =>
                $"{indent}CHECK IF ({DescribeInputInline(0)}) IS BLANK:\n" +
                $"{indent}  • IF empty/blank ➜ Return TRUE\n" +
                $"{indent}  • OTHERWISE ➜ Return FALSE",

            "NOTBLANK" =>
                $"{indent}CHECK IF ({DescribeInputInline(0)}) IS NOT BLANK:\n" +
                $"{indent}  • IF populated (contains text/value) ➜ Return TRUE\n" +
                $"{indent}  • OTHERWISE (if empty) ➜ Return FALSE",

            "NOT" =>
                $"{indent}INVERT CONDITION ({DescribeInputInline(0)}):\n" +
                $"{indent}  • IF ({DescribeInputInline(0)}) is TRUE ➜ Return FALSE\n" +
                $"{indent}  • IF ({DescribeInputInline(0)}) is FALSE ➜ Return TRUE",

            "STRIP" =>
                $"{indent}REMOVE TEXT FROM ({DescribeInputInline(0)}):\n" +
                $"{indent}  ➜ Strip every occurrence of {DescribeInputInline(1)} from {DescribeInputInline(0)}",

            "REPLACE" =>
                $"{indent}REPLACE TEXT IN ({DescribeInputInline(0)}):\n" +
                $"{indent}  ➜ Find every occurrence of {DescribeInputInline(1)} and replace it with {DescribeInputInline(2)}",

            "LEN" =>
                $"{indent}COUNT CHARACTERS IN ({DescribeInputInline(0)}):\n" +
                $"{indent}  ➜ Returns the number of characters in the text",

            "TRIM" or "LTRIM" or "RTRIM" =>
                $"{indent}TRIM SPACES FROM ({DescribeInputInline(0)}) ({Prefix}):\n" +
                $"{indent}  ➜ Removes whitespace (Note: FormGen automatically trims fields by default)",

            "TODAY" =>
                $"{indent}GET CURRENT DATE (TODAY()):\n" +
                $"{indent}  ➜ Returns today's system date as MM/DD/YYYY",

            "DATE" =>
                $"{indent}CONVERT TEXT TO DATE ({DescribeInputInline(0)}):\n" +
                $"{indent}  ➜ Converts text into a FormGen Date value for use inside DAY(), MONTH(), or YEAR()",

            "ADDDAYS" or "ADDWEEKS" or "ADDMONTHS" or "ADDYEARS" =>
                $"{indent}SHIFT DATE ({DescribeInputInline(0)}):\n" +
                $"{indent}  ➜ {DescribeInlineSummary()}",

            "NUM" =>
                $"{indent}CONVERT TO NUMBER ({DescribeInputInline(0)}):\n" +
                $"{indent}  ➜ Parses digits from text or date into a numeric value for math",

            _ =>
                $"{indent}EVALUATE {Name ?? Prefix} ({GetCode()}):\n" +
                $"{indent}  ➜ {DescribeInlineSummary()}"
        };
    }

    public override CodeBase Clone()
    {
        var clone = new FormgenFunctionCode(
            Prefix ?? "FUNC",
            Name ?? "Function",
            Inputs.Select(i => i.Description).ToList(),
            UsesParentheses)
        {
            Description = Description,
            WasDeconstructed = WasDeconstructed,
            ReconstructName = ReconstructName,
            ReconstructDescription = ReconstructDescription
        };

        for (int i = 0; i < Inputs.Count && i < clone.Inputs.Count; i++)
        {
            if (Inputs[i].Value is CodeBase cb)
                clone.SetInputValue(i, cb.Clone());
            else
                clone.SetInputValue(i, Inputs[i].Value?.ToString() ?? string.Empty);
            clone.Inputs[i].IsExposedInput = Inputs[i].IsExposedInput;
            clone.Inputs[i].ExposedGroupKey = Inputs[i].ExposedGroupKey;
        }
        return clone;
    }
}

public class ExtendableFormgenFunctionCode : FormgenFunctionCode, IExtendableCode
{
    public int DefaultArgCount { get; private set; }
    public int ArgIncrement { get; private set; }
    private readonly IReadOnlyList<string> _repeatingLabels;
    private readonly bool _keepLastAsDefault;

    public ExtendableFormgenFunctionCode()
        : this("MAX", "Maximum (MAX)", ["Number", "Number"], ["Number"], keepLastAsDefault: false)
    {
    }

    public ExtendableFormgenFunctionCode(
        string prefix,
        string name,
        IReadOnlyList<string> initialLabels,
        IReadOnlyList<string> repeatingLabels,
        bool keepLastAsDefault = false)
        : base(prefix, name, initialLabels)
    {
        DefaultArgCount = initialLabels.Count;
        ArgIncrement = repeatingLabels.Count;
        _repeatingLabels = repeatingLabels;
        _keepLastAsDefault = keepLastAsDefault;
    }

    public override bool IsMultiLinePseudoCode => true;

    public override string GetPseudoCode(int indentLevel = 0)
    {
        string indent = new(' ', indentLevel * 2);
        var sb = new System.Text.StringBuilder();

        switch (Prefix)
        {
            case "CASELTE":
            {
                var cmp = DescribeInputInline(0);
                sb.AppendLine($"{indent}CHECK NUMERIC THRESHOLDS FOR ({cmp}) IN ORDER (<=):");
                int branch = 1;
                for (int i = 1; i < Inputs.Count - 1; i += 2)
                {
                    sb.AppendLine($"{indent}  • Branch {branch} — WHEN ({cmp}) <= {DescribeInputInline(i)}:");
                    sb.AppendLine(FormatInputBranch(i + 1, indentLevel + 2));
                    branch++;
                }
                if (Inputs.Count >= 2)
                {
                    sb.AppendLine($"{indent}  • DEFAULT — OTHERWISE (when {cmp} is greater than all thresholds above):");
                    sb.Append(FormatInputBranch(Inputs.Count - 1, indentLevel + 2));
                }
                return sb.ToString().TrimEnd();
            }

            case "MATCHRANGE":
            {
                var cmp = DescribeInputInline(0);
                sb.AppendLine($"{indent}CHECK WHICH NUMERIC RANGE CONTAINS ({cmp}):");
                int branch = 1;
                for (int i = 1; i <= Inputs.Count - 4; i += 3)
                {
                    sb.AppendLine($"{indent}  • Range {branch} — WHEN ({cmp}) is between {DescribeInputInline(i)} and {DescribeInputInline(i + 1)} (inclusive):");
                    sb.AppendLine(FormatInputBranch(i + 2, indentLevel + 2));
                    branch++;
                }
                if (Inputs.Count >= 2)
                {
                    sb.AppendLine($"{indent}  • DEFAULT — OTHERWISE (when {cmp} falls outside all ranges above):");
                    sb.Append(FormatInputBranch(Inputs.Count - 1, indentLevel + 2));
                }
                return sb.ToString().TrimEnd();
            }

            case "AND":
            {
                sb.AppendLine($"{indent}CHECK THAT ALL {Inputs.Count} CONDITIONS ARE TRUE (AND):");
                for (int i = 0; i < Inputs.Count; i++)
                {
                    sb.AppendLine($"{indent}  • Condition {i + 1}: {DescribeInputInline(i)}");
                }
                sb.Append($"{indent}  ➜ Return TRUE only if every condition above is TRUE; otherwise FALSE");
                return sb.ToString();
            }

            case "OR":
            {
                sb.AppendLine($"{indent}CHECK IF ANY OF THE {Inputs.Count} CONDITIONS IS TRUE (OR):");
                for (int i = 0; i < Inputs.Count; i++)
                {
                    sb.AppendLine($"{indent}  • Condition {i + 1}: {DescribeInputInline(i)}");
                }
                sb.Append($"{indent}  ➜ Return TRUE if at least one condition above is TRUE; otherwise FALSE");
                return sb.ToString();
            }

            case "MAX":
            case "MIN":
            {
                var items = Enumerable.Range(0, Inputs.Count).Select(DescribeInputInline);
                var kind = Prefix == "MAX" ? "LARGEST (MAXIMUM)" : "SMALLEST (MINIMUM)";
                return $"{indent}FIND {kind} NUMBER AMONG [{string.Join(", ", items)}]:\n" +
                       $"{indent}  ➜ Compares all values and returns the {(Prefix == "MAX" ? "highest" : "lowest")} number";
            }
        }

        return base.GetPseudoCode(indentLevel);
    }

    public CodeBase AddExtraInputs(int count)
    {
        for (int c = 0; c < count; c++)
        {
            foreach (var label in _repeatingLabels)
            {
                int insertIdx = _keepLastAsDefault && Inputs.Count > 0 ? Inputs.Count - 1 : Inputs.Count;
                AddInput(insertIdx, label);
            }
        }
        return this;
    }

    public CodeBase RemoveExtraInputs(int count)
    {
        for (int c = 0; c < count; c++)
        {
            if (Inputs.Count - ArgIncrement < DefaultArgCount) break;
            for (int r = 0; r < ArgIncrement; r++)
            {
                int removeIdx = _keepLastAsDefault && Inputs.Count > 1 ? Inputs.Count - 2 : Inputs.Count - 1;
                RemoveInput(removeIdx);
            }
        }
        return this;
    }

    public override CodeBase Clone()
    {
        var clone = new ExtendableFormgenFunctionCode(
            Prefix ?? "FUNC",
            Name ?? "Function",
            Inputs.Select(i => i.Description).ToList(),
            _repeatingLabels,
            _keepLastAsDefault)
        {
            DefaultArgCount = DefaultArgCount,
            ArgIncrement = ArgIncrement,
            Description = Description,
            WasDeconstructed = WasDeconstructed,
            ReconstructName = ReconstructName,
            ReconstructDescription = ReconstructDescription
        };

        for (int i = 0; i < Inputs.Count && i < clone.Inputs.Count; i++)
        {
            if (Inputs[i].Value is CodeBase cb)
                clone.SetInputValue(i, cb.Clone());
            else
                clone.SetInputValue(i, Inputs[i].Value?.ToString() ?? string.Empty);
            clone.Inputs[i].IsExposedInput = Inputs[i].IsExposedInput;
            clone.Inputs[i].ExposedGroupKey = Inputs[i].ExposedGroupKey;
        }
        return clone;
    }
}
