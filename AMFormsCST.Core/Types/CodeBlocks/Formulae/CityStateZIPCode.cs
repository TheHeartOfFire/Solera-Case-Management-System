using AMFormsCST.Core.Types.CodeBlocks.Functions;

namespace AMFormsCST.Core.Types.CodeBlocks.Formulae
{
    public class CityStateZIPCode : CodeBase
    {
        public CityStateZIPCode()
        {
            Name = "City, State ZIP";
            Description = "Nicely format a city, state, and ZIP code.";
            AddInput("City");
            AddInput("State");
            AddInput("ZIP");
        }

        public override CodeBase? GetStructure()
        {
            if (HasNoInputs()) return null;

            return new SeplistCode()
                .SetInputValue(0, "\' \'")
                .SetInputValue(1, new SeplistCode()
                    .SetInputValue(0, "\', \'")
                    .SetInputFrom(1, Inputs[0])
                    .SetInputFrom(2, Inputs[1]))
                .SetInputFrom(2, Inputs[2]);
        }
    }
}
