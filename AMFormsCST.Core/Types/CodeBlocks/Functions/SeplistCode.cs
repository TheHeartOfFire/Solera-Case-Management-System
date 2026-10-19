using AMFormsCST.Core.Interfaces.FormgenUtils;

namespace AMFormsCST.Core.Types.CodeBlocks.Functions
{
    public class SeplistCode : CodeBase, IExtendableCode
    {
        public int DefaultArgCount { get; }
        public int ArgIncrement { get; }

        public SeplistCode()
        {
            Name = "Seplist";
            Prefix = "SEPLIST";
            Description = "Separate a list of items with the given separator.";
            AddInput("Separator");
            AddInput("Item");
            AddInput("Item");

            DefaultArgCount = 3;
            ArgIncrement = 1;

        }
        public CodeBase AddExtraInputs(int count)
        {
            for (var i = 0; i < count; i++)
                AddInput("Item");

            return this;
        }

        public CodeBase RemoveExtraInputs(int count)
        {
            for (var i = 0; i < count; i++)
            {
                if (InputCount() <= DefaultArgCount) return this;

                RemoveInput(InputCount() - 1);
            }
            return this;
        }

        public override bool IsMultiLinePseudoCode => true;

        public override string DescribeInlineSummary()
        {
            var items = Enumerable.Range(1, Math.Max(0, Inputs.Count - 1)).Select(DescribeInputInline);
            return $"join [{string.Join(", ", items)}] separated by {DescribeInputInline(0)}";
        }

        public override string GetPseudoCode(int indentLevel = 0)
        {
            string indent = new(' ', indentLevel * 2);
            var sb = new System.Text.StringBuilder();
            var sep = DescribeInputInline(0);
            sb.AppendLine($"{indent}JOIN NON-BLANK ITEMS USING SEPARATOR {sep}:");

            for (int i = 1; i < Inputs.Count; i++)
            {
                if (Inputs[i].Value is CodeBase childBlock && (childBlock.GetStructure() ?? childBlock).IsMultiLinePseudoCode)
                {
                    sb.AppendLine($"{indent}  • Item {i} (included if non-blank, skipped if blank):");
                    sb.AppendLine((childBlock.GetStructure() ?? childBlock).GetPseudoCode(indentLevel + 2));
                }
                else
                {
                    sb.AppendLine($"{indent}  • Item {i}: IF ({DescribeInputInline(i)}) is not blank ➜ include it; OTHERWISE skip it");
                }
            }

            sb.Append($"{indent}  ➜ Combine all included items above with {sep} between each item");
            return sb.ToString().TrimEnd();
        }
    }
}
