using System.Text;

namespace AMFormsCST.Core.Types.CodeBlocks.Functions
{
    public class IfCode : CodeBase
    {
        public IfCode()
        {
            Name = "If Statement";
            Prefix = "IF";
            Description = "If the condition is true, return the true value, otherwise return the false value.";

            AddInput("Condition");
            AddInput("ResultIfTrue");
            AddInput("ResultIfFalse");
        }

        public override bool IsMultiLinePseudoCode => true;

        public override string DescribeInlineSummary() =>
            $"IF ({DescribeInputInline(0)}) THEN {DescribeInputInline(1)} ELSE {DescribeInputInline(2)}";

        public override string GetPseudoCode(int indentLevel = 0)
        {
            return BuildIfChainPseudoCode(indentLevel, isElseIf: false);
        }

        private string BuildIfChainPseudoCode(int indentLevel, bool isElseIf)
        {
            string indent = new(' ', indentLevel * 2);
            var sb = new StringBuilder();
            var keyword = isElseIf ? "ELSE IF" : "IF";

            sb.AppendLine($"{indent}{keyword} ({DescribeInputInline(0)}) IS TRUE THEN:");
            sb.AppendLine(FormatInputBranch(1, indentLevel + 1));

            if (Inputs.Count > 2 && Inputs[2].Value is CodeBase falseChild)
            {
                var unwrappedFalse = falseChild.GetStructure() ?? falseChild;
                if (unwrappedFalse is IfCode nestedIf)
                {
                    sb.Append(nestedIf.BuildIfChainPseudoCode(indentLevel, isElseIf: true));
                    return sb.ToString().TrimEnd();
                }
            }

            sb.AppendLine($"{indent}OTHERWISE (if condition is FALSE):");
            sb.Append(FormatInputBranch(2, indentLevel + 1));
            return sb.ToString().TrimEnd();
        }
    }
}
