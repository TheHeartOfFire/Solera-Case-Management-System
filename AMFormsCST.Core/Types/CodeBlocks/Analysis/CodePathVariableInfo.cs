namespace AMFormsCST.Core.Types.CodeBlocks.Analysis;

/// <summary>
/// Captures variable details, constraints, and live evaluation values at a specific fork in a formula.
/// </summary>
public class CodePathVariableInfo
{
    public string Name { get; set; } = string.Empty;
    public string RequiredCondition { get; set; } = string.Empty;
    public string? CurrentTestValue { get; set; }
    public bool? IsSatisfied { get; set; }
    public string Hint { get; set; } = string.Empty;

    public string FormattedDisplay
    {
        get
        {
            if (CurrentTestValue != null)
            {
                var status = IsSatisfied == true ? "✓" : (IsSatisfied == false ? "✕" : "•");
                return $"{status} {Name}: \"{CurrentTestValue}\" (requires {RequiredCondition})";
            }
            return $"{Name} (requires {RequiredCondition})";
        }
    }
}

