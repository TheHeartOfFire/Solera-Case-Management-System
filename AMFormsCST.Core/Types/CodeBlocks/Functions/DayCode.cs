namespace AMFormsCST.Core.Types.CodeBlocks.Functions
{
    public class DayCode : CodeBase
    {
        public DayCode()
        {
            Name = "Day";
            Prefix = "DAY";
            Description = "Extract the numeric day from a date";
            AddInput("Date Field");
        }

        public override string DescribeInlineSummary() =>
            $"day of month (1–31) of {DescribeInputInline(0)}";

        public override string GetPseudoCode(int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * 2);
            return $"{indent}EXTRACT NUMERIC DAY OF MONTH (1–31) FROM ({DescribeInputInline(0)}):\n" +
                   $"{indent}  ➜ Returns the calendar day number (1 through 31)";
        }
    }
}
