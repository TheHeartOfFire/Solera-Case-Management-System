using AMFormsCST.Core.Types.CodeBlocks.Formulae;
using AMFormsCST.Core.Types.CodeBlocks.Functions;
using AMFormsCST.Core.Utils;
using System.IO;
using Xunit;

using Xunit.Abstractions;

namespace AMFormsCST.Test.Core.Utils;

public class FormgenCodeParserTests
{
    private readonly ITestOutputHelper? _output;

    public FormgenCodeParserTests(ITestOutputHelper? output = null)
    {
        _output = output;
    }
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
    public void Parse_XifWithParensInLiteral_ReturnsValid()
    {
        var code = "XIF(Odom = '(1) I here by certify that the odometer reading reflects the amount of mileage in excess of its mechanical limits.')";
        var result = FormgenCodeParser.Parse(code);
        _output?.WriteLine($"Status: {result.Status}, Diag: {result.DiagnosticMessage}, Fix: {result.SuggestedFix}");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
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
        Assert.Equal("IF(F72 = 'N', 'NEW', 'USED')", result.SuggestedFix);
    }

    [Fact]
    public void Parse_MalformedTypoFunctionName_SuggestsCorrection()
    {
        var result = FormgenCodeParser.Parse("SEPLST(', ', F1, F2)");
        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.Contains("SEPLIST", result.DiagnosticMessage);
        Assert.NotNull(result.SuggestedFix);
        Assert.Equal("SEPLIST(', ', F1, F2)", result.SuggestedFix);
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
        Assert.Equal("IF(F72 = 'N', 'NEW', 'USED')", result.SuggestedFix);
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
        var result = FormgenCodeParser.Parse("BuyerName");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("Predefined Smart Field: BuyerName", result.StatusTitle);
        Assert.Equal("BuyerName", result.CondensedBlock.GetCode());
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
        var result = FormgenCodeParser.Parse("ACC01_PRICE - F2867");
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.NotNull(result.CondensedBlock);
        Assert.Equal("ACC01_PRICE - F2867", result.CondensedBlock.GetCode());
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
        var sf = new SmartFieldCode("BuyerName");
        Assert.Equal("BuyerName", sf.GetCode());
        Assert.Equal("SF", sf.Prefix);
        Assert.True(sf.IsPredefined);

        var clone = (SmartFieldCode)sf.Clone();
        Assert.Equal("BuyerName", clone.FieldName);
        Assert.Equal("BuyerName", clone.GetCode());
        Assert.True(clone.IsPredefined);
    }

    [Theory]
    [InlineData("BuyerName")]
    [InlineData("CobuyerAddr")]
    [InlineData("_SALEDATE")]
    [InlineData("HASCOBUYER")]
    [InlineData("ACC01_COMPNAME")]
    [InlineData("ACC15_POSPRICE")]
    [InlineData("HASACC01")]
    [InlineData("_DEALNUM")]
    [InlineData("TOTALREBATEDESC")]
    public void Parse_PredefinedSmartField_IdentifiesAsPredefined(string smartFieldName)
    {
        var result = FormgenCodeParser.Parse(smartFieldName);
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
        Assert.Contains("Predefined Smart Field", result.StatusTitle);
        Assert.NotNull(result.ParsedTree);
        Assert.IsType<SmartFieldCode>(result.ParsedTree);
        var sf = (SmartFieldCode)result.ParsedTree;
        Assert.True(sf.IsPredefined);
        Assert.Equal(smartFieldName, sf.FieldName);
    }

    [Fact]
    public void Parse_SamplePdfFormgen_AllExpressionsValid()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "AMFormsCST.Desktop", "SampleData", "Formgen Sample Data", "Pdf Sample", "Sample Pdf.formgen");
        var fullPath = Path.GetFullPath(path);
        Assert.True(File.Exists(fullPath), $"File should exist at {fullPath}");
        var doc = new System.Xml.XmlDocument();
        doc.Load(fullPath);
        var nodes = doc.SelectNodes("//expression");
        Assert.NotNull(nodes);

        var failures = new List<string>();
        foreach (System.Xml.XmlNode n in nodes)
        {
            var text = n.InnerText?.Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;
            var res = FormgenCodeParser.Parse(text);
            if (res.Status != FormgenParseStatus.Valid)
            {
                failures.Add($"[{text}] -> {res.Status}: {res.DiagnosticMessage} (Fix: '{res.SuggestedFix}')");
            }
        }

        Assert.True(failures.Count == 0, "Failures:\n" + string.Join("\n", failures));
    }

    [Theory]
    [InlineData("IF(F667>0 && NOTBLANK(F49),F456,'')")]
    [InlineData("IF(F667=0 || BLANK(F49),'NA','')")]
    [InlineData("IF(F17 != 'J' || BLANK(F49),'NA','')")]
    public void Parse_CompoundConditionsWithLogicalAmpersandsAndPipes_ParsesValid(string code)
    {
        var result = FormgenCodeParser.Parse(code);
        Assert.Equal(FormgenParseStatus.Valid, result.Status);
    }

    [Fact]
    public void FormgenReferenceCatalog_ContainsPredefinedVariablesFromFormGen()
    {
        Assert.True(FormgenReferenceCatalog.IsPredefinedSmartField("BuyerName"));
        Assert.True(FormgenReferenceCatalog.IsPredefinedSmartField("BUYERCITYSTATEZIP"));
        Assert.True(FormgenReferenceCatalog.IsPredefinedSmartField("HASCOBUYER"));
        Assert.True(FormgenReferenceCatalog.IsPredefinedSmartField("ACC01_COST"));
        Assert.True(FormgenReferenceCatalog.IsPredefinedSmartField("ACC15_COMPNAME"));
        Assert.True(FormgenReferenceCatalog.IsPredefinedSmartField("HASACC03"));
        Assert.True(FormgenReferenceCatalog.IsPredefinedSmartField("_DEALNUM"));
        Assert.True(FormgenReferenceCatalog.IsPredefinedSmartField("_SALEDATE"));

        // Non-working aliases and unconfirmed smart fields should return false
        Assert.False(FormgenReferenceCatalog.IsPredefinedSmartField("DEALNUM"));
        Assert.False(FormgenReferenceCatalog.IsPredefinedSmartField("SALEDATE"));
        Assert.False(FormgenReferenceCatalog.IsPredefinedSmartField("BUYERCSZ"));
        Assert.False(FormgenReferenceCatalog.IsPredefinedSmartField("HASCOREG"));
        Assert.False(FormgenReferenceCatalog.IsPredefinedSmartField("NonExistentCustomVar123"));
    }

    [Fact]
    public void Parse_MissingParenInNestedIfWithinSeplist_RepairsParenWithoutDiscardingRemainingArguments()
    {
        // User test case: valid SEPLIST formula where closing parenthesis for the first IF was omitted
        var input = "SEPLIST(' ', IF(NOTBLANK(F340),'TRADE #2: ',' ', F340, F341, F339, F345, F479, IF(NOTBLANK(F351), 'TRADE #3: ', ' '), F351, F352, F350, F356, F480)";

        var result = FormgenCodeParser.Parse(input);

        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.NotNull(result.SuggestedFix);

        // Crucial requirement: Must NOT truncate arguments! Both IFs and all F-fields must be preserved!
        Assert.Contains("TRADE #2:", result.SuggestedFix);
        Assert.Contains("TRADE #3:", result.SuggestedFix);
        Assert.Contains("F340", result.SuggestedFix);
        Assert.Contains("F341", result.SuggestedFix);
        Assert.Contains("F339", result.SuggestedFix);
        Assert.Contains("F345", result.SuggestedFix);
        Assert.Contains("F479", result.SuggestedFix);
        Assert.Contains("F351", result.SuggestedFix);
        Assert.Contains("F352", result.SuggestedFix);
        Assert.Contains("F350", result.SuggestedFix);
        Assert.Contains("F356", result.SuggestedFix);
        Assert.Contains("F480", result.SuggestedFix);

        // The suggested fix itself must be a 100% valid formula
        var reparsed = FormgenCodeParser.Parse(result.SuggestedFix);
        Assert.Equal(FormgenParseStatus.Valid, reparsed.Status);
        Assert.NotNull(reparsed.CondensedBlock);
    }

    [Fact]
    public void Parse_NestedFunctionWithExtraArgsInsideParent_SpillsArgumentsCleanly()
    {
        var input = "SEPLIST(' ', IF(NOTBLANK(F340),'TRADE #2: ',' ', F340, F341))";

        var result = FormgenCodeParser.Parse(input);

        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.NotNull(result.SuggestedFix);
        Assert.Contains("F340", result.SuggestedFix);
        Assert.Contains("F341", result.SuggestedFix);

        var reparsed = FormgenCodeParser.Parse(result.SuggestedFix);
        Assert.Equal(FormgenParseStatus.Valid, reparsed.Status);
    }

    [Fact]
    public void Parse_MissingOpeningQuoteInNestedArgument_RepairsOpeningQuoteCleanly()
    {
        // User test case: opening quote before TRADE #2: was removed
        var input = "SEPLIST(' ', IF(NOTBLANK(F340),TRADE #2: ',' '), F340, F341, F339, F345, F479, IF(NOTBLANK(F351), 'TRADE #3: ', ' '), F351, F352, F350, F356, F480)";

        var result = FormgenCodeParser.Parse(input);

        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.NotNull(result.SuggestedFix);

        // Crucial requirement: Must NOT create corrupted triple quotes like ' ' ' or extra trailing arguments!
        Assert.DoesNotContain("' ' '", result.SuggestedFix);
        Assert.DoesNotContain("', '')", result.SuggestedFix);
        Assert.Contains("'TRADE #2: '", result.SuggestedFix);
        Assert.Contains("'TRADE #3: '", result.SuggestedFix);

        var reparsed = FormgenCodeParser.Parse(result.SuggestedFix);
        Assert.Equal(FormgenParseStatus.Valid, reparsed.Status);
        Assert.NotNull(reparsed.CondensedBlock);
    }

    [Fact]
    public void Parse_MissingClosingQuoteInNestedArgument_RepairsWithinDistance1()
    {
        // User test case: closing quote after TRADE #2: was removed
        var original = "SEPLIST(' ', IF(NOTBLANK(F340), 'TRADE #2: ', ' '), F340, F341, F339, F345, F479, IF(NOTBLANK(F351), 'TRADE #3: ', ' '), F351, F352, F350, F356, F480)";
        var mutated = "SEPLIST(' ', IF(NOTBLANK(F340), 'TRADE #2: , ' '), F340, F341, F339, F345, F479, IF(NOTBLANK(F351), 'TRADE #3: ', ' '), F351, F352, F350, F356, F480)";

        var result = FormgenCodeParser.Parse(mutated);

        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.NotNull(result.SuggestedFix);

        // Crucial requirement: Must NOT generate naked text like ' 'TRADE #2: or leave unclosed quote before comma!
        Assert.DoesNotContain("' 'TRADE", result.SuggestedFix);
        Assert.Contains("'TRADE #2: '", result.SuggestedFix);

        // Minimal edit: suggested fix must match the original formula (distance <= 1)
        Assert.True(LevenshteinDistance(result.SuggestedFix, original) <= 1, $"Expected distance <= 1, got '{result.SuggestedFix}'");

        var reparsed = FormgenCodeParser.Parse(result.SuggestedFix);
        Assert.Equal(FormgenParseStatus.Valid, reparsed.Status);
        Assert.NotNull(reparsed.CondensedBlock);
    }

    [Fact]
    public void Parse_SeplistMalformedSeparator_DefaultsToSingleSpace()
    {
        // User test case: SEPLIST( ', with unclosed quote and TRADE #2: missing opening quote
        var input = "SEPLIST( ', IF(NOTBLANK(F340),TRADE #2: ',' '), F340, F341, F339, F345, F479, IF(NOTBLANK(F351), 'TRADE #3: ', ' '), F351, F352, F350, F356, F480)";

        var result = FormgenCodeParser.Parse(input);

        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.NotNull(result.SuggestedFix);

        // Crucial requirement: SEPLIST separator must be an enclosed single space ' ', NOT an empty string ''!
        Assert.StartsWith("SEPLIST(' '", result.SuggestedFix);
        Assert.Contains("'TRADE #2: '", result.SuggestedFix);
        Assert.Contains("'TRADE #3: '", result.SuggestedFix);

        var reparsed = FormgenCodeParser.Parse(result.SuggestedFix);
        Assert.Equal(FormgenParseStatus.Valid, reparsed.Status);
        Assert.NotNull(reparsed.CondensedBlock);
    }

    [Fact]
    public void Parse_SingleAmpersandOutsideQuotes_RepairsToDoubleAmpersand()
    {
        var input = "IF(GapType = 'GAP' & F667 > 0, F14, ' ')";
        var result = FormgenCodeParser.Parse(input);

        Assert.Equal(FormgenParseStatus.MalformedSimilar, result.Status);
        Assert.NotNull(result.SuggestedFix);
        Assert.Contains("&&", result.SuggestedFix);
        Assert.Equal("IF(GapType = 'GAP' && F667 > 0, F14, ' ')", result.SuggestedFix);

        var reparsed = FormgenCodeParser.Parse(result.SuggestedFix);
        Assert.Equal(FormgenParseStatus.Valid, reparsed.Status);
    }

    [Fact]
    public void Parse_MissingOpeningParenthesisAfterIfOrKnownFunction_RepairsParenthesis()
    {
        // 1. Top-level IF missing '(' before variable
        var input1 = "IFWPAD = 'WEEKLY', 'WEEKLY', 'OTHER')";
        var res1 = FormgenCodeParser.Parse(input1);
        Assert.Equal(FormgenParseStatus.MalformedSimilar, res1.Status);
        Assert.Equal("IF(WPAD = 'WEEKLY', 'WEEKLY', 'OTHER')", res1.SuggestedFix);

        // 2. Nested IF missing '(' before variable
        var input2 = "IF(A = 1, 'YES', IFRebate = 'R2', 'R2', 'NO'))";
        var res2 = FormgenCodeParser.Parse(input2);
        Assert.Equal(FormgenParseStatus.MalformedSimilar, res2.Status);
        Assert.Equal("IF(A = 1, 'YES', IF(Rebate = 'R2', 'R2', 'NO'))", res2.SuggestedFix);

        // 3. SEPLIST missing '(' before quote
        var input3 = "SEPLIST' ', F1, F2)";
        var res3 = FormgenCodeParser.Parse(input3);
        Assert.Equal(FormgenParseStatus.MalformedSimilar, res3.Status);
        Assert.Equal("SEPLIST(' ', F1, F2)", res3.SuggestedFix);
    }

    [Fact]
    public void Parse_MissingComparisonOperator_RepairsWithDefaultAndOffersOptions()
    {
        // 1. Missing operator in string comparison
        var inputString = "IF(WPAD 'WEEKLY', 'WEEKLY', 'OTHER')";
        var resString = FormgenCodeParser.Parse(inputString);
        Assert.Equal(FormgenParseStatus.MalformedSimilar, resString.Status);
        Assert.Equal("IF(WPAD = 'WEEKLY', 'WEEKLY', 'OTHER')", resString.SuggestedFix);
        Assert.True(resString.HasMultipleSuggestedFixes);
        Assert.Contains(resString.SuggestedFixes, f => f.Contains("WPAD = 'WEEKLY'"));
        Assert.Contains(resString.SuggestedFixes, f => f.Contains("WPAD != 'WEEKLY'"));

        // 2. Missing operator in number comparison
        var inputNum = "IF(F678 1, 'MONTHLY', 'OTHER')";
        var resNum = FormgenCodeParser.Parse(inputNum);
        Assert.Equal(FormgenParseStatus.MalformedSimilar, resNum.Status);
        Assert.Equal("IF(F678 = 1, 'MONTHLY', 'OTHER')", resNum.SuggestedFix);
        Assert.True(resNum.HasMultipleSuggestedFixes);
        Assert.Contains(resNum.SuggestedFixes, f => f.Contains("F678 = 1"));
        Assert.Contains(resNum.SuggestedFixes, f => f.Contains("F678 > 1"));
    }

    [Fact]
    public void Parse_MissingTrailingArgumentInNestedIf_RepairsTypedDefault()
    {
        // 1. Numeric formula missing fallback before final ')'
        var inputNumeric = "IF(Rebate = 'R1', F955, IF(Rebate = 'R2', F964, ))";
        var resNumeric = FormgenCodeParser.Parse(inputNumeric);
        Assert.Equal(FormgenParseStatus.MalformedSimilar, resNumeric.Status);
        Assert.Equal("IF(Rebate = 'R1', F955, IF(Rebate = 'R2', F964, 0))", resNumeric.SuggestedFix);
        Assert.True(resNumeric.HasMultipleSuggestedFixes);

        // 2. String formula missing fallback before final ')'
        var inputString = "IF(WPAD = 'WEEKLY', 'WEEKLY', IF(WPAD = 'BI-WEEKLY', 'BI-WEEKLY', ))";
        var resString = FormgenCodeParser.Parse(inputString);
        Assert.Equal(FormgenParseStatus.MalformedSimilar, resString.Status);
        Assert.Equal("IF(WPAD = 'WEEKLY', 'WEEKLY', IF(WPAD = 'BI-WEEKLY', 'BI-WEEKLY', ' '))", resString.SuggestedFix);
        Assert.True(resString.HasMultipleSuggestedFixes);
    }

    [Theory]
    [InlineData("SEPLIST(' ', IF(NOTBLANK(F340), 'TRADE #2: ', ' '), F340, F341, F339, F345, F479, IF(NOTBLANK(F351), 'TRADE #3: ', ' '), F351, F352, F350, F356, F480)")]
    [InlineData("IF(WPAD = 'WEEKLY', 'WEEKLY', IF(WPAD = 'BI-WEEKLY', 'BI-WEEKLY', IF(WPAD = 'SEMI-MONTHLY', 'SEMI-MONTHLY', IF(F678 = 1, 'MONTHLY', IF(F678 = 3, 'QUARTERLY', IF(F678 = 6, 'BI-ANNUALLY', IF(F678 = 12, 'ANNUALLY', ' ')))))))")]
    [InlineData("IF(Rebate = 'Rebate #1', F945, IF(Rebate = 'Rebate #2', F946, IF(Rebate = 'Rebate #3', F947, IF(Rebate = 'Rebate #4', F948, IF(Rebate = 'Rebate #5', F949, IF(Rebate = 'Rebate #6', F950, IF(Rebate = 'Rebate #7', F951, IF(Rebate = 'Rebate #8', F952, IF(Rebate = 'Rebate #9', F953, IF(Rebate = 'Rebate #10', F954, ' '))))))))))")]
    [InlineData("IF(Rebate = 'Rebate #1', F955, IF(Rebate = 'Rebate #2', F956, IF(Rebate = 'Rebate #3', F957, IF(Rebate = 'Rebate #4', F958, IF(Rebate = 'Rebate #5', F959, IF(Rebate = 'Rebate #6', F960, IF(Rebate = 'Rebate #7', F961, IF(Rebate = 'Rebate #8', F962, IF(Rebate = 'Rebate #9', F963, IF(Rebate = 'Rebate #10', F964, 0))))))))))")]
    [InlineData("IF(AssignedTo = 'Ally Bank', 'Ally Bank', IF(AssignedTo = 'Ally Financial', 'Ally Financial', F226))")]
    [InlineData("SEPLIST(', ', SEPLIST(' ', F2975, F2976), SEPLIST(' ', F2977, F2978))")]
    [InlineData("IF(GapType = 'Debt Cancellation - E. Debt Cancellation Agreement Fee Paid to the Seller' && F667 > 0, F14, ' ')")]
    [InlineData("IF(GapType = 'GAP - C. Other Optional Insurance Paid to Insurance Company or Companies' && F667 > 0, F14, ' ')")]
    [InlineData("IF(F72 = 'N', 'NEW', 'USED')")]
    [InlineData("SEPLIST(', ', F93, F94)")]
    [InlineData("F3 + IF(F1404 = 5.00, F2230, 0) + IF(F1405 = 5.00, F2231, 0) - F2493 - F2008")]
    [InlineData("CASE(VMAKE, 'Acura - ACUR', 'ACUR', 'Audi - AUDI', 'AUDI', 'BMW - BMW', 'BMW', 'Ford - FORD', 'FORD', '')")]
    [InlineData("IF(Owner = 'Cobuyer', ' ', IF(Owner = 'Both', BUYERNAME, ' ')) + ' ' + IF(Owner = 'Cobuyer', COBUYERNAME, BUYERNAME)")]
    [InlineData("SEPLIST(' ', IF(WPAD = 'WEEKLY', 'WEEKLY', IF(F678 = 1, 'MONTHLY', ' ')), IF(F670 > 0, 'AND', ' '), IF(F670 > 0, TRIM(ROUND(F670, 0)), ' '), F671)")]
    [InlineData("IF(CONTAINS(F405, 'TIRE'), F1327, IF(CONTAINS(F1333, 'TIRE'), F1328, IF(CONTAINS(F1334, 'TIRE'), F1511, 0)))")]
    public void Parse_SingleCharacterDeletionMutation_ExaminesParserResilience(string originalFormula)
    {
        int validCount = 0;
        int malformedWithFixCount = 0;
        int malformedNoFixCount = 0;
        int unrecognizedCount = 0;
        int invalidFixCount = 0;
        int exactOrCloseFixCount = 0;

        var origParsed = FormgenCodeParser.Parse(originalFormula);
        string canonicalOriginal = origParsed.CondensedBlock?.GetCode() ?? NormalizeWhitespace(originalFormula);

        for (int i = 0; i < originalFormula.Length; i++)
        {
            char removedChar = originalFormula[i];
            string mutated = originalFormula.Remove(i, 1);

            var result = FormgenCodeParser.Parse(mutated);

            if (result.Status == FormgenParseStatus.Valid)
            {
                validCount++;
            }
            else if (result.Status == FormgenParseStatus.MalformedSimilar)
            {
                if (result.SuggestedFix != null)
                {
                    var fixCheck = FormgenCodeParser.Parse(result.SuggestedFix);
                    if (fixCheck.Status == FormgenParseStatus.Valid)
                    {
                        malformedWithFixCount++;
                        string canonicalFix = fixCheck.CondensedBlock?.GetCode() ?? result.SuggestedFix;
                        if (string.Equals(canonicalFix, canonicalOriginal, StringComparison.OrdinalIgnoreCase) ||
                            LevenshteinDistance(NormalizeWhitespace(result.SuggestedFix), NormalizeWhitespace(originalFormula)) <= 3)
                        {
                            exactOrCloseFixCount++;
                        }
                        else
                        {
                            _output?.WriteLine($"[DIVERGENT FIX idx {i} ('{removedChar}')]: mutated='{mutated}' => fix='{result.SuggestedFix}'");
                        }
                    }
                    else
                    {
                        invalidFixCount++;
                        _output?.WriteLine($"[INVALID FIX idx {i} ('{removedChar}')]: mutated='{mutated}' => fix='{result.SuggestedFix}'");
                    }
                }
                else
                {
                    malformedNoFixCount++;
                    _output?.WriteLine($"[NO FIX idx {i} ('{removedChar}')]: mutated='{mutated}'");
                }
            }
            else
            {
                unrecognizedCount++;
                _output?.WriteLine($"[UNRECOGNIZED idx {i} ('{removedChar}')]: mutated='{mutated}'");
            }
        }

        _output?.WriteLine($"\n=== Mutation Summary for formula length {originalFormula.Length} ===");
        _output?.WriteLine($"  Valid (benign deletions): {validCount}");
        _output?.WriteLine($"  Malformed w/ Valid Fix:   {malformedWithFixCount} (Exact/Close: {exactOrCloseFixCount})");
        _output?.WriteLine($"  Malformed without Fix:    {malformedNoFixCount}");
        _output?.WriteLine($"  Invalid Fix (Parser Bug): {invalidFixCount}");
        _output?.WriteLine($"  Unrecognized:             {unrecognizedCount}");
        _output?.WriteLine("==================================================\n");

        // Rule 1: A suggested fix MUST NEVER produce broken syntax!
        Assert.Equal(0, invalidFixCount);
    }

    [Fact(Skip = "Local corpus benchmark")]
    public void Parse_AllExtractedCorpusFormulas_DoesNotThrowAndReportsStats()
    {
        string corpusPath = @"C:\Users\heart\.gemini\antigravity\brain\9d011b5c-9313-4918-a920-5558cb74ed91\scratch\complex_formulas.txt";
        if (!File.Exists(corpusPath)) return;

        var lines = File.ReadAllLines(corpusPath);
        int total = lines.Length;
        int valid = 0;
        int malformed = 0;
        int unrecognized = 0;
        var unrecognizedSamples = new List<string>();
        var malformedSamples = new List<string>();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var res = FormgenCodeParser.Parse(line);
            if (res.Status == FormgenParseStatus.Valid)
            {
                valid++;
            }
            else if (res.Status == FormgenParseStatus.MalformedSimilar)
            {
                malformed++;
                if (malformedSamples.Count < 25)
                {
                    malformedSamples.Add($"{line}  ==>  [Fix: {res.SuggestedFix}] (Diag: {res.DiagnosticMessage})");
                }
            }
            else
            {
                unrecognized++;
                if (unrecognizedSamples.Count < 30)
                {
                    unrecognizedSamples.Add($"{line} => {res.DiagnosticMessage}");
                }
            }
        }
        sw.Stop();

        _output?.WriteLine($"=== Real-World Corpus Parse Results ({total} formulas in {sw.ElapsedMilliseconds} ms) ===");
        _output?.WriteLine($"  Valid:        {valid} ({valid * 100.0 / total:F1}%)");
        _output?.WriteLine($"  Malformed:    {malformed} ({malformed * 100.0 / total:F1}%)");
        _output?.WriteLine($"  Unrecognized: {unrecognized} ({unrecognized * 100.0 / total:F1}%)");
        _output?.WriteLine("============================================================================");

        if (malformedSamples.Count > 0)
        {
            _output?.WriteLine("\nSample Malformed Expressions:");
            foreach (var m in malformedSamples)
            {
                _output?.WriteLine($"  * {m}");
            }
        }

        if (unrecognizedSamples.Count > 0)
        {
            _output?.WriteLine("\nSample Unrecognized Expressions:");
            foreach (var s in unrecognizedSamples)
            {
                _output?.WriteLine($"  * {s}");
            }
        }

        Assert.True(valid > 0);
    }


    private static string NormalizeWhitespace(string s)
    {
        return System.Text.RegularExpressions.Regex.Replace(s.Replace("\"", "'"), @"\s+", " ").Trim();
    }

    private static int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return t?.Length ?? 0;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int[,] d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;

        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[s.Length, t.Length];
    }
}
