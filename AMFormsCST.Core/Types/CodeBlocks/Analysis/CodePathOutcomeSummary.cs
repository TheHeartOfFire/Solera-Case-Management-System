namespace AMFormsCST.Core.Types.CodeBlocks.Analysis;

/// <summary>
/// Summarizes a complete end-to-end execution path from root to a specific formula outcome.
/// </summary>
public class CodePathOutcomeSummary
{
    public int PathNumber { get; set; } = 1;

    /// <summary>
    /// The return expression or code produced by this outcome.
    /// </summary>
    public string OutcomeCode { get; set; } = string.Empty;

    /// <summary>
    /// The evaluated result string with current test variables.
    /// </summary>
    public string EvaluatedValue { get; set; } = string.Empty;

    /// <summary>
    /// True if the current test variables satisfy all forks leading to this outcome.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Sequential breadcrumbs of decisions taken along this path, e.g. ["IF (F72 = 'N') = TRUE"].
    /// </summary>
    public List<string> PathConditions { get; set; } = [];

    /// <summary>
    /// Variable conditions and values required at each fork along this path.
    /// </summary>
    public List<CodePathVariableInfo> Variables { get; set; } = [];

    /// <summary>
    /// Pre-calculated sample variable values that satisfy this exact path (for 1-click application into the tester).
    /// </summary>
    public Dictionary<string, string> SuggestedVariableValues { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Formatted summary string of the path.
    /// </summary>
    public string PathSummaryText => PathConditions.Count > 0
        ? string.Join(" ➔ ", PathConditions) + " ➜ " + OutcomeCode
        : OutcomeCode;
}

