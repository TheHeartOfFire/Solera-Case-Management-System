using AMFormsCST.Core.Types.CodeBlocks.Functions;

namespace AMFormsCST.Core.Types.CodeBlocks.Formulae
{
    public class DayAndSuffixCode : CodeBase
    {
        public DayAndSuffixCode()
        {
            Name = "Day and Suffix";
            Description = "Returns the day of the month with the suffix. 11 = 11th, 12 = 12th, 13 = 13th, 21 = 21st, 22 = 22nd, etc.";
            AddInput("Numeric Day");
        }

        public override CodeBase? GetStructure()
        {
            if (HasNoInputs()) return null;

            var numToTextStructure = new TextCode()
                .SetInputValue(0, new RoundCode()
                    .SetInputFrom(0, Inputs[0])
                    .SetInputValue(1, "0"));

            var mod10 = new OperatorCode("%")
                .SetInputFrom(0, Inputs[0])
                .SetInputValue(1, "10");

            var innerCase = new CaseCode()
                .AddExtraInputs(1)
                .SetInputValue(0, mod10)
                .SetInputValue(1, "1")
                .SetInputValue(2, "'st'")
                .SetInputValue(3, "2")
                .SetInputValue(4, "'nd'")
                .SetInputValue(5, "3")
                .SetInputValue(6, "'rd'")
                .SetInputValue(7, "'th'");

            var outerCase = new CaseCode()
                .AddExtraInputs(1)
                .SetInputFrom(0, Inputs[0])
                .SetInputValue(1, "11")
                .SetInputValue(2, "'th'")
                .SetInputValue(3, "12")
                .SetInputValue(4, "'th'")
                .SetInputValue(5, "13")
                .SetInputValue(6, "'th'")
                .SetInputValue(7, innerCase);

            return new OperatorCode("+")
                .SetInputValue(0, numToTextStructure)
                .SetInputValue(1, outerCase);
        }
    }
}
