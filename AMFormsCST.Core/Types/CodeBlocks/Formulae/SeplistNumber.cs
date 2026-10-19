using AMFormsCST.Core.Types.CodeBlocks.Functions;

namespace AMFormsCST.Core.Types.CodeBlocks.Formulae
{
    public class SeplistNumber : CodeBase
    {
        public SeplistNumber()
        {

            Name = "Seplist Number";
            Description = "Allow Numeric fields to be used in seplist.";
            AddInput("Numeric Field");
            AddInput("Decimal Places");
        }

        public override CodeBase? GetStructure()
        {
            if (HasNoInputs()) return null;

            return new IfCode()
                .SetInputValue(0, new OperatorCode("!=")
                    .SetInputFrom(0, Inputs[0])
                    .SetInputValue(1, "0"))
                .SetInputValue(1, new RoundCode()
                    .SetInputFrom(0, Inputs[0])
                    .SetInputFrom(1, Inputs[1]))
                .SetInputValue(2, "''");
        }
    }
}
