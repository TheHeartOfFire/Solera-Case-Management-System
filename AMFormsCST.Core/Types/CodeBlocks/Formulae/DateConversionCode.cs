using AMFormsCST.Core.Types.CodeBlocks.Functions;

namespace AMFormsCST.Core.Types.CodeBlocks.Formulae
{
    public class DateConversionCode : CodeBase
    {
        public DateConversionCode()
        {
            Name = "Date Conversion";
            Description = "Converts a date in the format of MM/DD/YYYY to a date in the format of MM/DD/YY.";
            AddInput("Date Field");
        }

        public override CodeBase? GetStructure()
        {
            if (HasNoInputs()) return null;

            return new SeplistCode()
                .AddExtraInputs(1)
                .SetInputValue(0, "'/'")
                .SetInputValue(1, new MonthCode()
                    .SetInputFrom(0, Inputs[0]))
                .SetInputValue(2, new DayCode()
                    .SetInputFrom(0, Inputs[0]))
                .SetInputValue(3, new OperatorCode("%")
                    .SetInputValue(0, new YearCode()
                        .SetInputFrom(0, Inputs[0]))
                    .SetInputValue(1, "100"));
        }
    }
}
