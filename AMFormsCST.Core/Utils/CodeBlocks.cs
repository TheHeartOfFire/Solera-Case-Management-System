using AMFormsCST.Core.Interfaces.CodeBlocks;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.CodeBlocks.Formulae;
using AMFormsCST.Core.Types.CodeBlocks.Functions;
using AMFormsCST.Core.Interfaces;

namespace AMFormsCST.Core.Utils;
public class CodeBlocks : ICodeBlocks
{
    private readonly ILogService? _logger;
    private readonly bool _enablePersistence;
    private readonly string? _persistenceFilePath;

    public IList<ICodeBase> CustomBlocks { get; set; } = [];
    private readonly IList<ICodeBase> _defaultBlocks = [];

    public CodeBlocks(ILogService? logger = null, bool enablePersistence = false, string? persistenceFilePath = null)
    {
        _logger = logger;
        _enablePersistence = enablePersistence;
        _persistenceFilePath = persistenceFilePath;
        _logger?.LogInfo("Initializing CodeBlocks.");

        // Composite Formulae / Snippets
        AddDefaultBlock(new CityStateZIPCode());
        AddDefaultBlock(new DateConversionCode());
        AddDefaultBlock(new DayAndSuffixCode());
        AddDefaultBlock(new MonthNameCode());
        AddDefaultBlock(new NumToTextCode());
        AddDefaultBlock(new SeplistNumber());
        AddDefaultBlock(new LeadingZeroCode());
        AddDefaultBlock(new DmvCalculationCode());
        AddDefaultBlock(new FuelDropdownDefaultCode());

        // Core Logic & Conditional Functions
        AddDefaultBlock(new IfCode());
        AddDefaultBlock(new FormgenFunctionCode("XIF", "Checkbox X-If (XIF)", ["Condition"]));
        AddDefaultBlock(new FormgenFunctionCode("XXIF", "Checkbox Double-X (XXIF)", ["Condition"]));
        AddDefaultBlock(new CaseCode());
        AddDefaultBlock(new ExtendableFormgenFunctionCode(
            "CASELTE",
            "Case Less/Equal (CASELTE)",
            ["Comparison", "Threshold", "Result", "Threshold", "Result", "Default"],
            ["Threshold", "Result"],
            keepLastAsDefault: true));
        AddDefaultBlock(new ExtendableFormgenFunctionCode(
            "MATCHRANGE",
            "Match Range (MATCHRANGE)",
            ["Comparison", "LowerBound", "UpperBound", "Result", "Default"],
            ["LowerBound", "UpperBound", "Result"],
            keepLastAsDefault: true));
        AddDefaultBlock(new FormgenFunctionCode("NOT", "Logical NOT", ["Condition"]));
        AddDefaultBlock(new ExtendableFormgenFunctionCode("AND", "Logical AND Function", ["Condition", "Condition"], ["Condition"]));
        AddDefaultBlock(new ExtendableFormgenFunctionCode("OR", "Logical OR Function", ["Condition", "Condition"], ["Condition"]));
        AddDefaultBlock(new FormgenFunctionCode("BLANK", "Is Blank (BLANK)", ["FieldOrPrompt"]));
        AddDefaultBlock(new FormgenFunctionCode("NOTBLANK", "Is Not Blank (NOTBLANK)", ["FieldOrPrompt"]));
        AddDefaultBlock(new ContainsCode());

        // Text & Formatting Functions
        AddDefaultBlock(new SeplistCode());
        AddDefaultBlock(new TextCode());
        AddDefaultBlock(new FormgenFunctionCode("STRIP", "Strip Text (STRIP)", ["SourceText", "TextToRemove"]));
        AddDefaultBlock(new FormgenFunctionCode("REPLACE", "Replace Text (REPLACE)", ["SourceText", "UnwantedText", "ReplacementText"]));
        AddDefaultBlock(new FormgenFunctionCode("LEN", "Character Length (LEN)", ["Value"]));
        AddDefaultBlock(new FormgenFunctionCode("TRIM", "Trim Spaces (TRIM)", ["Text"]));
        AddDefaultBlock(new FormgenFunctionCode("LTRIM", "Left Trim (LTRIM)", ["Text"]));
        AddDefaultBlock(new FormgenFunctionCode("RTRIM", "Right Trim (RTRIM)", ["Text"]));

        // Date Functions
        AddDefaultBlock(new FormgenFunctionCode("TODAY", "Current Date (TODAY)", [], usesParentheses: true));
        AddDefaultBlock(new MonthCode());
        AddDefaultBlock(new DayCode());
        AddDefaultBlock(new YearCode());
        AddDefaultBlock(new FormgenFunctionCode("DATE", "Text to Date (DATE)", ["DateText"]));
        AddDefaultBlock(new FormgenFunctionCode("ADDDAYS", "Add Days (ADDDAYS)", ["Date", "Days"]));
        AddDefaultBlock(new FormgenFunctionCode("ADDWEEKS", "Add Weeks (ADDWEEKS)", ["Date", "Weeks"]));
        AddDefaultBlock(new FormgenFunctionCode("ADDMONTHS", "Add Months (ADDMONTHS)", ["Date", "Months"]));
        AddDefaultBlock(new FormgenFunctionCode("ADDYEARS", "Add Years (ADDYEARS)", ["Date", "Years"]));

        // Math & Conversion Functions + Operators
        AddDefaultBlock(new OperatorCode());
        AddDefaultBlock(new RoundCode());
        AddDefaultBlock(new FormgenFunctionCode("NUM", "Convert to Number (NUM)", ["TextOrDate"]));
        AddDefaultBlock(new FormgenFunctionCode("FLOOR", "Round Down (FLOOR)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("CEILING", "Round Up (CEILING)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("ABS", "Absolute Value (ABS)", ["Number"]));
        AddDefaultBlock(new ExtendableFormgenFunctionCode("MAX", "Maximum (MAX)", ["Number", "Number"], ["Number"]));
        AddDefaultBlock(new ExtendableFormgenFunctionCode("MIN", "Minimum (MIN)", ["Number", "Number"], ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("SQRT", "Square Root (SQRT)", ["PositiveNumber"]));
        AddDefaultBlock(new FormgenFunctionCode("LOG", "Natural Log (LOG)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("LOG10", "Base-10 Log (LOG10)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("RANDOM", "Random 0–1 (RANDOM)", [], usesParentheses: true));
        AddDefaultBlock(new FormgenFunctionCode("PI", "Pi Constant (PI)", [], usesParentheses: false));
        AddDefaultBlock(new FormgenFunctionCode("SIN", "Sine (SIN)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("COS", "Cosine (COS)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("TAN", "Tangent (TAN)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("ASIN", "Arcsine (ASIN)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("ACOS", "Arccosine (ACOS)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("ATAN", "Arctangent (ATAN)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("SINH", "Hyperbolic Sine (SINH)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("COSH", "Hyperbolic Cosine (COSH)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("TANH", "Hyperbolic Tangent (TANH)", ["Number"]));
        AddDefaultBlock(new FormgenFunctionCode("RAD", "Degrees to Radians (RAD)", ["Degrees"]));
        AddDefaultBlock(new FormgenFunctionCode("DEG", "Radians to Degrees (DEG)", ["Radians"]));

        if (_enablePersistence)
        {
            LoadCustomBlocks();
        }

        _logger?.LogInfo($"Default blocks initialized: {_defaultBlocks.Count} blocks.");
    }

    private void AddDefaultBlock(ICodeBase block)
    {
        _defaultBlocks.Add(block);
        _logger?.LogDebug($"Default block added: {block.Name ?? block.GetType().Name}");
    }

    public IReadOnlyCollection<ICodeBase> GetBlocks()
    {
        List<ICodeBase> allBlocks = [.. _defaultBlocks, .. CustomBlocks];
        _logger?.LogDebug($"GetBlocks called. Returning {allBlocks.Count} blocks.");
        return allBlocks;
    }

    public void LoadCustomBlocks()
    {
        if (!_enablePersistence && string.IsNullOrWhiteSpace(_persistenceFilePath)) return;

        var dtos = IO.LoadCustomSnippets(_persistenceFilePath);
        CustomBlocks.Clear();
        var prototypes = _defaultBlocks.ToList();
        foreach (var dto in dtos)
        {
            var custom = CustomSnippetCode.FromDto(dto, prototypes);
            if (custom != null)
            {
                CustomBlocks.Add(custom);
                prototypes.Add(custom);
            }
        }
    }

    public void SaveCustomBlocks()
    {
        if (!_enablePersistence && string.IsNullOrWhiteSpace(_persistenceFilePath)) return;

        var dtos = CustomBlocks
            .OfType<CustomSnippetCode>()
            .Select(c => c.ToDto())
            .ToList();

        IO.SaveCustomSnippets(dtos, _persistenceFilePath);
    }
}


