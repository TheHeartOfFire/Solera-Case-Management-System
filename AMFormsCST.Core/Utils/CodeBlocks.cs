using AMFormsCST.Core.Interfaces.CodeBlocks;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.CodeBlocks.Formulae;
using AMFormsCST.Core.Types.CodeBlocks.Functions;
using AMFormsCST.Core.Interfaces;

namespace AMFormsCST.Core.Utils;
public class CodeBlocks : ICodeBlocks
{
    private readonly ILogService? _logger;

    public IList<ICodeBase> CustomBlocks { get; set; } = [];
    private readonly IList<ICodeBase> _defaultBlocks = [];

    public CodeBlocks(ILogService? logger = null)
    {
        _logger = logger;
        _logger?.LogInfo("Initializing CodeBlocks.");

        AddDefaultBlock(new CityStateZIPCode());
        AddDefaultBlock(new DateConversionCode());
        AddDefaultBlock(new DayAndSuffixCode());
        AddDefaultBlock(new MonthNameCode());
        AddDefaultBlock(new NumToTextCode());
        AddDefaultBlock(new SeplistNumber());
        AddDefaultBlock(new DmvCalculationCode());
        AddDefaultBlock(new FuelDropdownDefaultCode());
        AddDefaultBlock(new CaseCode());
        AddDefaultBlock(new IfCode());
        AddDefaultBlock(new SeplistCode());
        AddDefaultBlock(new ContainsCode());
        AddDefaultBlock(new DayCode());
        AddDefaultBlock(new MonthCode());
        AddDefaultBlock(new YearCode());
        AddDefaultBlock(new RoundCode());
        AddDefaultBlock(new TextCode());
        AddDefaultBlock(new OperatorCode());

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
}
