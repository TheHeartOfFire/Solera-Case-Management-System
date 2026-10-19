namespace AMFormsCST.Core.Types.CodeBlocks.Functions
{
    public class RoundCode : CodeBase
    {
        public RoundCode()
        {
            Name = "Round";
            Prefix = "ROUND";
            Description = "Round a number to a specified number of decimal places. Uses nearest rounding. i.e. 0.5 rounds to 1.0, 0.4 rounds to 0.0";
            AddInput("Number");
            AddInput("Decimal Places");
        }

        public override string DescribeInlineSummary() =>
            $"round ({DescribeInputInline(0)}) to {DescribeInputInline(1)} decimal place(s)";

        public override string GetPseudoCode(int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * 2);
            return $"{indent}ROUND ({DescribeInputInline(0)}) TO {DescribeInputInline(1)} DECIMAL PLACE(S):\n" +
                   $"{indent}  • If next digit >= 5 ➜ Round up (away from zero)\n" +
                   $"{indent}  • If next digit <= 4 ➜ Round down (toward zero)";
        }
    }
}
