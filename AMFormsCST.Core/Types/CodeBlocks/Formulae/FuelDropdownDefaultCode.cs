using AMFormsCST.Core.Types.CodeBlocks.Functions;

namespace AMFormsCST.Core.Types.CodeBlocks.Formulae
{
    public class FuelDropdownDefaultCode : CodeBase
    {
        public FuelDropdownDefaultCode()
        {
            Name = "Fuel Dropdown Default";
            Description = "This code goes in the Init screen. " +
                          "This serves as the default value for a dropdown named \"FUELTYPE\" with \"Gas\", \"Diesel\", \"Propane\", \"Hybrid\", \"Electric\", and \"Other\" as its options.";
        }

        public override CodeBase GetStructure()
        {
            var ifChain = new IfCode()
               .SetInputValue(0, new ContainsCode()
                   .SetInputValue(0, "F776")
                   .SetInputValue(1, "'GAS'"))
               .SetInputValue(1, "'Gas'")
               .SetInputValue(2, new IfCode()
                   .SetInputValue(0, new ContainsCode()
                       .SetInputValue(0, "F776")
                       .SetInputValue(1, "'DIESEL'"))
                   .SetInputValue(1, "'Diesel'")
                   .SetInputValue(2, new IfCode()
                       .SetInputValue(0, new ContainsCode()
                           .SetInputValue(0, "F776")
                           .SetInputValue(1, "'PROPANE'"))
                       .SetInputValue(1, "'Propane'")
                       .SetInputValue(2, new IfCode()
                           .SetInputValue(0, new ContainsCode()
                               .SetInputValue(0, "F776")
                               .SetInputValue(1, "'HYBRID'"))
                           .SetInputValue(1, "'Hybrid'")
                           .SetInputValue(2, new IfCode()
                               .SetInputValue(0, new ContainsCode()
                                   .SetInputValue(0, "F776")
                                   .SetInputValue(1, "'ELECTRIC'"))
                               .SetInputValue(1, "'Electric'")
                               .SetInputValue(2, new IfCode()
                                   .SetInputValue(0, new ContainsCode()
                                       .SetInputValue(0, "F776")
                                       .SetInputValue(1, "'OTHER'"))
                                   .SetInputValue(1, "'Other'")
                                   .SetInputValue(2, "' '"))))));

            return new OperatorCode("=")
                .SetInputValue(0, "FUELTYPE")
                .SetInputValue(1, ifChain);
        }
    }
}
