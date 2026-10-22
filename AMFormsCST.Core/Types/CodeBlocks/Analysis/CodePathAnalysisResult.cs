using System.Collections.ObjectModel;

namespace AMFormsCST.Core.Types.CodeBlocks.Analysis;

/// <summary>
/// Root container returned by CodePathAnalyzer containing the hierarchical tree and all outcome summaries.
/// </summary>
public class CodePathAnalysisResult
{
    public CodePathTreeNode? RootNode { get; set; }
    public ObservableCollection<CodePathOutcomeSummary> Outcomes { get; set; } = [];

    public int TotalOutcomesCount => Outcomes.Count;
    public int TotalForksCount { get; set; }
    public bool HasForks => TotalForksCount > 0;
    public CodePathOutcomeSummary? ActiveOutcome => Outcomes.FirstOrDefault(o => o.IsActive);
}

