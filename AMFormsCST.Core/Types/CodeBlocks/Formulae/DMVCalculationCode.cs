using AMFormsCST.Core.Types.CodeBlocks.Functions;

namespace AMFormsCST.Core.Types.CodeBlocks.Formulae
{
    public class DmvCalculationCode : CodeBase
    {
        public DmvCalculationCode()
        {
            Name = "DMV Calculation";
            Description = "The sum of taxable costs in the deal. " +
                          "Selling Price + Taxed Accessory Total Price + Taxed Fees Total Amount + Taxed GAP Amount + Taxed Service Contracts Total Amount - Non-Taxed Rebates Total Amount - Total Non-taxable Trade Allowance Amount";
        }

        public override CodeBase GetStructure()
        {
            var step1 = new OperatorCode("+").SetInputValue(0, "F3").SetInputValue(1, "F1953");
            var step2 = new OperatorCode("+").SetInputValue(0, step1).SetInputValue(1, "F1979");
            var step3 = new OperatorCode("+").SetInputValue(0, step2).SetInputValue(1, "F1997");
            var step4 = new OperatorCode("+").SetInputValue(0, step3).SetInputValue(1, "F1983");
            var step5 = new OperatorCode("-").SetInputValue(0, step4).SetInputValue(1, "F2008");
            var step6 = new OperatorCode("-").SetInputValue(0, step5).SetInputValue(1, "F2493");
            return step6;
        }
    }
}
