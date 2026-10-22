using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AMFormsCST.Core.Types.CodeBlocks.Analysis;

/// <summary>
/// Represents a node in the Code Path tree — either a decision fork with variable conditions or a leaf outcome.
/// </summary>
public class CodePathTreeNode : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User-friendly title for the node, e.g. "IF (F72 = 'N') is TRUE" or "Outcome: 'NEW'".
    /// </summary>
    public string NodeTitle { get; set; } = string.Empty;

    /// <summary>
    /// Category/Kind: "Root", "Fork", or "Outcome".
    /// </summary>
    public string NodeKind { get; set; } = "Fork";

    /// <summary>
    /// Badge label for the branch: "TRUE", "FALSE", "CASE 'N'", "DEFAULT", "OUTCOME", "LINEAR".
    /// </summary>
    public string BranchLabel { get; set; } = string.Empty;

    /// <summary>
    /// Raw condition expression evaluated at this fork, e.g. "F72 = 'N'".
    /// </summary>
    public string ConditionExpression { get; set; } = string.Empty;

    /// <summary>
    /// Detailed description or explanation of the fork logic.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The generated code produced at this node (if an outcome).
    /// </summary>
    public string OutcomeCode { get; set; } = string.Empty;

    /// <summary>
    /// The evaluated value with the current test variables.
    /// </summary>
    public string EvaluatedValue { get; set; } = string.Empty;

    /// <summary>
    /// True if this node represents a final return outcome of the formula.
    /// </summary>
    public bool IsOutcome { get; set; }

    /// <summary>
    /// True if the current test variable values follow this branch.
    /// </summary>
    public bool IsActiveInCurrentTest { get; set; }

    /// <summary>
    /// List of variables evaluated at this specific fork, their requirements, and their current test values.
    /// </summary>
    public ObservableCollection<CodePathVariableInfo> VariablesAtFork { get; set; } = [];

    /// <summary>
    /// Nested child branches or leaf outcomes.
    /// </summary>
    public ObservableCollection<CodePathTreeNode> Children { get; set; } = [];

    /// <summary>
    /// Controls expansion state in UI tree views. Only the taken path is expanded by default.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChanged();
            }
        }
    }

    public bool HasVariables => VariablesAtFork.Count > 0;
    public bool HasChildren => Children.Count > 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

