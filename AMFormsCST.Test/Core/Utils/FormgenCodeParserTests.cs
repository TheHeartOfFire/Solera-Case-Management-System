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

    [Fact]
    public void Parse_BareFField_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("F72");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("Field F72", result.StatusTitle);
        Assert.Equal("F72", result.CondensedBlock.GetCode());
        Assert.False(result.HasSuggestedFix);
    }

    [Fact]
    public void Parse_SmartFieldVariable_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("CashPrice");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("Smart Field: CashPrice", result.StatusTitle);
        Assert.Equal("CashPrice", result.CondensedBlock.GetCode());
        Assert.False(result.HasSuggestedFix);
    }

    [Fact]
    public void Parse_SingleQuotedTextLiteral_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("'Debt Cancellation - E. Fee Paid'");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("Text Literal", result.StatusTitle);
        Assert.Equal("'Debt Cancellation - E. Fee Paid'", result.CondensedBlock.GetCode());
        Assert.False(result.HasSuggestedFix);
    }

    [Fact]
    public void Parse_DoubleQuotedTextLiteral_ReturnsValidWithSingleQuoteSuggestion()
    {
        var result = FormgenCodeParser.Parse("\"N/A\"");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("Text Literal", result.StatusTitle);
        Assert.Equal("'N/A'", result.SuggestedFix);
    }

    [Fact]
    public void Parse_PureNumericLiteral_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("100.50");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("Number: 100.50", result.StatusTitle);
        Assert.Equal("100.50", result.CondensedBlock.GetCode());
    }

    [Fact]
    public void Parse_StaticSymbol_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("***");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("Static Text: ***", result.StatusTitle);
        Assert.Equal("***", result.CondensedBlock.GetCode());
    }

    [Fact]
    public void Parse_EquationOfFFieldsAndNumbers_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("F10 + F20");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("F10 + F20", result.CondensedBlock.GetCode());
        Assert.False(result.HasSuggestedFix);
    }

    [Fact]
    public void Parse_EquationWithSmartFields_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("CashPrice - F2867");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("CashPrice - F2867", result.CondensedBlock.GetCode());
    }

    [Fact]
    public void Parse_MultiTokenEquation_ReturnsValid()
    {
        var result = FormgenCodeParser.Parse("ACC01_PRICE + ACC02_PRICE + ACC03_PRICE + ACC04_PRICE + ACC05_PRICE");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.False(result.HasSuggestedFix);
    }

    [Fact]
    public void Parse_MalformedFunction_DoesNotSuggestNullFix()
    {
        // When syntax is malformed, suggested fix should never match the original input text
        var result = FormgenCodeParser.Parse("UNKNOWNFUNC(F10, 'TEST')");
        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        if (result.SuggestedFix != null)
        {
            Assert.NotEqual("UNKNOWNFUNC(F10, 'TEST')", result.SuggestedFix);
        }
    }

    [Fact]
    public void SmartFieldCode_OutputsBareNameAndClonesCleanly()
    {
        var sf = new SmartFieldCode("TradeCreditN");
        Assert.Equal("TradeCreditN", sf.GetCode());
        Assert.Equal("SF", sf.Prefix);

        var clone = (SmartFieldCode)sf.Clone();
        Assert.Equal("TradeCreditN", clone.FieldName);
        Assert.Equal("TradeCreditN", clone.GetCode());
    }
}
