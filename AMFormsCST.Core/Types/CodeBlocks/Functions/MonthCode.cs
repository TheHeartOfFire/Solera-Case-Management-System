namespace AMFormsCST.Core.Types.CodeBlocks.Functions
{
    public class MonthCode : CodeBase
    {
        public MonthCode()
        {
            Name = "Month";
            Prefix = "MONTH";
            Description = "Extract the numeric month from a date";
            AddInput("Date Field");
        }

        public override string DescribeInlineSummary() =>
            $"month number (1–12) of {DescribeInputInline(0)}";

        public override string GetPseudoCode(int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * 2);
            return $"{indent}EXTRACT NUMERIC MONTH (1–12) FROM ({DescribeInputInline(0)}):\n" +
                   $"{indent}  ➜ Returns 1 for January, 2 for February, ... 12 for December";
        }
    }
}
