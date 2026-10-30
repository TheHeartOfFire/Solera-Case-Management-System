using AMFormsCST.Core.Utils;

namespace AMFormsCST.Core.Types.CodeBlocks.Functions;

/// <summary>
/// Represents a FormGen Smart Field, F-Field token, or named variable identifier
/// (e.g. CashPrice, TradeCreditN, ACC01_COMPNAME, F72).
/// When evaluated or dragged onto the canvas, outputs the bare variable name without parentheses.
/// </summary>
public class SmartFieldCode : CodeBase
{
    public SmartFieldCode() : this("CashPrice")
    {
    }

    public SmartFieldCode(string fieldName)
    {
        Name = "Smart Field / Variable";
        Prefix = "SF";
        Description = "Auto/Mate FormGen Smart Field, F-Field, or named calculation variable (e.g. CashPrice, F72, ACC01_COMPNAME).";
        AddInput(fieldName, "Field Name");
    }

    public string FieldName
    {
        get => Inputs.Count > 0 ? Inputs[0].Value?.ToString() ?? string.Empty : string.Empty;
        set
        {
            if (Inputs.Count > 0)
                Inputs[0].SetValue(value);
            else
                AddInput(value, "Field Name");
        }
    }

    public override string GetCode()
    {
        if (HasNoInputs()) return string.Empty;
        var val = Inputs[0].Value?.ToString();
        return string.IsNullOrWhiteSpace(val) ? Inputs[0].Description : val.Trim();
    }

    public override string DescribeInlineSummary() =>
        Inputs.Count > 0 && !string.IsNullOrWhiteSpace(Inputs[0].Value?.ToString())
            ? Inputs[0].Value!.ToString()!.Trim()
            : "<Field Name>";

    public override string GetPseudoCode(int indentLevel = 0)
    {
        string indent = new(' ', indentLevel * 2);
        return $"{indent}VARIABLE / FIELD ({DescribeInlineSummary()}):\n" +
               $"{indent}  ➜ Retrieves the runtime value of {DescribeInlineSummary()}";
    }

    public override CodeBase Clone()
    {
        var clone = new SmartFieldCode(FieldName);
        clone.Name = Name;
        clone.Description = Description;
        clone.Prefix = Prefix;
        return clone;
    }
}

