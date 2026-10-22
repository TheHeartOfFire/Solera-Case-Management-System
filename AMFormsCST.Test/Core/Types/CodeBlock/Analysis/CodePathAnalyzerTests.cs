using AMFormsCST.Core.Types.CodeBlocks.Analysis;
using AMFormsCST.Core.Types.CodeBlocks.Formulae;
using AMFormsCST.Core.Types.CodeBlocks.Functions;
using Assert = Xunit.Assert;

namespace AMFormsCST.Test.Core.Types.CodeBlock.Analysis;

public class CodePathAnalyzerTests
{
    [Fact]
    public void Analyze_NullBlock_ReturnsEmptyResult()
    {
        var result = CodePathAnalyzer.Analyze(null);
        Assert.NotNull(result);
        Assert.Null(result.RootNode);
        Assert.Empty(result.Outcomes);
        Assert.Equal(0, result.TotalForksCount);
        Assert.False(result.HasForks);
    }

    [Fact]
    public void Analyze_LinearExpression_ReturnsSingleDeterministicOutcome()
    {
        var seplist = new SeplistCode();
        seplist.SetInputValue(0, "', '");
        seplist.SetInputValue(1, "F93");
        seplist.SetInputValue(2, "F94");

        var testValues = new Dictionary<string, string>
        {
            ["F93"] = "Austin",
            ["F94"] = "TX"
        };

        var result = CodePathAnalyzer.Analyze(seplist, testValues);

        Assert.NotNull(result.RootNode);
        Assert.Single(result.Outcomes);
        Assert.Equal(0, result.TotalForksCount);
        Assert.False(result.HasForks);

        var outcome = result.Outcomes[0];
        Assert.True(outcome.IsActive);
        Assert.Equal("Austin, TX", outcome.EvaluatedValue);
        Assert.Equal(2, outcome.Variables.Count);
        Assert.Contains(outcome.Variables, v => v.Name == "F93" && v.CurrentTestValue == "Austin");
        Assert.Contains(outcome.Variables, v => v.Name == "F94" && v.CurrentTestValue == "TX");
    }

    [Fact]
    public void Analyze_SimpleIfStatement_ProducesTwoOutcomesAndCorrectActivePath()
    {
        var ifCode = new IfCode();
        var op = new OperatorCode("=");
        op.SetInputValue(0, "F72");
        op.SetInputValue(1, "'N'");
        ifCode.SetInputValue(0, op);
        ifCode.SetInputValue(1, "'NEW'");
        ifCode.SetInputValue(2, "'USED'");

        // Test with F72 = "N" (True branch active)
        var testValues = new Dictionary<string, string> { ["F72"] = "N" };
        var result = CodePathAnalyzer.Analyze(ifCode, testValues);

        Assert.Equal(2, result.TotalOutcomesCount);
        Assert.Equal(1, result.TotalForksCount);
        Assert.True(result.HasForks);

        // Path 1 (True)
        var pathTrue = result.Outcomes[0];
        Assert.True(pathTrue.IsActive);
        Assert.Equal("'NEW'", pathTrue.OutcomeCode);
        Assert.Equal("NEW", pathTrue.EvaluatedValue);
        Assert.Equal("N", pathTrue.SuggestedVariableValues["F72"]);
        Assert.Single(pathTrue.Variables);
        Assert.Equal("F72", pathTrue.Variables[0].Name);
        Assert.True(pathTrue.Variables[0].IsSatisfied);

        // Path 2 (False)
        var pathFalse = result.Outcomes[1];
        Assert.False(pathFalse.IsActive);
        Assert.Equal("'USED'", pathFalse.OutcomeCode);
        Assert.Equal("USED", pathFalse.EvaluatedValue);
        Assert.Equal("OTHER", pathFalse.SuggestedVariableValues["F72"]);
        Assert.False(pathFalse.Variables[0].IsSatisfied);

        // Test tree nodes
        Assert.NotNull(result.RootNode);
        Assert.Equal(2, result.RootNode.Children.Count);
        Assert.True(result.RootNode.Children[0].IsActiveInCurrentTest);
        Assert.False(result.RootNode.Children[1].IsActiveInCurrentTest);
    }

    [Fact]
    public void Analyze_NestedIfStatement_ProducesThreeOutcomesAndDeepBranches()
    {
        // IF (F72 = 'N', 'NEW', IF (F72 = 'U', 'USED', 'OTHER'))
        var nestedIf = new IfCode();
        var op1 = new OperatorCode("=").SetInputValue(0, "F72").SetInputValue(1, "'N'");
        var op2 = new OperatorCode("=").SetInputValue(0, "F72").SetInputValue(1, "'U'");

        var innerIf = new IfCode()
            .SetInputValue(0, op2)
            .SetInputValue(1, "'USED'")
            .SetInputValue(2, "'OTHER'");

        nestedIf.SetInputValue(0, op1);
        nestedIf.SetInputValue(1, "'NEW'");
        nestedIf.SetInputValue(2, innerIf);

        var testValues = new Dictionary<string, string> { ["F72"] = "U" };
        var result = CodePathAnalyzer.Analyze(nestedIf, testValues);

        Assert.Equal(3, result.TotalOutcomesCount);
        Assert.Equal(2, result.TotalForksCount);

        // Path 1: F72 = 'N' -> 'NEW' (Inactive)
        Assert.False(result.Outcomes[0].IsActive);

        // Path 2: F72 != 'N' && F72 = 'U' -> 'USED' (Active!)
        Assert.True(result.Outcomes[1].IsActive);
        Assert.Equal("'USED'", result.Outcomes[1].OutcomeCode);
        Assert.Equal("U", result.Outcomes[1].SuggestedVariableValues["F72"]);

        // Path 3: F72 != 'N' && F72 != 'U' -> 'OTHER' (Inactive)
        Assert.False(result.Outcomes[2].IsActive);
    }

    [Fact]
    public void Analyze_CaseStatement_ProducesAllCaseOutcomesAndDefault()
    {
        var caseBlock = new CaseCode();
        caseBlock.SetInputValue(0, "F10");
        caseBlock.SetInputValue(1, "'A'");
        caseBlock.SetInputValue(2, "'Alpha'");
        caseBlock.SetInputValue(3, "'B'");
        caseBlock.SetInputValue(4, "'Beta'");
        caseBlock.SetInputValue(5, "'None'");

        var testValues = new Dictionary<string, string> { ["F10"] = "B" };
        var result = CodePathAnalyzer.Analyze(caseBlock, testValues);

        Assert.Equal(3, result.TotalOutcomesCount); // Case A, Case B, Default

        Assert.False(result.Outcomes[0].IsActive);
        Assert.Equal("'Alpha'", result.Outcomes[0].OutcomeCode);
        Assert.Equal("A", result.Outcomes[0].SuggestedVariableValues["F10"]);

        Assert.True(result.Outcomes[1].IsActive);
        Assert.Equal("'Beta'", result.Outcomes[1].OutcomeCode);
        Assert.Equal("B", result.Outcomes[1].SuggestedVariableValues["F10"]);

        Assert.False(result.Outcomes[2].IsActive);
        Assert.Equal("'None'", result.Outcomes[2].OutcomeCode);
        Assert.Equal("OTHER", result.Outcomes[2].SuggestedVariableValues["F10"]);
    }

    [Fact]
    public void Analyze_LeadingZeroFormula_UnwrapsStructureAndProducesOutcomes()
    {
        var lz = new LeadingZeroCode();
        lz.SetInputValue(0, "5");

        var testValues = new Dictionary<string, string> { ["5"] = "5" };
        var result = CodePathAnalyzer.Analyze(lz, testValues);

        Assert.Equal(2, result.TotalOutcomesCount);
        Assert.Equal(1, result.TotalForksCount);
        Assert.True(result.HasForks);

        // Path 1 (True: 5 < 10) -> SEPLIST('', '0', 5) = 05
        var truePath = result.Outcomes[0];
        Assert.True(truePath.IsActive);
        Assert.Equal("05", truePath.EvaluatedValue);

        // Path 2 (False: >= 10)
        var falsePath = result.Outcomes[1];
        Assert.False(falsePath.IsActive);
    }

    [Fact]
    public void Analyze_XifBlock_ProducesCheckedAndUncheckedOutcomes()
    {
        var xif = new FormgenFunctionCode();
        var op = new OperatorCode("=").SetInputValue(0, "F50").SetInputValue(1, "'Y'");
        xif.SetInputValue(0, op);

        var testValues = new Dictionary<string, string> { ["F50"] = "Y" };
        var result = CodePathAnalyzer.Analyze(xif, testValues);

        Assert.Equal(2, result.TotalOutcomesCount);
        Assert.True(result.Outcomes[0].IsActive);
        Assert.Equal("'X'", result.Outcomes[0].OutcomeCode);
        Assert.Equal("X", result.Outcomes[0].EvaluatedValue);

        Assert.False(result.Outcomes[1].IsActive);
        Assert.Equal("''", result.Outcomes[1].OutcomeCode);
        Assert.Equal("", result.Outcomes[1].EvaluatedValue);
    }

    [Fact]
    public void Analyze_MonthNameCode_EvaluatesCompoundComparisonAndMatchesCase4()
    {
        var monthBlock = new MonthNameCode();
        // Date Field set to April 15, 2026
        var testValues = new Dictionary<string, string> { ["Date Field"] = "04/15/2026" };

        var result = CodePathAnalyzer.Analyze(monthBlock, testValues);

        // 12 months + 1 default (Invalid Month) = 13 outcomes
        Assert.Equal(13, result.TotalOutcomesCount);

        // Path 4 (April) should be the active path
        var aprilOutcome = result.Outcomes.FirstOrDefault(o => o.OutcomeCode == "'APRIL'");
        Assert.NotNull(aprilOutcome);
        Assert.True(aprilOutcome.IsActive);
        Assert.Equal("APRIL", aprilOutcome.EvaluatedValue);

        // April outcome's variable requirement should be satisfied
        var aprilVar = aprilOutcome.Variables.FirstOrDefault(v => v.Name == "Date Field");
        Assert.NotNull(aprilVar);
        Assert.True(aprilVar.IsSatisfied);
        Assert.Contains("MONTH", aprilVar.RequiredCondition);
        Assert.Contains("04/15/2026", aprilVar.CurrentTestValue);
        Assert.Contains("4", aprilVar.CurrentTestValue);

        // January outcome should NOT be active
        var janOutcome = result.Outcomes.FirstOrDefault(o => o.OutcomeCode == "'JANUARY'");
        Assert.NotNull(janOutcome);
        Assert.False(janOutcome.IsActive);
        Assert.Equal("01/15/2026", janOutcome.SuggestedVariableValues["Date Field"]);

        // Default branch (Invalid Month) should NOT be active
        var defOutcome = result.Outcomes.Last();
        Assert.False(defOutcome.IsActive);

        // Tree node verification: Case 4 node in tree should be active and expanded
        Assert.True(result.RootNode?.IsExpanded);
        var case4Node = result.RootNode?.Children.FirstOrDefault(n => n.BranchLabel == "CASE '4'");
        Assert.NotNull(case4Node);
        Assert.True(case4Node.IsActiveInCurrentTest);
        Assert.True(case4Node.IsExpanded);

        // Passed branches should be collapsed (not expanded)
        var case1Node = result.RootNode?.Children.FirstOrDefault(n => n.BranchLabel == "CASE '1'");
        Assert.NotNull(case1Node);
        Assert.False(case1Node.IsActiveInCurrentTest);
        Assert.False(case1Node.IsExpanded);

        var case2Node = result.RootNode?.Children.FirstOrDefault(n => n.BranchLabel == "CASE '2'");
        Assert.NotNull(case2Node);
        Assert.False(case2Node.IsExpanded);

        var defNode = result.RootNode?.Children.FirstOrDefault(n => n.BranchLabel == "DEFAULT");
        Assert.NotNull(defNode);
        Assert.False(defNode.IsExpanded);
    }

    [Fact]
    public void Analyze_DayAndSuffixCode_EvaluatesModuloAndMatches21st()
    {
        var daySuffixBlock = new DayAndSuffixCode();
        var testValues = new Dictionary<string, string> { ["Numeric Day"] = "21" };

        var result = CodePathAnalyzer.Analyze(daySuffixBlock, testValues);

        // 3 outer cases (11, 12, 13) + 4 inner cases (1, 2, 3, default) = 7 outcomes
        Assert.Equal(7, result.TotalOutcomesCount);

        // Outcome for 21st should be active with evaluated value "21st"
        var activeOutcome = result.Outcomes.FirstOrDefault(o => o.IsActive);
        Assert.NotNull(activeOutcome);
        Assert.Equal("21st", activeOutcome.EvaluatedValue);
        Assert.Contains("'st'", activeOutcome.OutcomeCode);

        // Verify expansion states:
        Assert.True(result.RootNode?.IsExpanded);

        // Outer teen cases passed -> collapsed
        var teen11Node = result.RootNode?.Children.FirstOrDefault(n => n.BranchLabel == "CASE '11'");
        Assert.NotNull(teen11Node);
        Assert.False(teen11Node.IsExpanded);

        // Outer default taken -> expanded
        var outerDefNode = result.RootNode?.Children.FirstOrDefault(n => n.BranchLabel == "DEFAULT");
        Assert.NotNull(outerDefNode);
        Assert.True(outerDefNode.IsExpanded);

        // Inner case 1 ('st') taken -> expanded
        var inner1Node = outerDefNode.Children.FirstOrDefault(n => n.BranchLabel == "CASE '1'");
        Assert.NotNull(inner1Node);
        Assert.True(inner1Node.IsExpanded);

        // Inner case 2 ('nd') passed -> collapsed
        var inner2Node = outerDefNode.Children.FirstOrDefault(n => n.BranchLabel == "CASE '2'");
        Assert.NotNull(inner2Node);
        Assert.False(inner2Node.IsExpanded);

        // Verify suggested values for all paths:
        var stOutcome = result.Outcomes.FirstOrDefault(o => o.OutcomeCode.Contains("'st'"));
        Assert.NotNull(stOutcome);
        Assert.Equal("21", stOutcome.SuggestedVariableValues["Numeric Day"]);

        var ndOutcome = result.Outcomes.FirstOrDefault(o => o.OutcomeCode.Contains("'nd'"));
        Assert.NotNull(ndOutcome);
        Assert.Equal("22", ndOutcome.SuggestedVariableValues["Numeric Day"]);

        var rdOutcome = result.Outcomes.FirstOrDefault(o => o.OutcomeCode.Contains("'rd'"));
        Assert.NotNull(rdOutcome);
        Assert.Equal("23", rdOutcome.SuggestedVariableValues["Numeric Day"]);

        // Outer case for 11th
        var teen11Outcome = result.Outcomes.FirstOrDefault(o => o.PathConditions.Any(c => c.Contains("11")));
        Assert.NotNull(teen11Outcome);
        Assert.Equal("11", teen11Outcome.SuggestedVariableValues["Numeric Day"]);
    }
}

