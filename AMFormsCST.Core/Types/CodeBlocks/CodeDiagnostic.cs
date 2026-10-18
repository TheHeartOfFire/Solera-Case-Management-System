namespace AMFormsCST.Core.Types.CodeBlocks;

public enum CodeDiagnosticSeverity
{
    Info,
    Warning,
    Error
}

public record CodeDiagnostic(
    CodeDiagnosticSeverity Severity,
    string Message,
    int? InputIndex = null);

public record CodeEvaluationResult(
    bool IsSuccess,
    string Value,
    string Explanation,
    IReadOnlyList<CodeDiagnostic> Diagnostics);

