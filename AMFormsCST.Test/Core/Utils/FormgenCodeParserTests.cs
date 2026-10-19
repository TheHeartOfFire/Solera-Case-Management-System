using AMFormsCST.Core.Types.CodeBlocks.Formulae;
using AMFormsCST.Core.Types.CodeBlocks.Functions;
using AMFormsCST.Core.Utils;
using Xunit;

namespace AMFormsCST.Test.Core.Utils;

public class FormgenCodeParserTests
{
    [Fact]
    public void Parse_NullOrWhitespace_ReturnsUnrecognized()
    {
        var result = FormgenCodeParser.Parse("   ");
        Assert.Equal(FormgenParseStatus.Unrecognized, result.Status);
        Assert.Null(result.CondensedBlock);
    }

    [Fact]
    public void Parse_RandomProse_ReturnsUnrecognized()
    {
        var result = FormgenCodeParser.Parse("Hello world this is not a formula");
        Assert.Equal(FormgenParseStatus.Unrecognized, result.Status);
        Assert.Null(result.CondensedBlock);
        Assert.Contains("does not match any recognizable", result.DiagnosticMessage);
    }

    [Fact]
    public void Parse_ValidIfExpression_ReturnsValidSnippetBlock()
    {
        var result = FormgenCodeParser.Parse("IF(F72 = 'N', 'NEW', 'USED')");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.IsType<CustomSnippetCode>(result.CondensedBlock);

        var snippet = result.CondensedBlock;
        Assert.Equal("IF( F72 = 'N', 'NEW', 'USED' )", snippet.GetCode());
        Assert.True(snippet.Inputs.Count >= 1);
    }

    [Fact]
    public void Parse_ValidSeplistExpression_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("SEPLIST(', ', F93, F94)");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("SEPLIST( ', ', F93, F94 )", result.CondensedBlock.GetCode());
    }

    [Fact]
    public void Parse_ValidNestedExpression_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("IF(CONTAINS(F10, 'TEST'), 'MATCH', 'NO')");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("IF( CONTAINS( F10, 'TEST' ), 'MATCH', 'NO' )", result.CondensedBlock.GetCode());
    }

    [Fact]
    public void Parse_ValidNoArgFunction_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("TODAY()");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("TODAY()", result.CondensedBlock.GetCode());
    }

    [Fact]
    public void Parse_MalformedDoubleQuotes_DetectsAndSuggestsSingleQuotes()
    {
        var result = FormgenCodeParser.Parse("IF(F72 = \"N\", \"NEW\", \"USED\")");
        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.Contains("double quotes", result.DiagnosticMessage.ToLowerInvariant());
        Assert.NotNull(result.SuggestedFix);
        Assert.Equal("IF( F72 = 'N', 'NEW', 'USED' )", result.SuggestedFix);
    }

    [Fact]
    public void Parse_MalformedTypoFunctionName_SuggestsCorrection()
    {
        var result = FormgenCodeParser.Parse("SEPLST(', ', F1, F2)");
        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.Contains("SEPLIST", result.DiagnosticMessage);
        Assert.NotNull(result.SuggestedFix);
        Assert.Equal("SEPLIST( ', ', F1, F2 )", result.SuggestedFix);
    }

    [Fact]
    public void Parse_MalformedUnbalancedParens_ReturnsMalformed()
    {
        var result = FormgenCodeParser.Parse("IF(F72 = 'N', 'NEW', 'USED'");
        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.Contains("parenthesis", result.DiagnosticMessage.ToLowerInvariant());
        Assert.NotNull(result.SuggestedFix);
        Assert.EndsWith(")", result.SuggestedFix);
    }

    [Fact]
    public void Parse_MalformedSemicolonSeparators_DetectsAndSuggestsCommas()
    {
        var result = FormgenCodeParser.Parse("IF(F72 = 'N'; 'NEW'; 'USED')");
        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.Contains("semicolon", result.DiagnosticMessage.ToLowerInvariant());
        Assert.NotNull(result.SuggestedFix);
        Assert.Equal("IF( F72 = 'N', 'NEW', 'USED' )", result.SuggestedFix);
    }

    [Fact]
    public void Parse_MalformedMissingArguments_ReturnsDiagnostic()
    {
        var result = FormgenCodeParser.Parse("ROUND(F11)");
        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.Contains("2 argument(s)", result.DiagnosticMessage);
    }

    [Fact]
    public void GetPseudoCode_IfCode_ProducesBranchExplanation()
    {
        var ifCode = new IfCode();
        ifCode.Inputs[0].SetValue("F72 = 'N'");
        ifCode.Inputs[1].SetValue("'NEW'");
        ifCode.Inputs[2].SetValue("'USED'");

        var pseudo = ifCode.GetPseudoCode();
        Assert.Contains("IF (F72 = 'N') IS TRUE THEN:", pseudo);
        Assert.Contains("OTHERWISE (if condition is FALSE):", pseudo);
    }

    [Fact]
    public void GetPseudoCode_CaseCode_ProducesBranchExplanation()
    {
        var caseCode = new CaseCode();
        caseCode.Inputs[0].SetValue("F1 = 1");
        caseCode.Inputs[1].SetValue("'ONE'");
        caseCode.Inputs[2].SetValue("F1 = 2");
        caseCode.Inputs[3].SetValue("'TWO'");

        var pseudo = caseCode.GetPseudoCode();
        Assert.Contains("CHECK VALUE OF (F1 = 1):", pseudo);
        Assert.Contains("Branch 1", pseudo);
    }

    [Fact]
    public void GetPseudoCode_SeplistCode_ProducesExplanation()
    {
        var seplist = new SeplistCode();
        seplist.Inputs[0].SetValue("', '");
        seplist.Inputs[1].SetValue("F1");
        seplist.Inputs[2].SetValue("F2");

        var pseudo = seplist.GetPseudoCode();
        Assert.Contains("JOIN NON-BLANK ITEMS USING SEPARATOR ', '", pseudo);
    }

    [Fact]
    public void GetPseudoCode_RoundCode_ProducesExplanation()
    {
        var round = new RoundCode();
        round.Inputs[0].SetValue("F100");
        round.Inputs[1].SetValue("2");

        var pseudo = round.GetPseudoCode();
        Assert.Contains("ROUND (F100) TO 2", pseudo);
    }

    [Fact]
    public void GetPseudoCode_ContainsCode_ProducesExplanation()
    {
        var contains = new ContainsCode();
        contains.Inputs[0].SetValue("F10");
        contains.Inputs[1].SetValue("'SOLERA'");

        var pseudo = contains.GetPseudoCode();
        Assert.Contains("CHECK IF (F10", pseudo);
        Assert.Contains("CONTAINS", pseudo);
    }
}
