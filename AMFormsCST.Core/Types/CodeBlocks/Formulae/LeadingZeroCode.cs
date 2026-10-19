using AMFormsCST.Core.Types.CodeBlocks.Functions;

namespace AMFormsCST.Core.Types.CodeBlocks.Formulae;

public class LeadingZeroCode : CodeBase
{
    public LeadingZeroCode()
    {
        Name = "Leading Zero (2-Digit)";
        Description = "Adds a leading 0 to a 1-digit number (0–9 becomes 00–09) using IF(Number < 10, SEPLIST('', '0', Number), Number).";
        AddInput("Number");
    }

    public override CodeBase? GetStructure()
    {
        if (HasNoInputs()) return null;

        return new IfCode()
            .SetInputValue(0, new OperatorCode("<")
                .SetInputFrom(0, Inputs[0])
                .SetInputValue(1, "10"))
            .SetInputValue(1, new SeplistCode()
                .SetInputValue(0, "''")
                .SetInputValue(1, "'0'")
                .SetInputFrom(2, Inputs[0]))
            .SetInputFrom(2, Inputs[0]);
    }
}

