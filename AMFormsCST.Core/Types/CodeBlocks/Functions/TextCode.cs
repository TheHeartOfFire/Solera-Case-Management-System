namespace AMFormsCST.Core.Types.CodeBlocks.Functions
{
    public class TextCode : CodeBase
    {
        public TextCode()
        {
            Name = "Text";
            Prefix = "TEXT";
            Description = "Convert a Date or Numeric field to a Text field";
            AddInput("Value");
        }

        public override string DescribeInlineSummary() =>
            $"convert ({DescribeInputInline(0)}) to text";

        public override string GetPseudoCode(int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * 2);
            return $"{indent}CONVERT TO TEXT ({DescribeInputInline(0)}):\n" +
                   $"{indent}  ➜ Formats numeric or date value as a left-justified text string";
        }
    }
}
