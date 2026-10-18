using AMFormsCST.Core.Types.CodeBlocks.Functions;

namespace AMFormsCST.Core.Types.CodeBlocks.Formulae
{
    public class NumToTextCode : CodeBase
    {
        public NumToTextCode()
        {
            Name = "Number to Text";
            Description = "Converts a number to text. " +
                          "This is needed because just using the NUM() function produces strange results in FormGen. " +
                          "Can also be used to left justify numbers";
            AddInput("Number");
            AddInput("Decimal Places");
        }

        public override CodeBase? GetStructure()
        {
            if (HasNoInputs()) return null;

            return new TextCode()
                .SetInputValue(0, new RoundCode()
                    .SetInputFrom(0, Inputs[0])
                    .SetInputFrom(1, Inputs[1]));
        }
    }
}
