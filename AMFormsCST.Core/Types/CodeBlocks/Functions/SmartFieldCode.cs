using AMFormsCST.Core.Utils;

namespace AMFormsCST.Core.Types.CodeBlocks.Functions;

/// <summary>
/// Represents a FormGen Smart Field, F-Field token, or named variable identifier
/// (e.g. CashPrice, TradeCreditN, ACC01_COMPNAME, F72).
/// When evaluated or dragged onto the canvas, outputs the bare variable name without parentheses.
/// </summary>
public class SmartFieldCode : CodeBase
{
    public bool IsPredefined { get; set; }
    public string Category { get; set; } = "Smart Field";

    public SmartFieldCode() : this("CashPrice", isPredefined: true, "Financial", "Total vehicle / deal cash selling price")
    {
    }

    public SmartFieldCode(string fieldName)
        : this(
            fieldName,
            isPredefined: FormgenReferenceCatalog.IsPredefinedSmartField(fieldName),
            category: FormgenReferenceCatalog.TryGetSmartField(fieldName, out var sf) ? sf!.Category : "User Variable",
            description: FormgenReferenceCatalog.TryGetSmartField(fieldName, out sf) ? sf!.Description : null)
    {
    }

    public SmartFieldCode(string fieldName, bool isPredefined, string category = "Smart Field", string? description = null)
    {
        IsPredefined = isPredefined;
        Category = category;
        Name = isPredefined ? $"Smart Field: {fieldName}" : $"Variable: {fieldName}";
        Prefix = "SF";
        Description = description ?? (isPredefined
            ? $"Auto/Mate FormGen Predefined Smart Field '{fieldName}' ({category}) — reacts dynamically to prompts and deal data at print time."
            : $"Auto/Mate FormGen variable identifier '{fieldName}'.");
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
        var field = DescribeInlineSummary();
        if (IsPredefined)
        {
            return $"{indent}SMART FIELD ({field}):\n" +
                   $"{indent}  ➜ Retrieves FormGen predefined dynamic value '{field}' ({Category})";
        }

        return $"{indent}VARIABLE / FIELD ({field}):\n" +
               $"{indent}  ➜ Retrieves the runtime value of variable '{field}'";
    }

    public override CodeBase Clone()
    {
        var clone = new SmartFieldCode(FieldName, IsPredefined, Category, Description);
        clone.Name = Name;
        clone.Prefix = Prefix;
        return clone;
    }
}

