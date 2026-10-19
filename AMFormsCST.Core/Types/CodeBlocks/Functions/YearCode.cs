namespace AMFormsCST.Core.Types.CodeBlocks.Functions
{
    public class YearCode : CodeBase
    {
        public YearCode()
        {
            Name = "Year";
            Prefix = "YEAR";
            Description = "Extract the numeric year from a date";
            AddInput("Date Field");
        }

        public override string DescribeInlineSummary() =>
            $"4-digit year of {DescribeInputInline(0)}";

        public override string GetPseudoCode(int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * 2);
            return $"{indent}EXTRACT 4-DIGIT YEAR FROM ({DescribeInputInline(0)}):\n" +
                   $"{indent}  ➜ Returns the full 4-digit calendar year (e.g. 2026)";
        }
    }
}
