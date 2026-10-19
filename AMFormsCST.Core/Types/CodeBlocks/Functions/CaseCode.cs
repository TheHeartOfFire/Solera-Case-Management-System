using AMFormsCST.Core.Interfaces.FormgenUtils;

namespace AMFormsCST.Core.Types.CodeBlocks.Functions
{
    public class CaseCode : CodeBase, IExtendableCode
    {
        public int DefaultArgCount { get; }
        public int ArgIncrement { get; }

        public CaseCode()
        {
            Name = "Case Statement";
            Prefix = "CASE";
            Description = "Compare the comparison value to the case values, " +
                          "return the corresponding result of any matches, " +
                          "otherwise return the default value. " +
                          "Identical to multiple nested If Statements.";
            AddInput("Comparison");
            AddInput("Case");
            AddInput("Result");
            AddInput("Case");
            AddInput("Result");
            AddInput("Default");

            DefaultArgCount = 6;
            ArgIncrement = 2;
        }


        public CodeBase AddExtraInputs(int count)
        {
            for (var i = 0; i < count; i++)
            { 
                AddInput(InputCount() - 1, "Case");
                AddInput(InputCount() - 1, "Result");
            }

            return this;
        }

        public CodeBase RemoveExtraInputs(int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (InputCount() <= DefaultArgCount) return this;

                RemoveInput(InputCount() - 2);
                RemoveInput(InputCount() - 2);
            }

            return this;
        }

        public override bool IsMultiLinePseudoCode => true;

        public override string DescribeInlineSummary() =>
            $"CASE match on ({DescribeInputInline(0)})";

        public override string GetPseudoCode(int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * 2);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"{indent}CHECK VALUE OF ({DescribeInputInline(0)}):");

            int branchNum = 1;
            for (int i = 1; i < Inputs.Count - 1; i += 2)
            {
                sb.AppendLine($"{indent}  • Branch {branchNum} — WHEN it equals {DescribeInputInline(i)}:");
                sb.AppendLine(FormatInputBranch(i + 1, indentLevel + 2));
                branchNum++;
            }

            if (Inputs.Count >= 2)
            {
                sb.AppendLine($"{indent}  • DEFAULT — OTHERWISE (if none of the cases above match):");
                sb.Append(FormatInputBranch(Inputs.Count - 1, indentLevel + 2));
            }

            return sb.ToString().TrimEnd();
        }
    }
}
