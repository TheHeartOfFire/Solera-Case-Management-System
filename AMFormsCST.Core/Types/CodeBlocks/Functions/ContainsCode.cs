namespace AMFormsCST.Core.Types.CodeBlocks.Functions
{
    public class ContainsCode : CodeBase
    {
        public ContainsCode()
        {
            Name = "Contains";
            Prefix = "CONTAINS";
            Description = "Returns TRUE if TextB appears anywhere within TextA";
            AddInput("TextA");
            AddInput("TextB");
        }

        public override string DescribeInlineSummary() =>
            $"{DescribeInputInline(0)} contains {DescribeInputInline(1)} (case-sensitive)";

        public override string GetPseudoCode(int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * 2);
            return $"{indent}CHECK IF ({DescribeInputInline(0)}) CONTAINS SUBSTRING ({DescribeInputInline(1)}) [Case-Sensitive]:\n" +
                   $"{indent}  • IF found anywhere inside the text ➜ Return TRUE\n" +
                   $"{indent}  • OTHERWISE ➜ Return FALSE";
        }
    }
}
