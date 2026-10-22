using AMFormsCST.Core.Types.CodeBlocks.Formulae;
using AMFormsCST.Core.Types.CodeBlocks.Functions;
using AMFormsCST.Core.Utils;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AMFormsCST.Core.Types.CodeBlocks.Analysis;

public static class CodePathAnalyzer
{
    private static readonly Regex OperatorPattern = new(
        @"^([A-Za-z_][A-Za-z0-9_]*)\s*(=|!=|<=|>=|<|>)\s*(.+)$",
        RegexOptions.Compiled);

    private static readonly Regex FunctionConditionPattern = new(
        @"^(BLANK|NOTBLANK|CONTAINS)\s*\((.+)\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static CodePathAnalysisResult Analyze(CodeBase? root, IReadOnlyDictionary<string, string>? testValues = null)
    {
        if (root == null)
        {
            return new CodePathAnalysisResult();
        }

        var values = testValues != null
            ? new Dictionary<string, string>(testValues, StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var result = new CodePathAnalysisResult();

        // 1. Unwrap root structure if available
        var effectiveRoot = root.GetStructure() ?? root;

        var rootNode = new CodePathTreeNode
        {
            NodeTitle = !string.IsNullOrWhiteSpace(root.Name) ? root.Name : (root.Prefix ?? "Formula Root"),
            NodeKind = "Root",
            BranchLabel = "ROOT",
            OutcomeCode = root.GetCode(),
            EvaluatedValue = EvaluateBlock(root, values),
            IsActiveInCurrentTest = true,
            Description = root.Description ?? string.Empty
        };

        var outcomeList = new List<CodePathOutcomeSummary>();
        int totalForks = 0;

        // 2. Check if the block has conditional branches
        if (IsBranchingBlock(effectiveRoot))
        {
            TraverseBranchingBlock(
                effectiveRoot,
                rootNode,
                outcomeList,
                accumulatedConditions: [],
                accumulatedVariables: [],
                suggestedValues: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                isParentActive: true,
                values,
                ref totalForks);
        }
        else
        {
            // Check if any child input contains a branching block (e.g. SEPLIST(', ', IF(...), ...))
            var branchingChildIndex = FindBranchingChildInputIndex(effectiveRoot);
            if (branchingChildIndex >= 0)
            {
                TraverseOuterBlockWithBranchingChild(
                    root,
                    effectiveRoot,
                    branchingChildIndex,
                    rootNode,
                    outcomeList,
                    values,
                    ref totalForks);
            }
            else
            {
                // Linear expression with no conditional forks
                var allVars = root.GetVariables();
                var varInfos = new List<CodePathVariableInfo>();
                foreach (var v in allVars)
                {
                    values.TryGetValue(v, out var currentVal);
                    var field = FormgenReferenceCatalog.TryGetField(v);
                    varInfos.Add(new CodePathVariableInfo
                    {
                        Name = v,
                        RequiredCondition = "Used in formula",
                        CurrentTestValue = currentVal,
                        IsSatisfied = true,
                        Hint = field?.Description ?? "Input variable"
                    });
                }

                var linearOutcome = new CodePathOutcomeSummary
                {
                    PathNumber = 1,
                    OutcomeCode = root.GetCode(),
                    EvaluatedValue = rootNode.EvaluatedValue,
                    IsActive = true,
                    PathConditions = ["Deterministic / Linear Path (No conditional forks)"],
                    Variables = varInfos,
                    SuggestedVariableValues = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase)
                };

                outcomeList.Add(linearOutcome);

                var leafNode = new CodePathTreeNode
                {
                    NodeTitle = $"Result: {linearOutcome.OutcomeCode}",
                    NodeKind = "Outcome",
                    BranchLabel = "OUTCOME",
                    OutcomeCode = linearOutcome.OutcomeCode,
                    EvaluatedValue = linearOutcome.EvaluatedValue,
                    IsOutcome = true,
                    IsActiveInCurrentTest = true,
                    VariablesAtFork = new ObservableCollection<CodePathVariableInfo>(varInfos)
                };

                rootNode.Children.Add(leafNode);
            }
        }

        // 3. Number outcomes
        for (int i = 0; i < outcomeList.Count; i++)
        {
            outcomeList[i].PathNumber = i + 1;
        }

        // 4. Collapse passed/un-taken branches so that only the taken path is expanded
        EnsureActivePathExpanded(rootNode);

        result.RootNode = rootNode;
        result.Outcomes = new ObservableCollection<CodePathOutcomeSummary>(outcomeList);
        result.TotalForksCount = totalForks;

        return result;
    }

    private static void EnsureActivePathExpanded(CodePathTreeNode node)
    {
        bool hasActiveChild = false;
        foreach (var child in node.Children)
        {
            EnsureActivePathExpanded(child);
            if (child.IsActiveInCurrentTest || child.IsExpanded)
            {
                hasActiveChild = true;
            }
        }

        // Expand root and nodes along the active/taken path; collapse passed/inactive branches
        if (node.NodeKind == "Root" || node.IsActiveInCurrentTest || hasActiveChild)
        {
            node.IsExpanded = true;
        }
        else
        {
            node.IsExpanded = false;
        }
    }

    private static bool IsBranchingBlock(CodeBase block)
    {
        var unwrapped = block.GetStructure() ?? block;
        var prefix = unwrapped.Prefix?.ToUpperInvariant();
        return prefix is "IF" or "CASE" or "CASELTE" or "MATCHRANGE" or "XIF" or "XXIF";
    }

    private static int FindBranchingChildInputIndex(CodeBase block)
    {
        for (int i = 0; i < block.Inputs.Count; i++)
        {
            if (block.Inputs[i].Value is CodeBase child && IsBranchingBlock(child))
            {
                return i;
            }
        }
        return -1;
    }

    private static void TraverseBranchingBlock(
        CodeBase block,
        CodePathTreeNode parentNode,
        List<CodePathOutcomeSummary> outcomes,
        List<string> accumulatedConditions,
        List<CodePathVariableInfo> accumulatedVariables,
        Dictionary<string, string> suggestedValues,
        bool isParentActive,
        IReadOnlyDictionary<string, string> testValues,
        ref int totalForks)
    {
        var unwrapped = block.GetStructure() ?? block;
        var prefix = unwrapped.Prefix?.ToUpperInvariant();

        switch (prefix)
        {
            case "IF":
                HandleIfBlock(unwrapped, parentNode, outcomes, accumulatedConditions, accumulatedVariables, suggestedValues, isParentActive, testValues, ref totalForks);
                break;

            case "CASE":
                HandleCaseBlock(unwrapped, parentNode, outcomes, accumulatedConditions, accumulatedVariables, suggestedValues, isParentActive, testValues, ref totalForks);
                break;

            case "CASELTE":
                HandleCaseLteBlock(unwrapped, parentNode, outcomes, accumulatedConditions, accumulatedVariables, suggestedValues, isParentActive, testValues, ref totalForks);
                break;

            case "MATCHRANGE":
                HandleMatchRangeBlock(unwrapped, parentNode, outcomes, accumulatedConditions, accumulatedVariables, suggestedValues, isParentActive, testValues, ref totalForks);
                break;

            case "XIF":
            case "XXIF":
                HandleXifBlock(unwrapped, parentNode, outcomes, accumulatedConditions, accumulatedVariables, suggestedValues, isParentActive, testValues, ref totalForks);
                break;

            default:
                // Fallback for leaf block or unhandled branching block
                AddLeafOutcome(unwrapped, parentNode, outcomes, accumulatedConditions, accumulatedVariables, suggestedValues, isParentActive, testValues);
                break;
        }
    }

    private static void HandleIfBlock(
        CodeBase ifBlock,
        CodePathTreeNode parentNode,
        List<CodePathOutcomeSummary> outcomes,
        List<string> accumulatedConditions,
        List<CodePathVariableInfo> accumulatedVariables,
        Dictionary<string, string> suggestedValues,
        bool isParentActive,
        IReadOnlyDictionary<string, string> testValues,
        ref int totalForks)
    {
        totalForks++;

        var condInput = ifBlock.Inputs.Count > 0 ? ifBlock.Inputs[0] : null;
        var trueInput = ifBlock.Inputs.Count > 1 ? ifBlock.Inputs[1] : null;
        var falseInput = ifBlock.Inputs.Count > 2 ? ifBlock.Inputs[2] : null;

        var condExpr = condInput != null ? GetInputCode(condInput) : "Condition";
        bool? conditionEvaluated = EvaluateCondition(condInput, testValues);

        bool isTrueActive = isParentActive && conditionEvaluated == true;
        bool isFalseActive = isParentActive && conditionEvaluated == false;

        // Parse variable constraints for TRUE and FALSE branches
        var (trueVars, falseVars, trueSuggestions, falseSuggestions) = AnalyzeConditionConstraints(condInput, testValues);

        // 1. TRUE Branch
        var trueNode = new CodePathTreeNode
        {
            NodeTitle = $"Branch: IF ({condExpr}) is TRUE",
            NodeKind = "Fork",
            BranchLabel = "TRUE",
            ConditionExpression = condExpr,
            Description = $"When condition ({condExpr}) evaluates to TRUE",
            IsActiveInCurrentTest = isTrueActive,
            VariablesAtFork = new ObservableCollection<CodePathVariableInfo>(trueVars)
        };
        parentNode.Children.Add(trueNode);

        var trueCondList = new List<string>(accumulatedConditions) { $"{condExpr} = TRUE" };
        var trueVarList = new List<CodePathVariableInfo>(accumulatedVariables);
        trueVarList.AddRange(trueVars);

        var trueMergedSuggestions = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in trueSuggestions) trueMergedSuggestions[kv.Key] = kv.Value;

        if (trueInput != null && trueInput.Value is CodeBase trueChild && IsBranchingBlock(trueChild))
        {
            TraverseBranchingBlock(trueChild, trueNode, outcomes, trueCondList, trueVarList, trueMergedSuggestions, isTrueActive, testValues, ref totalForks);
        }
        else
        {
            AddInputLeafOutcome(trueInput, trueNode, outcomes, trueCondList, trueVarList, trueMergedSuggestions, isTrueActive, testValues);
        }

        // 2. FALSE Branch
        var falseNode = new CodePathTreeNode
        {
            NodeTitle = $"Branch: IF ({condExpr}) is FALSE",
            NodeKind = "Fork",
            BranchLabel = "FALSE",
            ConditionExpression = condExpr,
            Description = $"When condition ({condExpr}) evaluates to FALSE",
            IsActiveInCurrentTest = isFalseActive,
            VariablesAtFork = new ObservableCollection<CodePathVariableInfo>(falseVars)
        };
        parentNode.Children.Add(falseNode);

        var falseCondList = new List<string>(accumulatedConditions) { $"{condExpr} = FALSE" };
        var falseVarList = new List<CodePathVariableInfo>(accumulatedVariables);
        falseVarList.AddRange(falseVars);

        var falseMergedSuggestions = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in falseSuggestions) falseMergedSuggestions[kv.Key] = kv.Value;

        if (falseInput != null && falseInput.Value is CodeBase falseChild && IsBranchingBlock(falseChild))
        {
            TraverseBranchingBlock(falseChild, falseNode, outcomes, falseCondList, falseVarList, falseMergedSuggestions, isFalseActive, testValues, ref totalForks);
        }
        else
        {
            AddInputLeafOutcome(falseInput, falseNode, outcomes, falseCondList, falseVarList, falseMergedSuggestions, isFalseActive, testValues);
        }
    }

    private static void HandleCaseBlock(
        CodeBase caseBlock,
        CodePathTreeNode parentNode,
        List<CodePathOutcomeSummary> outcomes,
        List<string> accumulatedConditions,
        List<CodePathVariableInfo> accumulatedVariables,
        Dictionary<string, string> suggestedValues,
        bool isParentActive,
        IReadOnlyDictionary<string, string> testValues,
        ref int totalForks)
    {
        totalForks++;

        var cmpInput = caseBlock.Inputs.Count > 0 ? caseBlock.Inputs[0] : null;
        var cmpExpr = cmpInput != null ? GetInputCode(cmpInput) : "Comparison";
        var cmpVar = ExtractFirstVariable(cmpInput) ?? cmpExpr;

        testValues.TryGetValue(cmpVar, out var currentCmpVal);

        var evaluatedCmpVal = cmpInput != null ? EvaluateInput(cmpInput, testValues) : currentCmpVal;
        bool isCompound = !string.IsNullOrWhiteSpace(cmpExpr) && !string.Equals(cmpExpr.Trim(), cmpVar.Trim(), StringComparison.OrdinalIgnoreCase);

        var allCaseValues = new List<string>();
        bool anyMatched = false;

        // Process (Case, Result) pairs
        for (int i = 1; i < caseBlock.Inputs.Count - 1; i += 2)
        {
            var caseInput = caseBlock.Inputs[i];
            var resultInput = i + 1 < caseBlock.Inputs.Count ? caseBlock.Inputs[i + 1] : null;

            var caseCode = GetInputCode(caseInput);
            var cleanCaseVal = Unquote(caseCode);
            allCaseValues.Add(cleanCaseVal);

            bool isMatch = false;
            if (evaluatedCmpVal != null)
            {
                isMatch = ValuesMatch(evaluatedCmpVal, cleanCaseVal);
            }

            bool branchActive = isParentActive && !anyMatched && isMatch;
            if (branchActive)
            {
                anyMatched = true;
            }

            var field = FormgenReferenceCatalog.TryGetField(cmpVar);
            var reqCond = isCompound
                ? $"{cmpExpr} == '{cleanCaseVal}'"
                : $"== '{cleanCaseVal}'";

            var displayTestVal = isCompound && !string.IsNullOrWhiteSpace(evaluatedCmpVal) && evaluatedCmpVal != currentCmpVal
                ? $"{currentCmpVal} (evaluated: {evaluatedCmpVal})"
                : currentCmpVal;

            var varInfo = new CodePathVariableInfo
            {
                Name = cmpVar,
                RequiredCondition = reqCond,
                CurrentTestValue = displayTestVal,
                IsSatisfied = evaluatedCmpVal != null ? isMatch : null,
                Hint = field?.Description ?? "Comparison variable"
            };

            var branchConditions = new List<string>(accumulatedConditions) { $"{cmpExpr} == {caseCode}" };
            var branchVars = new List<CodePathVariableInfo>(accumulatedVariables) { varInfo };

            var suggestedVal = SuggestVariableValueForCase(cmpInput, cmpVar, cleanCaseVal, currentCmpVal);

            var branchSuggestions = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase)
            {
                [cmpVar] = suggestedVal
            };

            var caseNode = new CodePathTreeNode
            {
                NodeTitle = $"Branch: CASE '{cleanCaseVal}'",
                NodeKind = "Fork",
                BranchLabel = $"CASE '{cleanCaseVal}'",
                ConditionExpression = $"{cmpExpr} == {caseCode}",
                Description = $"When {cmpExpr} equals '{cleanCaseVal}'",
                IsActiveInCurrentTest = branchActive,
                VariablesAtFork = [varInfo]
            };
            parentNode.Children.Add(caseNode);

            if (resultInput != null && resultInput.Value is CodeBase resChild && IsBranchingBlock(resChild))
            {
                TraverseBranchingBlock(resChild, caseNode, outcomes, branchConditions, branchVars, branchSuggestions, branchActive, testValues, ref totalForks);
            }
            else
            {
                AddInputLeafOutcome(resultInput, caseNode, outcomes, branchConditions, branchVars, branchSuggestions, branchActive, testValues);
            }
        }

        // Process Default branch (last input)
        if (caseBlock.Inputs.Count >= 2)
        {
            var defInput = caseBlock.Inputs[^1];
            bool defActive = isParentActive && !anyMatched;

            var defReq = allCaseValues.Count > 0
                ? $"NOT in ({string.Join(", ", allCaseValues.Select(v => $"'{v}'"))})"
                : "Default";

            var reqCond = isCompound
                ? $"{cmpExpr} {defReq}"
                : defReq;

            var displayTestVal = isCompound && !string.IsNullOrWhiteSpace(evaluatedCmpVal) && evaluatedCmpVal != currentCmpVal
                ? $"{currentCmpVal} (evaluated: {evaluatedCmpVal})"
                : currentCmpVal;

            bool isDefSatisfied = evaluatedCmpVal != null && !allCaseValues.Any(v => ValuesMatch(evaluatedCmpVal, v));

            var field = FormgenReferenceCatalog.TryGetField(cmpVar);
            var defVarInfo = new CodePathVariableInfo
            {
                Name = cmpVar,
                RequiredCondition = reqCond,
                CurrentTestValue = displayTestVal,
                IsSatisfied = evaluatedCmpVal != null ? isDefSatisfied : null,
                Hint = field?.Description ?? "Comparison variable"
            };

            var defConditions = new List<string>(accumulatedConditions) { $"{cmpExpr} matches none of the cases (DEFAULT)" };
            var defVars = new List<CodePathVariableInfo>(accumulatedVariables) { defVarInfo };

            var defSuggestedVal = SuggestDefaultVariableValue(cmpInput, cmpVar, allCaseValues, currentCmpVal);

            var defSuggestions = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase)
            {
                [cmpVar] = defSuggestedVal
            };

            var defNode = new CodePathTreeNode
            {
                NodeTitle = "Branch: DEFAULT (Fallthrough)",
                NodeKind = "Fork",
                BranchLabel = "DEFAULT",
                ConditionExpression = $"{cmpExpr} {defReq}",
                Description = $"When {cmpExpr} does not match any specified cases",
                IsActiveInCurrentTest = defActive,
                VariablesAtFork = [defVarInfo]
            };
            parentNode.Children.Add(defNode);

            if (defInput.Value is CodeBase defChild && IsBranchingBlock(defChild))
            {
                TraverseBranchingBlock(defChild, defNode, outcomes, defConditions, defVars, defSuggestions, defActive, testValues, ref totalForks);
            }
            else
            {
                AddInputLeafOutcome(defInput, defNode, outcomes, defConditions, defVars, defSuggestions, defActive, testValues);
            }
        }
    }

    private static void HandleCaseLteBlock(
        CodeBase block,
        CodePathTreeNode parentNode,
        List<CodePathOutcomeSummary> outcomes,
        List<string> accumulatedConditions,
        List<CodePathVariableInfo> accumulatedVariables,
        Dictionary<string, string> suggestedValues,
        bool isParentActive,
        IReadOnlyDictionary<string, string> testValues,
        ref int totalForks)
    {
        totalForks++;

        var cmpInput = block.Inputs.Count > 0 ? block.Inputs[0] : null;
        var cmpExpr = cmpInput != null ? GetInputCode(cmpInput) : "Comparison";
        var cmpVar = ExtractFirstVariable(cmpInput) ?? cmpExpr;
        testValues.TryGetValue(cmpVar, out var currentCmpVal);

        var evaluatedCmpVal = cmpInput != null ? EvaluateInput(cmpInput, testValues) : currentCmpVal;
        bool isCompound = !string.IsNullOrWhiteSpace(cmpExpr) && !string.Equals(cmpExpr.Trim(), cmpVar.Trim(), StringComparison.OrdinalIgnoreCase);

        bool anyMatched = false;

        for (int i = 1; i < block.Inputs.Count - 1; i += 2)
        {
            var threshInput = block.Inputs[i];
            var resultInput = i + 1 < block.Inputs.Count ? block.Inputs[i + 1] : null;

            var threshCode = GetInputCode(threshInput);
            double.TryParse(Unquote(threshCode), NumberStyles.Any, CultureInfo.InvariantCulture, out double threshNum);

            bool isMatch = false;
            if (evaluatedCmpVal != null && double.TryParse(evaluatedCmpVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double curNum))
            {
                isMatch = curNum <= threshNum;
            }

            bool branchActive = isParentActive && !anyMatched && isMatch;
            if (branchActive) anyMatched = true;

            var reqCond = isCompound
                ? $"{cmpExpr} <= {threshCode}"
                : $"<= {threshCode}";

            var displayTestVal = isCompound && !string.IsNullOrWhiteSpace(evaluatedCmpVal) && evaluatedCmpVal != currentCmpVal
                ? $"{currentCmpVal} (evaluated: {evaluatedCmpVal})"
                : currentCmpVal;

            var varInfo = new CodePathVariableInfo
            {
                Name = cmpVar,
                RequiredCondition = reqCond,
                CurrentTestValue = displayTestVal,
                IsSatisfied = evaluatedCmpVal != null ? isMatch : null,
                Hint = "Numeric comparison"
            };

            var branchConditions = new List<string>(accumulatedConditions) { $"{cmpExpr} <= {threshCode}" };
            var branchVars = new List<CodePathVariableInfo>(accumulatedVariables) { varInfo };
            var branchSuggestions = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase)
            {
                [cmpVar] = threshNum.ToString(CultureInfo.InvariantCulture)
            };

            var node = new CodePathTreeNode
            {
                NodeTitle = $"Branch: <= {threshCode}",
                NodeKind = "Fork",
                BranchLabel = $"<= {threshCode}",
                ConditionExpression = $"{cmpExpr} <= {threshCode}",
                Description = $"When {cmpExpr} is less than or equal to {threshCode}",
                IsActiveInCurrentTest = branchActive,
                VariablesAtFork = [varInfo]
            };
            parentNode.Children.Add(node);

            if (resultInput != null && resultInput.Value is CodeBase resChild && IsBranchingBlock(resChild))
            {
                TraverseBranchingBlock(resChild, node, outcomes, branchConditions, branchVars, branchSuggestions, branchActive, testValues, ref totalForks);
            }
            else
            {
                AddInputLeafOutcome(resultInput, node, outcomes, branchConditions, branchVars, branchSuggestions, branchActive, testValues);
            }
        }

        // Default
        if (block.Inputs.Count >= 2)
        {
            var defInput = block.Inputs[^1];
            bool defActive = isParentActive && !anyMatched;

            var reqCond = isCompound
                ? $"{cmpExpr} > all thresholds"
                : "> all thresholds";

            var displayTestVal = isCompound && !string.IsNullOrWhiteSpace(evaluatedCmpVal) && evaluatedCmpVal != currentCmpVal
                ? $"{currentCmpVal} (evaluated: {evaluatedCmpVal})"
                : currentCmpVal;

            var defVarInfo = new CodePathVariableInfo
            {
                Name = cmpVar,
                RequiredCondition = reqCond,
                CurrentTestValue = displayTestVal,
                IsSatisfied = evaluatedCmpVal != null ? !anyMatched : null,
                Hint = "Numeric comparison (exceeded thresholds)"
            };

            var defConditions = new List<string>(accumulatedConditions) { $"{cmpExpr} > all thresholds (DEFAULT)" };
            var defVars = new List<CodePathVariableInfo>(accumulatedVariables) { defVarInfo };
            var defSuggestions = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase)
            {
                [cmpVar] = "99999"
            };

            var defNode = new CodePathTreeNode
            {
                NodeTitle = "Branch: DEFAULT (Exceeded thresholds)",
                NodeKind = "Fork",
                BranchLabel = "DEFAULT",
                ConditionExpression = $"{cmpExpr} > all thresholds",
                Description = $"When {cmpExpr} exceeds all thresholds",
                IsActiveInCurrentTest = defActive,
                VariablesAtFork = [defVarInfo]
            };
            parentNode.Children.Add(defNode);

            if (defInput.Value is CodeBase defChild && IsBranchingBlock(defChild))
            {
                TraverseBranchingBlock(defChild, defNode, outcomes, defConditions, defVars, defSuggestions, defActive, testValues, ref totalForks);
            }
            else
            {
                AddInputLeafOutcome(defInput, defNode, outcomes, defConditions, defVars, defSuggestions, defActive, testValues);
            }
        }
    }

    private static void HandleMatchRangeBlock(
        CodeBase block,
        CodePathTreeNode parentNode,
        List<CodePathOutcomeSummary> outcomes,
        List<string> accumulatedConditions,
        List<CodePathVariableInfo> accumulatedVariables,
        Dictionary<string, string> suggestedValues,
        bool isParentActive,
        IReadOnlyDictionary<string, string> testValues,
        ref int totalForks)
    {
        totalForks++;

        var cmpInput = block.Inputs.Count > 0 ? block.Inputs[0] : null;
        var cmpExpr = cmpInput != null ? GetInputCode(cmpInput) : "Comparison";
        var cmpVar = ExtractFirstVariable(cmpInput) ?? cmpExpr;
        testValues.TryGetValue(cmpVar, out var currentCmpVal);

        var evaluatedCmpVal = cmpInput != null ? EvaluateInput(cmpInput, testValues) : currentCmpVal;
        bool isCompound = !string.IsNullOrWhiteSpace(cmpExpr) && !string.Equals(cmpExpr.Trim(), cmpVar.Trim(), StringComparison.OrdinalIgnoreCase);

        bool anyMatched = false;

        for (int i = 1; i < block.Inputs.Count - 1; i += 3)
        {
            var lowInput = block.Inputs[i];
            var highInput = i + 1 < block.Inputs.Count ? block.Inputs[i + 1] : null;
            var resultInput = i + 2 < block.Inputs.Count ? block.Inputs[i + 2] : null;

            var lowCode = GetInputCode(lowInput);
            var highCode = highInput != null ? GetInputCode(highInput) : lowCode;

            double.TryParse(Unquote(lowCode), NumberStyles.Any, CultureInfo.InvariantCulture, out double lowNum);
            double.TryParse(Unquote(highCode), NumberStyles.Any, CultureInfo.InvariantCulture, out double highNum);

            bool isMatch = false;
            if (evaluatedCmpVal != null && double.TryParse(evaluatedCmpVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double curNum))
            {
                isMatch = curNum >= lowNum && curNum <= highNum;
            }

            bool branchActive = isParentActive && !anyMatched && isMatch;
            if (branchActive) anyMatched = true;

            var reqCond = isCompound
                ? $"{lowCode} <= {cmpExpr} <= {highCode}"
                : $"{lowCode} .. {highCode}";

            var displayTestVal = isCompound && !string.IsNullOrWhiteSpace(evaluatedCmpVal) && evaluatedCmpVal != currentCmpVal
                ? $"{currentCmpVal} (evaluated: {evaluatedCmpVal})"
                : currentCmpVal;

            var varInfo = new CodePathVariableInfo
            {
                Name = cmpVar,
                RequiredCondition = reqCond,
                CurrentTestValue = displayTestVal,
                IsSatisfied = evaluatedCmpVal != null ? isMatch : null,
                Hint = "Range check"
            };

            var branchConditions = new List<string>(accumulatedConditions) { $"{lowCode} <= {cmpExpr} <= {highCode}" };
            var branchVars = new List<CodePathVariableInfo>(accumulatedVariables) { varInfo };
            var branchSuggestions = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase)
            {
                [cmpVar] = lowNum.ToString(CultureInfo.InvariantCulture)
            };

            var node = new CodePathTreeNode
            {
                NodeTitle = $"Branch: In Range [{lowCode} .. {highCode}]",
                NodeKind = "Fork",
                BranchLabel = $"[{lowCode}..{highCode}]",
                ConditionExpression = $"{lowCode} <= {cmpExpr} <= {highCode}",
                Description = $"When {cmpExpr} is between {lowCode} and {highCode}",
                IsActiveInCurrentTest = branchActive,
                VariablesAtFork = [varInfo]
            };
            parentNode.Children.Add(node);

            if (resultInput != null && resultInput.Value is CodeBase resChild && IsBranchingBlock(resChild))
            {
                TraverseBranchingBlock(resChild, node, outcomes, branchConditions, branchVars, branchSuggestions, branchActive, testValues, ref totalForks);
            }
            else
            {
                AddInputLeafOutcome(resultInput, node, outcomes, branchConditions, branchVars, branchSuggestions, branchActive, testValues);
            }
        }

        // Default
        if (block.Inputs.Count >= 2)
        {
            var defInput = block.Inputs[^1];
            bool defActive = isParentActive && !anyMatched;

            var reqCond = isCompound
                ? $"{cmpExpr} outside all ranges"
                : "outside all ranges";

            var displayTestVal = isCompound && !string.IsNullOrWhiteSpace(evaluatedCmpVal) && evaluatedCmpVal != currentCmpVal
                ? $"{currentCmpVal} (evaluated: {evaluatedCmpVal})"
                : currentCmpVal;

            var defVarInfo = new CodePathVariableInfo
            {
                Name = cmpVar,
                RequiredCondition = reqCond,
                CurrentTestValue = displayTestVal,
                IsSatisfied = evaluatedCmpVal != null ? !anyMatched : null,
                Hint = "Range check (no match)"
            };

            var defConditions = new List<string>(accumulatedConditions) { $"{cmpExpr} outside all ranges (DEFAULT)" };
            var defVars = new List<CodePathVariableInfo>(accumulatedVariables) { defVarInfo };
            var defSuggestions = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase)
            {
                [cmpVar] = "-1"
            };

            var defNode = new CodePathTreeNode
            {
                NodeTitle = "Branch: DEFAULT (Outside ranges)",
                NodeKind = "Fork",
                BranchLabel = "DEFAULT",
                ConditionExpression = $"{cmpExpr} outside ranges",
                Description = $"When {cmpExpr} does not fall within any specified range",
                IsActiveInCurrentTest = defActive,
                VariablesAtFork = [defVarInfo]
            };
            parentNode.Children.Add(defNode);

            if (defInput.Value is CodeBase defChild && IsBranchingBlock(defChild))
            {
                TraverseBranchingBlock(defChild, defNode, outcomes, defConditions, defVars, defSuggestions, defActive, testValues, ref totalForks);
            }
            else
            {
                AddInputLeafOutcome(defInput, defNode, outcomes, defConditions, defVars, defSuggestions, defActive, testValues);
            }
        }
    }

    private static void HandleXifBlock(
        CodeBase xifBlock,
        CodePathTreeNode parentNode,
        List<CodePathOutcomeSummary> outcomes,
        List<string> accumulatedConditions,
        List<CodePathVariableInfo> accumulatedVariables,
        Dictionary<string, string> suggestedValues,
        bool isParentActive,
        IReadOnlyDictionary<string, string> testValues,
        ref int totalForks)
    {
        totalForks++;

        var condInput = xifBlock.Inputs.Count > 0 ? xifBlock.Inputs[0] : null;
        var condExpr = condInput != null ? GetInputCode(condInput) : "Condition";
        bool? conditionEvaluated = EvaluateCondition(condInput, testValues);

        bool isTrueActive = isParentActive && conditionEvaluated == true;
        bool isFalseActive = isParentActive && conditionEvaluated == false;

        var (trueVars, falseVars, trueSuggestions, falseSuggestions) = AnalyzeConditionConstraints(condInput, testValues);
        var mark = xifBlock.Prefix?.ToUpperInvariant() == "XXIF" ? "'XX'" : "'X'";

        // 1. TRUE (Marked)
        var trueMergedSuggestions = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in trueSuggestions) trueMergedSuggestions[kv.Key] = kv.Value;

        var trueCondList = new List<string>(accumulatedConditions) { $"{condExpr} = TRUE" };
        var trueVarList = new List<CodePathVariableInfo>(accumulatedVariables);
        trueVarList.AddRange(trueVars);

        var trueSummary = new CodePathOutcomeSummary
        {
            OutcomeCode = mark,
            EvaluatedValue = Unquote(mark),
            IsActive = isTrueActive,
            PathConditions = trueCondList,
            Variables = trueVarList,
            SuggestedVariableValues = trueMergedSuggestions
        };
        outcomes.Add(trueSummary);

        var trueNode = new CodePathTreeNode
        {
            NodeTitle = $"Outcome: {mark} (Checked)",
            NodeKind = "Outcome",
            BranchLabel = "TRUE ➜ MARK",
            ConditionExpression = condExpr,
            Description = $"When {condExpr} is TRUE, outputs {mark}",
            OutcomeCode = mark,
            EvaluatedValue = Unquote(mark),
            IsOutcome = true,
            IsActiveInCurrentTest = isTrueActive,
            VariablesAtFork = new ObservableCollection<CodePathVariableInfo>(trueVars)
        };
        parentNode.Children.Add(trueNode);

        // 2. FALSE (Blank)
        var falseMergedSuggestions = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase);
        foreach (var kv in falseSuggestions) falseMergedSuggestions[kv.Key] = kv.Value;

        var falseCondList = new List<string>(accumulatedConditions) { $"{condExpr} = FALSE" };
        var falseVarList = new List<CodePathVariableInfo>(accumulatedVariables);
        falseVarList.AddRange(falseVars);

        var falseSummary = new CodePathOutcomeSummary
        {
            OutcomeCode = "''",
            EvaluatedValue = string.Empty,
            IsActive = isFalseActive,
            PathConditions = falseCondList,
            Variables = falseVarList,
            SuggestedVariableValues = falseMergedSuggestions
        };
        outcomes.Add(falseSummary);

        var falseNode = new CodePathTreeNode
        {
            NodeTitle = "Outcome: '' (Unchecked / Blank)",
            NodeKind = "Outcome",
            BranchLabel = "FALSE ➜ BLANK",
            ConditionExpression = condExpr,
            Description = $"When {condExpr} is FALSE, outputs blank",
            OutcomeCode = "''",
            EvaluatedValue = string.Empty,
            IsOutcome = true,
            IsActiveInCurrentTest = isFalseActive,
            VariablesAtFork = new ObservableCollection<CodePathVariableInfo>(falseVars)
        };
        parentNode.Children.Add(falseNode);
    }

    private static void TraverseOuterBlockWithBranchingChild(
        CodeBase originalRoot,
        CodeBase effectiveRoot,
        int branchingChildIndex,
        CodePathTreeNode parentNode,
        List<CodePathOutcomeSummary> outcomes,
        IReadOnlyDictionary<string, string> testValues,
        ref int totalForks)
    {
        var childBlock = (CodeBase)effectiveRoot.Inputs[branchingChildIndex].Value;
        var childAnalysis = Analyze(childBlock, testValues);

        totalForks += childAnalysis.TotalForksCount;

        // Recursively update leaf outcome nodes in the child tree to reflect the outer wrapper
        void WrapOutcomeNodes(CodePathTreeNode node)
        {
            if (node.IsOutcome)
            {
                var cloned = effectiveRoot.Clone();
                cloned.SetInputValue(branchingChildIndex, node.OutcomeCode ?? "''");
                node.OutcomeCode = cloned.GetCode();
                node.EvaluatedValue = EvaluateBlock(cloned, testValues);
                node.NodeTitle = $"Outcome: {node.OutcomeCode}";
                node.BranchLabel = node.IsActiveInCurrentTest ? "ACTIVE OUTCOME" : "OUTCOME";
            }
            else
            {
                foreach (var child in node.Children)
                {
                    WrapOutcomeNodes(child);
                }
            }
        }

        if (childAnalysis.RootNode != null)
        {
            foreach (var childNode in childAnalysis.RootNode.Children)
            {
                WrapOutcomeNodes(childNode);
                parentNode.Children.Add(childNode);
            }
        }

        foreach (var childOutcome in childAnalysis.Outcomes)
        {
            // Clone root and substitute child outcome value
            var cloned = effectiveRoot.Clone();
            cloned.SetInputValue(branchingChildIndex, childOutcome.OutcomeCode);

            var overallOutcomeCode = cloned.GetCode();
            var evaluatedVal = EvaluateBlock(cloned, testValues);

            var combinedSuggestions = new Dictionary<string, string>(childOutcome.SuggestedVariableValues, StringComparer.OrdinalIgnoreCase);
            foreach (var v in originalRoot.GetVariables())
            {
                if (!combinedSuggestions.ContainsKey(v) && testValues.TryGetValue(v, out var existingVal))
                {
                    combinedSuggestions[v] = existingVal;
                }
            }

            var summary = new CodePathOutcomeSummary
            {
                OutcomeCode = overallOutcomeCode,
                EvaluatedValue = evaluatedVal,
                IsActive = childOutcome.IsActive,
                PathConditions = new List<string>(childOutcome.PathConditions),
                Variables = new List<CodePathVariableInfo>(childOutcome.Variables),
                SuggestedVariableValues = combinedSuggestions
            };
            outcomes.Add(summary);
        }
    }

    private static void AddInputLeafOutcome(
        CodeInput? input,
        CodePathTreeNode parentNode,
        List<CodePathOutcomeSummary> outcomes,
        List<string> accumulatedConditions,
        List<CodePathVariableInfo> accumulatedVariables,
        Dictionary<string, string> suggestedValues,
        bool isPathActive,
        IReadOnlyDictionary<string, string> testValues)
    {
        var code = input != null ? GetInputCode(input) : "''";
        var evaluated = EvaluateInput(input, testValues);

        var summary = new CodePathOutcomeSummary
        {
            OutcomeCode = code,
            EvaluatedValue = evaluated,
            IsActive = isPathActive,
            PathConditions = new List<string>(accumulatedConditions),
            Variables = new List<CodePathVariableInfo>(accumulatedVariables),
            SuggestedVariableValues = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase)
        };
        outcomes.Add(summary);

        var leafNode = new CodePathTreeNode
        {
            NodeTitle = $"Outcome: {code}",
            NodeKind = "Outcome",
            BranchLabel = isPathActive ? "ACTIVE OUTCOME" : "OUTCOME",
            OutcomeCode = code,
            EvaluatedValue = evaluated,
            IsOutcome = true,
            IsActiveInCurrentTest = isPathActive,
            VariablesAtFork = new ObservableCollection<CodePathVariableInfo>(accumulatedVariables)
        };
        parentNode.Children.Add(leafNode);
    }

    private static void AddLeafOutcome(
        CodeBase block,
        CodePathTreeNode parentNode,
        List<CodePathOutcomeSummary> outcomes,
        List<string> accumulatedConditions,
        List<CodePathVariableInfo> accumulatedVariables,
        Dictionary<string, string> suggestedValues,
        bool isPathActive,
        IReadOnlyDictionary<string, string> testValues)
    {
        var code = block.GetCode();
        var evaluated = EvaluateBlock(block, testValues);

        var summary = new CodePathOutcomeSummary
        {
            OutcomeCode = code,
            EvaluatedValue = evaluated,
            IsActive = isPathActive,
            PathConditions = new List<string>(accumulatedConditions),
            Variables = new List<CodePathVariableInfo>(accumulatedVariables),
            SuggestedVariableValues = new Dictionary<string, string>(suggestedValues, StringComparer.OrdinalIgnoreCase)
        };
        outcomes.Add(summary);

        var leafNode = new CodePathTreeNode
        {
            NodeTitle = $"Outcome: {code}",
            NodeKind = "Outcome",
            BranchLabel = isPathActive ? "ACTIVE OUTCOME" : "OUTCOME",
            OutcomeCode = code,
            EvaluatedValue = evaluated,
            IsOutcome = true,
            IsActiveInCurrentTest = isPathActive,
            VariablesAtFork = new ObservableCollection<CodePathVariableInfo>(accumulatedVariables)
        };
        parentNode.Children.Add(leafNode);
    }

    private static (List<CodePathVariableInfo> trueVars, List<CodePathVariableInfo> falseVars, Dictionary<string, string> trueSuggestions, Dictionary<string, string> falseSuggestions)
        AnalyzeConditionConstraints(CodeInput? condInput, IReadOnlyDictionary<string, string> testValues)
    {
        var trueVars = new List<CodePathVariableInfo>();
        var falseVars = new List<CodePathVariableInfo>();
        var trueSuggestions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var falseSuggestions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (condInput == null) return (trueVars, falseVars, trueSuggestions, falseSuggestions);

        if (condInput.Value is OperatorCode op)
        {
            var leftStr = op.Inputs.Count > 0 ? GetInputCode(op.Inputs[0]) : string.Empty;
            var rightStr = op.Inputs.Count > 1 ? GetInputCode(op.Inputs[1]) : string.Empty;
            var leftVar = ExtractFirstVariable(op.Inputs.Count > 0 ? op.Inputs[0] : null) ?? leftStr;
            var cleanRight = Unquote(rightStr);

            testValues.TryGetValue(leftVar, out var currentVal);

            var evaluatedLeft = op.Inputs.Count > 0 ? EvaluateInput(op.Inputs[0], testValues) : currentVal;

            var (trueCond, falseCond, trueSugg, falseSugg, isSatisfied) =
                EvaluateOperatorConstraints(op.Operator, cleanRight, evaluatedLeft);

            bool isCompound = !string.IsNullOrWhiteSpace(leftStr) && !string.Equals(leftStr.Trim(), leftVar.Trim(), StringComparison.OrdinalIgnoreCase);

            var reqTrue = isCompound ? $"{leftStr} {trueCond}" : trueCond;
            var reqFalse = isCompound ? $"{leftStr} {falseCond}" : falseCond;
            var displayVal = isCompound && !string.IsNullOrWhiteSpace(evaluatedLeft) && evaluatedLeft != currentVal
                ? $"{currentVal} (evaluated: {evaluatedLeft})"
                : currentVal;

            var field = FormgenReferenceCatalog.TryGetField(leftVar);

            trueVars.Add(new CodePathVariableInfo
            {
                Name = leftVar,
                RequiredCondition = reqTrue,
                CurrentTestValue = displayVal,
                IsSatisfied = isSatisfied,
                Hint = field?.Description ?? "Condition variable"
            });

            falseVars.Add(new CodePathVariableInfo
            {
                Name = leftVar,
                RequiredCondition = reqFalse,
                CurrentTestValue = displayVal,
                IsSatisfied = isSatisfied.HasValue ? !isSatisfied.Value : null,
                Hint = field?.Description ?? "Condition variable"
            });

            var trueSuggestedVal = isCompound && trueSugg != null
                ? SuggestVariableValueForCase(op.Inputs.Count > 0 ? op.Inputs[0] : null, leftVar, trueSugg, currentVal)
                : trueSugg;

            var falseSuggestedVal = isCompound && falseSugg != null
                ? SuggestVariableValueForCase(op.Inputs.Count > 0 ? op.Inputs[0] : null, leftVar, falseSugg, currentVal)
                : falseSugg;

            if (trueSuggestedVal != null) trueSuggestions[leftVar] = trueSuggestedVal;
            if (falseSuggestedVal != null) falseSuggestions[leftVar] = falseSuggestedVal;

            return (trueVars, falseVars, trueSuggestions, falseSuggestions);
        }

        var rawStr = condInput.Value is CodeBase cb ? cb.GetCode() : (condInput.Value?.ToString() ?? condInput.Description);
        var trimmed = rawStr?.Trim() ?? string.Empty;

        var opMatch = OperatorPattern.Match(trimmed);
        if (opMatch.Success)
        {
            var varName = opMatch.Groups[1].Value;
            var opStr = opMatch.Groups[2].Value;
            var rightVal = Unquote(opMatch.Groups[3].Value.Trim());

            testValues.TryGetValue(varName, out var currentVal);

            var (trueCond, falseCond, trueSugg, falseSugg, isSatisfied) =
                EvaluateOperatorConstraints(opStr, rightVal, currentVal);

            var field = FormgenReferenceCatalog.TryGetField(varName);

            trueVars.Add(new CodePathVariableInfo
            {
                Name = varName,
                RequiredCondition = trueCond,
                CurrentTestValue = currentVal,
                IsSatisfied = isSatisfied,
                Hint = field?.Description ?? "Condition variable"
            });

            falseVars.Add(new CodePathVariableInfo
            {
                Name = varName,
                RequiredCondition = falseCond,
                CurrentTestValue = currentVal,
                IsSatisfied = isSatisfied.HasValue ? !isSatisfied.Value : null,
                Hint = field?.Description ?? "Condition variable"
            });

            if (trueSugg != null) trueSuggestions[varName] = trueSugg;
            if (falseSugg != null) falseSuggestions[varName] = falseSugg;

            return (trueVars, falseVars, trueSuggestions, falseSuggestions);
        }

        var funcMatch = FunctionConditionPattern.Match(trimmed);
        if (funcMatch.Success)
        {
            var funcName = funcMatch.Groups[1].Value.ToUpperInvariant();
            var argStr = funcMatch.Groups[2].Value.Trim();
            var firstVar = ExtractVariableName(argStr) ?? argStr;

            testValues.TryGetValue(firstVar, out var currentVal);
            var field = FormgenReferenceCatalog.TryGetField(firstVar);

            if (funcName == "BLANK")
            {
                bool isBlank = string.IsNullOrWhiteSpace(currentVal);
                trueVars.Add(new CodePathVariableInfo { Name = firstVar, RequiredCondition = "is BLANK (\"\")", CurrentTestValue = currentVal, IsSatisfied = currentVal != null ? isBlank : null, Hint = field?.Description ?? "" });
                falseVars.Add(new CodePathVariableInfo { Name = firstVar, RequiredCondition = "is NOT BLANK", CurrentTestValue = currentVal, IsSatisfied = currentVal != null ? !isBlank : null, Hint = field?.Description ?? "" });
                trueSuggestions[firstVar] = "";
                falseSuggestions[firstVar] = "Sample";
            }
            else if (funcName == "NOTBLANK")
            {
                bool isNotBlank = !string.IsNullOrWhiteSpace(currentVal);
                trueVars.Add(new CodePathVariableInfo { Name = firstVar, RequiredCondition = "is NOT BLANK", CurrentTestValue = currentVal, IsSatisfied = currentVal != null ? isNotBlank : null, Hint = field?.Description ?? "" });
                falseVars.Add(new CodePathVariableInfo { Name = firstVar, RequiredCondition = "is BLANK (\"\")", CurrentTestValue = currentVal, IsSatisfied = currentVal != null ? !isNotBlank : null, Hint = field?.Description ?? "" });
                trueSuggestions[firstVar] = "Sample";
                falseSuggestions[firstVar] = "";
            }
            else if (funcName == "CONTAINS")
            {
                var parts = argStr.Split(',', 2);
                var sub = parts.Length > 1 ? Unquote(parts[1].Trim()) : "text";
                bool hasSub = currentVal != null && currentVal.Contains(sub, StringComparison.OrdinalIgnoreCase);

                trueVars.Add(new CodePathVariableInfo { Name = firstVar, RequiredCondition = $"contains \"{sub}\"", CurrentTestValue = currentVal, IsSatisfied = currentVal != null ? hasSub : null, Hint = field?.Description ?? "" });
                falseVars.Add(new CodePathVariableInfo { Name = firstVar, RequiredCondition = $"does NOT contain \"{sub}\"", CurrentTestValue = currentVal, IsSatisfied = currentVal != null ? !hasSub : null, Hint = field?.Description ?? "" });
                trueSuggestions[firstVar] = sub;
                falseSuggestions[firstVar] = "Other";
            }

            return (trueVars, falseVars, trueSuggestions, falseSuggestions);
        }

        // Generic identifier or boolean expression
        var vars = condInput.Value is CodeBase cbGeneral
            ? cbGeneral.GetVariables()
            : [ExtractVariableName(trimmed) ?? trimmed];

        foreach (var v in vars)
        {
            if (string.IsNullOrWhiteSpace(v)) continue;
            testValues.TryGetValue(v, out var cur);
            var isTruth = cur != null ? IsTruthy(cur) : (bool?)null;

            var field = FormgenReferenceCatalog.TryGetField(v);

            trueVars.Add(new CodePathVariableInfo
            {
                Name = v,
                RequiredCondition = "is TRUE / Truthy",
                CurrentTestValue = cur,
                IsSatisfied = isTruth,
                Hint = field?.Description ?? ""
            });

            falseVars.Add(new CodePathVariableInfo
            {
                Name = v,
                RequiredCondition = "is FALSE / Blank",
                CurrentTestValue = cur,
                IsSatisfied = isTruth.HasValue ? !isTruth.Value : null,
                Hint = field?.Description ?? ""
            });

            trueSuggestions[v] = "1";
            falseSuggestions[v] = "0";
        }

        return (trueVars, falseVars, trueSuggestions, falseSuggestions);
    }

    private static (string trueCond, string falseCond, string? trueSugg, string? falseSugg, bool? isSatisfied)
        EvaluateOperatorConstraints(string op, string rightVal, string? currentVal)
    {
        double numRight = 0;
        double numCur = 0;
        bool isNumericRight = double.TryParse(rightVal, NumberStyles.Any, CultureInfo.InvariantCulture, out numRight);
        bool isNumericCur = currentVal != null && double.TryParse(currentVal, NumberStyles.Any, CultureInfo.InvariantCulture, out numCur);

        switch (op)
        {
            case "=":
                bool? eqSat = currentVal != null
                    ? (isNumericRight && isNumericCur ? Math.Abs(numCur - numRight) < 1e-9 : string.Equals(currentVal, rightVal, StringComparison.OrdinalIgnoreCase))
                    : null;
                return ($"== '{rightVal}'", $"!= '{rightVal}'", rightVal, isNumericRight ? (numRight + 1).ToString(CultureInfo.InvariantCulture) : "OTHER", eqSat);

            case "!=":
                bool? neSat = currentVal != null
                    ? (isNumericRight && isNumericCur ? Math.Abs(numCur - numRight) >= 1e-9 : !string.Equals(currentVal, rightVal, StringComparison.OrdinalIgnoreCase))
                    : null;
                return ($"!= '{rightVal}'", $"== '{rightVal}'", isNumericRight ? (numRight + 1).ToString(CultureInfo.InvariantCulture) : "OTHER", rightVal, neSat);

            case "<":
                bool? ltSat = isNumericCur && isNumericRight ? numCur < numRight : null;
                return ($"< {rightVal}", $">= {rightVal}", isNumericRight ? (numRight - 1).ToString(CultureInfo.InvariantCulture) : "0", isNumericRight ? (numRight + 5).ToString(CultureInfo.InvariantCulture) : "10", ltSat);

            case "<=":
                bool? lteSat = isNumericCur && isNumericRight ? numCur <= numRight : null;
                return ($"<= {rightVal}", $"> {rightVal}", isNumericRight ? numRight.ToString(CultureInfo.InvariantCulture) : "0", isNumericRight ? (numRight + 5).ToString(CultureInfo.InvariantCulture) : "10", lteSat);

            case ">":
                bool? gtSat = isNumericCur && isNumericRight ? numCur > numRight : null;
                return ($"> {rightVal}", $"<= {rightVal}", isNumericRight ? (numRight + 5).ToString(CultureInfo.InvariantCulture) : "10", isNumericRight ? (numRight - 1).ToString(CultureInfo.InvariantCulture) : "0", gtSat);

            case ">=":
                bool? gteSat = isNumericCur && isNumericRight ? numCur >= numRight : null;
                return ($">= {rightVal}", $"< {rightVal}", isNumericRight ? numRight.ToString(CultureInfo.InvariantCulture) : "10", isNumericRight ? (numRight - 1).ToString(CultureInfo.InvariantCulture) : "0", gteSat);

            default:
                return ($"{op} {rightVal}", $"NOT ({op} {rightVal})", rightVal, "0", null);
        }
    }

    private static bool? EvaluateCondition(CodeInput? condInput, IReadOnlyDictionary<string, string> testValues)
    {
        if (condInput == null) return null;

        if (condInput.Value is CodeBase condBlock)
        {
            var result = condBlock.Evaluate(testValues);
            return IsTruthy(result.Value);
        }

        var str = condInput.Value?.ToString() ?? condInput.Description;
        if (string.IsNullOrWhiteSpace(str)) return null;

        var opMatch = OperatorPattern.Match(str.Trim());
        if (opMatch.Success)
        {
            var varName = opMatch.Groups[1].Value;
            var opStr = opMatch.Groups[2].Value;
            var rightVal = Unquote(opMatch.Groups[3].Value.Trim());

            if (testValues.TryGetValue(varName, out var curVal))
            {
                var (_, _, _, _, sat) = EvaluateOperatorConstraints(opStr, rightVal, curVal);
                return sat;
            }
        }

        var funcMatch = FunctionConditionPattern.Match(str.Trim());
        if (funcMatch.Success)
        {
            var fn = funcMatch.Groups[1].Value.ToUpperInvariant();
            var arg = funcMatch.Groups[2].Value.Trim();
            var varName = ExtractVariableName(arg) ?? arg;

            if (testValues.TryGetValue(varName, out var curVal))
            {
                if (fn == "BLANK") return string.IsNullOrWhiteSpace(curVal);
                if (fn == "NOTBLANK") return !string.IsNullOrWhiteSpace(curVal);
            }
        }

        if (testValues.TryGetValue(str.Trim(), out var directVal))
        {
            return IsTruthy(directVal);
        }

        return null;
    }

    private static bool IsTruthy(string val)
    {
        if (string.IsNullOrWhiteSpace(val)) return false;
        var trimmed = val.Trim();
        if (string.Equals(trimmed, "TRUE", StringComparison.OrdinalIgnoreCase) || trimmed == "1") return true;
        if (string.Equals(trimmed, "FALSE", StringComparison.OrdinalIgnoreCase) || trimmed == "0") return false;
        return true;
    }

    private static string GetInputCode(CodeInput input)
    {
        if (input.Value is CodeBase cb) return cb.GetCode();
        var raw = input.Value?.ToString() ?? string.Empty;
        return string.IsNullOrWhiteSpace(raw) ? input.Description : raw;
    }

    private static string EvaluateInput(CodeInput? input, IReadOnlyDictionary<string, string> testValues)
    {
        if (input == null) return string.Empty;
        if (input.Value is CodeBase cb) return EvaluateBlock(cb, testValues);

        var raw = input.Value?.ToString() ?? string.Empty;
        var token = string.IsNullOrWhiteSpace(raw) ? input.Description : raw.Trim();

        if (CodeBase.IsSingleQuoted(token))
            return token[1..^1];

        if (testValues.TryGetValue(token, out var val))
            return CodeBase.IsSingleQuoted(val.Trim()) ? val.Trim()[1..^1] : val.Trim();

        return token;
    }

    private static string EvaluateBlock(CodeBase block, IReadOnlyDictionary<string, string> testValues)
    {
        try
        {
            var result = block.Evaluate(testValues);
            return result.Value;
        }
        catch
        {
            return block.GetCode();
        }
    }

    private static string Unquote(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith('\'') && trimmed.EndsWith('\''))
        {
            return trimmed[1..^1];
        }
        return trimmed;
    }

    private static string? ExtractFirstVariable(CodeInput? input)
    {
        if (input == null) return null;
        if (input.Value is CodeBase cb)
        {
            var vars = cb.GetVariables();
            return vars.Count > 0 ? vars[0] : null;
        }
        var raw = input.Value?.ToString() ?? input.Description;
        return ExtractVariableName(raw);
    }

    private static string? ExtractVariableName(string text)
    {
        var m = Regex.Match(text, @"\b[A-Za-z_][A-Za-z0-9_]*\b");
        return m.Success ? m.Value : null;
    }

    private static bool ValuesMatch(string? a, string? b)
    {
        if (a == null || b == null) return false;
        var trimmedA = a.Trim();
        var trimmedB = b.Trim();
        if (string.Equals(trimmedA, trimmedB, StringComparison.OrdinalIgnoreCase)) return true;
        if (double.TryParse(trimmedA, NumberStyles.Any, CultureInfo.InvariantCulture, out double da) &&
            double.TryParse(trimmedB, NumberStyles.Any, CultureInfo.InvariantCulture, out double db))
        {
            return Math.Abs(da - db) < 1e-9;
        }
        return false;
    }

    private static string SuggestVariableValueForCase(CodeInput? cmpInput, string cmpVar, string targetVal, string? currentVal)
    {
        if (cmpInput == null) return targetVal;

        if (IsMonthExpression(cmpInput))
        {
            if (int.TryParse(targetVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out int m) && m >= 1 && m <= 12)
            {
                if (DateTime.TryParse(currentVal, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt) ||
                    DateTime.TryParse(currentVal, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
                {
                    int day = Math.Min(dt.Day, DateTime.DaysInMonth(dt.Year, m));
                    return new DateTime(dt.Year, m, day).ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
                }
                return $"{m:D2}/15/2026";
            }
        }

        if (IsDayExpression(cmpInput))
        {
            if (int.TryParse(targetVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out int d) && d >= 1 && d <= 31)
            {
                if (DateTime.TryParse(currentVal, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt) ||
                    DateTime.TryParse(currentVal, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
                {
                    int day = Math.Min(d, DateTime.DaysInMonth(dt.Year, dt.Month));
                    return new DateTime(dt.Year, dt.Month, day).ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
                }
                return $"04/{d:D2}/2026";
            }
        }

        if (IsYearExpression(cmpInput))
        {
            if (int.TryParse(targetVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) && y >= 1900 && y <= 2100)
            {
                if (DateTime.TryParse(currentVal, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt) ||
                    DateTime.TryParse(currentVal, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out dt))
                {
                    return new DateTime(y, dt.Month, dt.Day).ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
                }
                return $"04/15/{y}";
            }
        }

        if (IsModulo10Expression(cmpInput))
        {
            if (int.TryParse(targetVal, NumberStyles.Integer, CultureInfo.InvariantCulture, out int rem) && rem >= 0 && rem < 10)
            {
                return (20 + rem).ToString(CultureInfo.InvariantCulture);
            }
        }

        return targetVal;
    }

    private static string SuggestDefaultVariableValue(CodeInput? cmpInput, string cmpVar, List<string> allCaseValues, string? currentVal)
    {
        if (cmpInput != null && IsMonthExpression(cmpInput))
        {
            return "99/99/9999";
        }

        if (cmpInput != null && IsModulo10Expression(cmpInput))
        {
            for (int digit = 0; digit < 10; digit++)
            {
                var strDigit = digit.ToString(CultureInfo.InvariantCulture);
                if (!allCaseValues.Any(v => ValuesMatch(v, strDigit)))
                {
                    return (20 + digit).ToString(CultureInfo.InvariantCulture);
                }
            }
            return "24";
        }

        var numericVals = new HashSet<double>();
        foreach (var v in allCaseValues)
        {
            if (double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
            {
                numericVals.Add(d);
            }
        }

        if (numericVals.Count > 0)
        {
            double candidate = 999;
            while (numericVals.Contains(candidate)) candidate++;
            return candidate.ToString(CultureInfo.InvariantCulture);
        }

        return "OTHER";
    }

    private static bool IsMonthExpression(CodeInput input)
    {
        if (input.Value is MonthCode) return true;
        if (input.Value is CodeBase cb && cb.Prefix == "MONTH") return true;
        var code = GetInputCode(input);
        return code.StartsWith("MONTH(", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDayExpression(CodeInput input)
    {
        if (input.Value is DayCode) return true;
        if (input.Value is CodeBase cb && cb.Prefix == "DAY") return true;
        var code = GetInputCode(input);
        return code.StartsWith("DAY(", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsYearExpression(CodeInput input)
    {
        if (input.Value is YearCode) return true;
        if (input.Value is CodeBase cb && cb.Prefix == "YEAR") return true;
        var code = GetInputCode(input);
        return code.StartsWith("YEAR(", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsModulo10Expression(CodeInput input)
    {
        if (input.Value is OperatorCode op && op.Operator == "%")
        {
            var right = op.Inputs.Count > 1 ? GetInputCode(op.Inputs[1]).Trim() : string.Empty;
            return right == "10";
        }
        var code = GetInputCode(input);
        return code.Contains("% 10") || code.Contains("%10");
    }
}
