using AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;
using AMFormsCST.Core.Interfaces.CodeBlocks;
using Moq;
using System.Collections.ObjectModel;
using Xunit;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using AMFormsCST.Core.Interfaces;

namespace AMFormsCST.Test.Desktop.ViewModels.Pages.Tools.Pages;
public class CodeSnippetsViewModelTests
{
    [Fact]
    public void Constructor_InitializesCodeSnippetsAndSelectsFirst()
    {
        // Arrange
        var codeBaseMock1 = new Mock<ICodeBase>();
        codeBaseMock1.SetupGet(cb => cb.Name).Returns("A");
        codeBaseMock1.Setup(cb => cb.GetCode()).Returns("codeA");
        codeBaseMock1.SetupGet(cb => cb.Inputs).Returns([]);

        var codeBaseMock2 = new Mock<ICodeBase>();
        codeBaseMock2.SetupGet(cb => cb.Name).Returns("B");
        codeBaseMock2.Setup(cb => cb.GetCode()).Returns("codeB");
        codeBaseMock2.SetupGet(cb => cb.Inputs).Returns([]);

        var codeBlocksMock = new Mock<ICodeBlocks>();
        codeBlocksMock.Setup(cb => cb.GetBlocks()).Returns(new[] { codeBaseMock2.Object, codeBaseMock1.Object }); // Unordered

        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocksMock.Object);

        // Act
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);

        // Assert
        Assert.Equal(2, vm.CodeSnippets.Count);
        Assert.Equal("A", vm.CodeSnippets[0].Name); // Ordered by Name
        Assert.Equal("B", vm.CodeSnippets[1].Name);
        Assert.Equal(vm.CodeSnippets[0], vm.SelectedCodeSnippet);
        Assert.True(vm.SelectedCodeSnippet.IsSelected);
    }

    [Fact]
    public void SelectCodeSnippetCommand_ChangesSelectionAndIsSelected()
    {
        // Arrange
        var codeBaseMock1 = new Mock<ICodeBase>();
        codeBaseMock1.SetupGet(cb => cb.Name).Returns("A");
        codeBaseMock1.Setup(cb => cb.GetCode()).Returns("codeA");
        codeBaseMock1.SetupGet(cb => cb.Inputs).Returns([]);

        var codeBaseMock2 = new Mock<ICodeBase>();
        codeBaseMock2.SetupGet(cb => cb.Name).Returns("B");
        codeBaseMock2.Setup(cb => cb.GetCode()).Returns("codeB");
        codeBaseMock2.SetupGet(cb => cb.Inputs).Returns([]);

        var codeBlocksMock = new Mock<ICodeBlocks>();
        codeBlocksMock.Setup(cb => cb.GetBlocks()).Returns(new[] { codeBaseMock1.Object, codeBaseMock2.Object });

        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocksMock.Object);

        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        var first = vm.CodeSnippets[0];
        var second = vm.CodeSnippets[1];

        // Act
        vm.SelectCodeSnippetCommand.Execute(second);

        // Assert
        Assert.Equal(second, vm.SelectedCodeSnippet);
        Assert.True(second.IsSelected);
        Assert.False(first.IsSelected);
    }

    [Fact]
    public void SelectCodeSnippetCommand_DoesNothingIfNullOrAlreadySelected()
    {
        // Arrange
        var codeBaseMock = new Mock<ICodeBase>();
        codeBaseMock.SetupGet(cb => cb.Name).Returns("A");
        codeBaseMock.Setup(cb => cb.GetCode()).Returns("codeA");
        codeBaseMock.SetupGet(cb => cb.Inputs).Returns([]);

        var codeBlocksMock = new Mock<ICodeBlocks>();
        codeBlocksMock.Setup(cb => cb.GetBlocks()).Returns(new[] { codeBaseMock.Object });

        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocksMock.Object);

        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        var selected = vm.SelectedCodeSnippet;

        // Act
        vm.SelectCodeSnippetCommand.Execute(null);
        vm.SelectCodeSnippetCommand.Execute(selected);

        // Assert
        Assert.Equal(selected, vm.SelectedCodeSnippet);
        Assert.True(selected.IsSelected);
    }

    [Fact]
    public void CopyOutput_CopiesSelectedCodeSnippetOutput()
    {
        // Arrange
        var codeBaseMock = new Mock<ICodeBase>();
        codeBaseMock.SetupGet(cb => cb.Name).Returns("A");
        codeBaseMock.Setup(cb => cb.GetCode()).Returns("output");
        codeBaseMock.SetupGet(cb => cb.Inputs).Returns([]);

        var codeBlocksMock = new Mock<ICodeBlocks>();
        codeBlocksMock.Setup(cb => cb.GetBlocks()).Returns(new[] { codeBaseMock.Object });

        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocksMock.Object);

        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        var selected = vm.SelectedCodeSnippet;
        selected.Output = "output";

        // Act & Assert
        var exception = Record.Exception(() => vm.CopyOutput());
        Assert.Null(exception); // Ensure no exception is thrown
    }

    [Fact]
    public void CopyOutput_DoesNothingIfNoSelectedOrEmptyOutput()
    {
        // Arrange
        var codeBaseMock = new Mock<ICodeBase>();
        codeBaseMock.SetupGet(cb => cb.Name).Returns("A");
        codeBaseMock.Setup(cb => cb.GetCode()).Returns("");
        codeBaseMock.SetupGet(cb => cb.Inputs).Returns([]);

        var codeBlocksMock = new Mock<ICodeBlocks>();
        codeBlocksMock.Setup(cb => cb.GetBlocks()).Returns(new[] { codeBaseMock.Object });

        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocksMock.Object);

        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        var selected = vm.SelectedCodeSnippet;
        selected.Output = "";

        // Act & Assert
        var exception = Record.Exception(() => vm.CopyOutput());
        Assert.Null(exception); // Ensure no exception is thrown
    }

    [Fact]
    public void DockBlockIntoSlot_NestsChildAndUpdatesActiveOutputCode()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        var ifBlock = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.IfCode(), new System.Windows.Point(0, 0));
        var containsBlock = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.ContainsCode(), new System.Windows.Point(100, 100));
        containsBlock.Inputs[0].BindableValue = "F776";
        containsBlock.Inputs[1].BindableValue = "'GAS'";
        ifBlock.Inputs[1].BindableValue = "'Gas'";
        ifBlock.Inputs[2].BindableValue = "'Other'";

        // Act
        var docked = vm.DockBlockIntoSlot(containsBlock, ifBlock.Inputs[0]);

        // Assert
        Assert.True(docked);
        Assert.Single(vm.Nodes); // containsBlock removed from root canvas and nested inside ifBlock
        Assert.Same(containsBlock, ifBlock.Inputs[0].ConnectedNode);
        Assert.Same(ifBlock.Inputs[0], containsBlock.ParentSlot);
        Assert.Equal("IF( CONTAINS( F776, 'GAS' ), 'Gas', 'Other' )", vm.ActiveOutputCode);
    }

    [Fact]
    public void DockBlockIntoSlot_WhenOccupied_EjectsExistingBlockToCanvas()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        var textBlock = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.TextCode(), new System.Windows.Point(0, 0));
        var firstChild = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.MonthCode(), new System.Windows.Point(50, 50));
        var secondChild = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.YearCode(), new System.Windows.Point(100, 100));

        vm.DockBlockIntoSlot(firstChild, textBlock.Inputs[0]);
        Assert.Equal(2, vm.Nodes.Count); // textBlock and secondChild on canvas

        // Act: Dock secondChild into the same slot
        vm.DockBlockIntoSlot(secondChild, textBlock.Inputs[0]);

        // Assert: firstChild was ejected back to root Nodes, and secondChild is now docked
        Assert.Equal(2, vm.Nodes.Count);
        Assert.Contains(firstChild, vm.Nodes);
        Assert.DoesNotContain(secondChild, vm.Nodes);
        Assert.Null(firstChild.ParentSlot);
        Assert.Same(secondChild, textBlock.Inputs[0].ConnectedNode);
    }

    [Fact]
    public void DockBlockIntoSlot_PreventsCircularSelfNesting()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        var parentBlock = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.IfCode(), new System.Windows.Point(0, 0));
        var childBlock = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.TextCode(), new System.Windows.Point(50, 50));
        vm.DockBlockIntoSlot(childBlock, parentBlock.Inputs[0]);

        // Act: Try to dock parentBlock into childBlock's slot
        var result = vm.DockBlockIntoSlot(parentBlock, childBlock.Inputs[0]);

        // Assert
        Assert.False(result);
        Assert.Null(childBlock.Inputs[0].ConnectedNode);
    }

    [Fact]
    public void DeconstructBlock_ExpandsFormulaIntoPrimitiveStructureAndPreservesInputs()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        var formulaNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Formulae.NumToTextCode(), new System.Windows.Point(0, 0));
        formulaNode.Inputs[0].BindableValue = "F500";
        formulaNode.Inputs[1].BindableValue = "2";
        Assert.True(formulaNode.CanDeconstruct);

        // Act
        vm.DeconstructBlock(formulaNode);

        // Assert
        var expandedRoot = Assert.IsType<CodeSnippetViewModel>(Assert.Single(vm.Nodes));
        Assert.IsType<AMFormsCST.Core.Types.CodeBlocks.Functions.TextCode>(expandedRoot.CodeBase);
        Assert.IsType<CodeSnippetViewModel>(expandedRoot.Inputs[0].ConnectedNode);
        Assert.Equal("TEXT( ROUND( F500, 2 ))", vm.ActiveOutputCode);
    }

    [Fact]
    public void ExtendableBlock_AddAndRemoveExtraInput_EjectsNestedBlockOnRemove()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        var seplistNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.SeplistCode(), new System.Windows.Point(0, 0));
        Assert.Equal(3, seplistNode.Inputs.Count);

        // Act 1: Add extra slot and dock a child block into it
        seplistNode.AddExtraInput();
        Assert.Equal(4, seplistNode.Inputs.Count);
        var childNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.DayCode(), new System.Windows.Point(50, 50));
        vm.DockBlockIntoSlot(childNode, seplistNode.Inputs[3]);
        Assert.Single(vm.Nodes);

        // Act 2: Remove the extra slot
        seplistNode.RemoveExtraInput();

        // Assert: Slot count back to 3 and childNode was ejected back onto root canvas
        Assert.Equal(3, seplistNode.Inputs.Count);
        Assert.Equal(2, vm.Nodes.Count);
        Assert.Contains(childNode, vm.Nodes);
    }

    [Fact]
    public void UserInputViewModel_StringQuotedType_AutomaticallySingleQuotesValue()
    {
        // Arrange
        var userInput = new UserInputViewModel
        {
            SelectedType = UserInputViewModel.TypeStringQuoted,
            Value = "GAS"
        };

        // Act & Assert
        Assert.Equal("'GAS'", userInput.GetCode());

        // Already quoted string should not be double-quoted
        userInput.Value = "'GAS'";
        Assert.Equal("'GAS'", userInput.GetCode());
    }

    [Fact]
    public void SaveCustomSnippet_CreatesReusableSnippetExposingEmptySlots()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        var roundNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.RoundCode(), new System.Windows.Point(0, 0));
        // Fill Decimal Places (slot 1) with "2", leave Number (slot 0) empty so it becomes an exposed input
        roundNode.Inputs[1].BindableValue = "2";
        vm.NewCustomSnippetName = "Round Two Decimals";

        // Act
        vm.SaveCustomSnippet();

        // Assert
        Assert.Single(vm.CustomSnippets);
        var saved = vm.CustomSnippets[0];
        Assert.Equal("Round Two Decimals", saved.Name);
        Assert.Single(saved.Inputs); // Only the 1 unfilled slot is exposed!
        saved.SetInputValue(0, "F999");
        Assert.Equal("ROUND( F999, 2 )", saved.GetCode());
    }

    [Fact]
    public void FormulaTester_AutoDetectsVariablesAndLiveEvaluatesSubstitutedValues()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        var cityStateZipNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Formulae.CityStateZIPCode(), new System.Windows.Point(0, 0));
        cityStateZipNode.Inputs[0].BindableValue = "F100";
        cityStateZipNode.Inputs[1].BindableValue = "F101";
        cityStateZipNode.Inputs[2].BindableValue = "F102";

        // Act: Substitute F100, F101, F102 in TestVariables
        Assert.Equal(3, vm.TestVariables.Count);
        vm.TestVariables.First(v => v.Name == "F100").SampleValue = "Dallas";
        vm.TestVariables.First(v => v.Name == "F101").SampleValue = "TX";
        vm.TestVariables.First(v => v.Name == "F102").SampleValue = "75201";

        // Assert
        Assert.Equal("Dallas, TX 75201", vm.TestEvaluatedResult);
    }

    [Fact]
    public void FormulaTester_DateConversionAndDayAndSuffix_EvaluatesCorrectly()
    {
        // Arrange
        var dateBlock = new AMFormsCST.Core.Types.CodeBlocks.Formulae.DateConversionCode();
        dateBlock.SetInputValue(0, "F200");
        var dateEval = dateBlock.Evaluate(new Dictionary<string, string> { ["F200"] = "12/25/2026" });
        Assert.True(dateEval.IsSuccess);
        Assert.Equal("12/25/26", dateEval.Value);

        var daySuffixBlock = new AMFormsCST.Core.Types.CodeBlocks.Formulae.DayAndSuffixCode();
        daySuffixBlock.SetInputValue(0, "F201");
        var eval22 = daySuffixBlock.Evaluate(new Dictionary<string, string> { ["F201"] = "22" });
        Assert.Equal("22nd", eval22.Value);

        var eval13 = daySuffixBlock.Evaluate(new Dictionary<string, string> { ["F201"] = "13" });
        Assert.Equal("13th", eval13.Value);
    }

    [Fact]
    public void TeachingDiagnostics_FlagsUnquotedSeparatorAndEmptySlotsWithExplanations()
    {
        // Arrange
        var seplist = new AMFormsCST.Core.Types.CodeBlocks.Functions.SeplistCode();
        seplist.SetInputValue(0, ","); // Unquoted literal comma instead of ','
        seplist.SetInputValue(1, "F100");
        // Slot 2 left empty

        // Act
        var diagnostics = seplist.Validate();

        // Assert
        Assert.Contains(diagnostics, d => d.InputIndex == 0 && d.Message.Contains("single quotes"));
        Assert.Contains(diagnostics, d => d.InputIndex == 2 && d.Message.Contains("empty"));
    }

    [Fact]
    public void ReconstructBlock_AfterDeconstructingFormula_CollapsesBackToSingleBlockWithSameInputs()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        var cityStateZipNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Formulae.CityStateZIPCode(), new System.Windows.Point(10, 20));
        cityStateZipNode.Inputs[0].BindableValue = "F100";

        // Act 1: Deconstruct into nested SEPLIST blocks
        vm.DeconstructBlock(cityStateZipNode);
        var expandedRoot = Assert.IsType<CodeSnippetViewModel>(Assert.Single(vm.Nodes));
        Assert.True(expandedRoot.CanReconstruct);
        Assert.IsType<CodeSnippetViewModel>(expandedRoot.Inputs[1].ConnectedNode);

        // Edit State slot while deconstructed
        var innerSeplist = (CodeSnippetViewModel)expandedRoot.Inputs[1].ConnectedNode!;
        innerSeplist.Inputs[2].BindableValue = "F101";

        // Act 2: Reconstruct back down to a single block
        vm.ReconstructBlock(expandedRoot);

        // Assert
        var reconstructedRoot = Assert.IsType<CodeSnippetViewModel>(Assert.Single(vm.Nodes));
        Assert.Equal("City, State ZIP", reconstructedRoot.Title);
        Assert.True(reconstructedRoot.CanDeconstruct);
        Assert.False(reconstructedRoot.CanReconstruct);
        Assert.Equal(3, reconstructedRoot.Inputs.Count);
        Assert.Equal("F100", reconstructedRoot.Inputs[0].BindableValue);
        Assert.Equal("F101", reconstructedRoot.Inputs[1].BindableValue);
        Assert.Equal(string.Empty, reconstructedRoot.Inputs[2].BindableValue);
        Assert.Equal("SEPLIST( ' ', SEPLIST( ', ', F100, F101 ), ZIP )", reconstructedRoot.GetCode());
    }

    [Fact]
    public void ReconstructBlock_OnGeneralNestedFunctions_CollapsesIntoSingleBlockAndPreservesCode()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        var textNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.TextCode(), new System.Windows.Point(0, 0));
        var roundNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.RoundCode(), new System.Windows.Point(50, 0));
        roundNode.Inputs[0].BindableValue = "F500";
        roundNode.Inputs[1].BindableValue = "2";

        vm.DockBlockIntoSlot(roundNode, textNode.Inputs[0]);
        Assert.True(textNode.CanReconstruct);

        // Act: Reconstruct the general nested function tree into a single block
        vm.ReconstructBlock(textNode);

        // Assert
        var singleBlock = Assert.IsType<CodeSnippetViewModel>(Assert.Single(vm.Nodes));
        Assert.True(singleBlock.CanDeconstruct);
        Assert.False(singleBlock.CanReconstruct);
        Assert.Single(singleBlock.Inputs); // F500 is exposed; literal 2 is folded inside
        Assert.Equal("F500", singleBlock.Inputs[0].BindableValue);
        Assert.Equal("TEXT( ROUND( F500, 2 ))", singleBlock.GetCode());
    }

    [Fact]
    public void FormgenReferenceCatalog_LoadsMasterFieldsAndVehicleMerchandisingStatus()
    {
        // F776 (VH-FUEL-TYPE) is in both fields.json and Vehicle Merchandising Fields.xlsx (Vehicle Only = Y)
        var f776 = AMFormsCST.Core.Utils.FormgenReferenceCatalog.TryGetField("F776");
        Assert.NotNull(f776);
        Assert.Equal("VH-FUEL-TYPE", f776!.VariableName);
        Assert.True(f776.IsVehicleMerchandisingSafe);
        Assert.Contains("VM Safe", f776.VmStatusBadge);
        Assert.Contains("Available in Vehicle Merchandising", f776.VerboseTooltip);

        // F3 (DL-FINAL-PRICE) is in fields.json (Numeric 7.2) but not in the VM-safe subset
        var f3 = AMFormsCST.Core.Utils.FormgenReferenceCatalog.TryGetField("F3");
        Assert.NotNull(f3);
        Assert.True(f3!.IsNumeric);
        Assert.False(f3.IsVehicleMerchandisingSafe);
        Assert.Contains("Deal Only", f3.VmStatusBadge);

        // F202 (DL-MSRP-AMT) is explicitly marked Vehicle Only = N in Vehicle Merchandising Fields.xlsx
        var f202 = AMFormsCST.Core.Utils.FormgenReferenceCatalog.TryGetField("F202");
        Assert.NotNull(f202);
        Assert.True(f202!.IsExplicitlyExcludedFromVm);
        Assert.Contains("Vehicle Only = N", f202.VmStatusBadge);
    }

    [Fact]
    public void ExpandedWarnings_DetectsUnknownFieldsNumericInSeplistAndMonthFieldQuirk()
    {
        // 1. Numeric F-field (F3) directly inside SEPLIST warns to use Seplist Number / TEXT(ROUND())
        var seplist = new AMFormsCST.Core.Types.CodeBlocks.Functions.SeplistCode();
        seplist.SetInputValue(0, "' '");
        seplist.SetInputValue(1, "F3");
        seplist.SetInputValue(2, "F99999"); // Unknown F-field

        var seplistDiagnostics = seplist.Validate();
        Assert.Contains(seplistDiagnostics, d => d.InputIndex == 1 && d.Message.Contains("numeric") && d.Message.Contains("Seplist Number"));
        Assert.Contains(seplistDiagnostics, d => d.InputIndex == 2 && d.Message.Contains("master FormGen field list"));

        // 2. MONTH(F44) warns about FormGen date quirk from Formgen Formulas.xlsm
        var month = new AMFormsCST.Core.Types.CodeBlocks.Functions.MonthCode();
        month.SetInputValue(0, "F44");
        var monthDiagnostics = month.Validate();
        Assert.Contains(monthDiagnostics, d => d.InputIndex == 0 && d.Message.Contains("TODAY()") && d.Message.Contains("DATE(F44)"));

        // 3. F202 warns about explicit exclusion from Vehicle Merchandising
        var text = new AMFormsCST.Core.Types.CodeBlocks.Functions.TextCode();
        text.SetInputValue(0, "F202");
        var textDiagnostics = text.Validate();
        Assert.Contains(textDiagnostics, d => d.InputIndex == 0 && d.Message.Contains("Vehicle Only = N"));
    }

    [Fact]
    public void VerboseExplanationsAndSocketBadges_PopulateForFunctionsOperatorsAndFFields()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        // Act: Add Operator and Seplist blocks
        var opNode = vm.AddBlockToCanvas(
            new AMFormsCST.Core.Types.CodeBlocks.Functions.OperatorCode("%"),
            new System.Windows.Point(0, 0));
        var seplistNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.SeplistCode(), new System.Windows.Point(100, 0));
        seplistNode.Inputs[1].BindableValue = "F776";

        // Assert verbose explanations and F-field badge
        Assert.Contains("Modulus Operator", opNode.DetailedExplanation);
        Assert.Contains("Separated List", seplistNode.DetailedExplanation);
        Assert.True(seplistNode.Inputs[1].HasFieldInfo);
        Assert.True(seplistNode.Inputs[1].FieldInfo!.IsVehicleMerchandisingSafe);
        Assert.Contains("Fuel Type [VM Safe", seplistNode.Inputs[1].FieldBadgeText);
        Assert.Contains("VH-FUEL-TYPE", seplistNode.Inputs[1].SocketTooltip);
    }

    [Fact]
    public void CondensedFunctions_DoNotProduceDuplicateWarningsInActiveDiagnostics()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        // Act: Add condensed City, State ZIP snippet with all 3 slots empty
        var cityStateZipNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Formulae.CityStateZIPCode(), new System.Windows.Point(0, 0));

        // Assert: Exactly 3 warnings (1 for City, 1 for State, 1 for ZIP) and no duplicate "on Seplist" warnings
        Assert.Equal(3, vm.ActiveDiagnostics.Count);
        Assert.DoesNotContain(vm.ActiveDiagnostics, d => d.Message.Contains("on Seplist"));
    }

    [Fact]
    public void ClearCanvas_ClearsWarningsAndFormulaTesterState()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Formulae.CityStateZIPCode(), new System.Windows.Point(0, 0));
        Assert.NotEmpty(vm.ActiveDiagnostics);
        Assert.NotEmpty(vm.TestVariables);
        Assert.False(string.IsNullOrEmpty(vm.TestEvaluatedResult));

        // Act
        vm.ClearCanvas();

        // Assert
        Assert.Empty(vm.Nodes);
        Assert.Empty(vm.ActiveDiagnostics);
        Assert.Empty(vm.TestVariables);
        Assert.Empty(vm.GeneratedCodeSegments);
        Assert.Equal(string.Empty, vm.TestEvaluatedResult);
        Assert.Equal(string.Empty, vm.TestEvaluationExplanation);
        Assert.Equal(string.Empty, vm.ActiveOutputCode);
    }

    [Fact]
    public void FunctionCategories_OrganizesAvailableFunctionsIntoCollapsibleCategories()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());

        // Act
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);

        // Assert
        Assert.Equal(6, vm.FunctionCategories.Count);
        Assert.Contains(vm.FunctionCategories, c => c.Name == "Fields & Variables");
        Assert.Contains(vm.FunctionCategories, c => c.Name == "Logic & Conditionals");
        Assert.Contains(vm.FunctionCategories, c => c.Name == "Text & Formatting");
        Assert.Contains(vm.FunctionCategories, c => c.Name == "Date & Time");
        Assert.Contains(vm.FunctionCategories, c => c.Name == "Math & Numbers");
        Assert.Contains(vm.FunctionCategories, c => c.Name == "Trigonometry");
        Assert.Equal(vm.AvailableBlocks.Count, vm.FunctionCategories.Sum(c => c.Functions.Count));
    }

    [Fact]
    public void GeneratedCodeSegments_ColorCoordinatesTestVariablesAndIgnoresQuotedLiteralsAndFunctionNames()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        var cityStateZipNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Formulae.CityStateZIPCode(), new System.Windows.Point(0, 0));
        cityStateZipNode.Inputs[0].BindableValue = "F100";
        cityStateZipNode.Inputs[1].BindableValue = "F101";
        cityStateZipNode.Inputs[2].BindableValue = "F102";

        // Assert: 3 test variables with distinct colors, and 3 matching variable segments in GeneratedCodeSegments
        Assert.Equal(3, vm.TestVariables.Count);
        Assert.NotEqual(vm.TestVariables[0].AccentColorHex, vm.TestVariables[1].AccentColorHex);
        Assert.NotEqual(vm.TestVariables[1].AccentColorHex, vm.TestVariables[2].AccentColorHex);

        var varSegments = vm.GeneratedCodeSegments.Where(s => s.IsVariable).ToList();
        Assert.Equal(3, varSegments.Count);
        Assert.Same(vm.TestVariables[0], varSegments[0].Variable);
        Assert.Same(vm.TestVariables[1], varSegments[1].Variable);
        Assert.Same(vm.TestVariables[2], varSegments[2].Variable);

        // Full concatenated text of segments equals ActiveOutputCode
        Assert.Equal(vm.ActiveOutputCode, string.Concat(vm.GeneratedCodeSegments.Select(s => s.Text)));
    }

    [Fact]
    public void StructuredExplainerAndToggle_UpdatesCleanlyOnSelectionAndToggle()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        Assert.True(vm.IsExplainerExpanded);

        // Act 1: Toggle explainer collapsed & expanded
        vm.ToggleExplainerCommand.Execute(null);
        Assert.False(vm.IsExplainerExpanded);
        vm.ToggleExplainerCommand.Execute(null);
        Assert.True(vm.IsExplainerExpanded);

        // Act 2: Select a function block and verify structured explainer fields
        var seplistNode = vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.SeplistCode(), new System.Windows.Point(0, 0));
        vm.OnNodeSelected(seplistNode);

        // Assert
        Assert.Equal("SEPLIST", vm.ExplainerCategoryBadge);
        Assert.Contains("Separated List", vm.ExplainerTitle);
        Assert.True(vm.HasExplainerSyntax);
        Assert.True(vm.HasExplainerExample);
    }

    [Fact]
    public void RecenterCanvasAndAutoRecenter_ResetsViewportOnManualRecenterClearAndAddingToEmptyCanvas()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(new AMFormsCST.Core.Utils.CodeBlocks());
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);

        int recenterCount = 0;
        vm.RecenterRequested += () => recenterCount++;

        // Simulate getting lost on the canvas (panning far away and zooming)
        vm.ViewportLocation = new System.Windows.Point(5000, -3200);
        vm.ViewportZoom = 2.5;

        // Act 1: Manual Recenter command
        vm.RecenterCanvasCommand.Execute(null);
        Assert.Equal(1, recenterCount);
        Assert.Equal(1.0, vm.ViewportZoom);
        Assert.NotEqual(new System.Windows.Point(5000, -3200), vm.ViewportLocation);

        // Act 2: Pan away again, then ClearCanvas -> should auto-recenter to (0, 0)
        vm.ViewportLocation = new System.Windows.Point(-4000, 2800);
        vm.ViewportZoom = 0.5;
        vm.ClearCanvas();
        Assert.Equal(2, recenterCount);
        Assert.Equal(new System.Windows.Point(0, 0), vm.ViewportLocation);
        Assert.Equal(1.0, vm.ViewportZoom);

        // Act 3: Pan away on empty canvas, then add a block -> should auto-recenter around the new block
        vm.ViewportLocation = new System.Windows.Point(8000, 8000);
        vm.ViewportZoom = 1.8;
        vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.TextCode(), new System.Windows.Point(300, 200));
        Assert.Equal(3, recenterCount);
        Assert.Equal(1.0, vm.ViewportZoom);
        Assert.Equal(new System.Windows.Point(300 - 80, 200 - 60), vm.ViewportLocation);

        // Act 4: Adding a second block when canvas is NOT empty does not unexpectedly jump viewport
        vm.ViewportLocation = new System.Windows.Point(100, 100);
        vm.AddBlockToCanvas(new AMFormsCST.Core.Types.CodeBlocks.Functions.RoundCode(), new System.Windows.Point(500, 500));
        Assert.Equal(3, recenterCount);
        Assert.Equal(new System.Windows.Point(100, 100), vm.ViewportLocation);
    }

    [Fact]
    public void CustomSnippets_PersistAcrossApplicationResetAndSupportDeletion()
    {
        var tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"CustomSnippetsTest_{System.Guid.NewGuid():N}.json");
        try
        {
            // Arrange Session 1: Create a composite formula on the canvas and save it as a custom snippet
            var codeBlocksSession1 = new AMFormsCST.Core.Utils.CodeBlocks(enablePersistence: true, persistenceFilePath: tempFile);
            var supportToolSession1 = new Mock<ISupportTool>();
            supportToolSession1.SetupGet(st => st.CodeBlocks).Returns(codeBlocksSession1);
            var vmSession1 = new CodeSnippetsViewModel(supportToolSession1.Object);
            vmSession1.ClearCanvas();

            var opNode = vmSession1.AddBlockToCanvas(
                new AMFormsCST.Core.Types.CodeBlocks.Functions.OperatorCode("+"),
                new System.Windows.Point(0, 0));
            vmSession1.InsertBlockTemplateIntoSlot(
                new AMFormsCST.Core.Types.CodeBlocks.Functions.TextCode(),
                opNode.Inputs[0]);
            // Leave textNode.Inputs[0] empty so it becomes an exposed slot, and fill opNode.Inputs[1] with "' USD'"
            opNode.Inputs[1].BindableValue = "' USD'";

            vmSession1.NewCustomSnippetName = "Format USD";
            vmSession1.NewCustomSnippetDescription = "Formats a numeric field with USD suffix";
            vmSession1.SaveCustomSnippetCommand.Execute(null);

            Assert.Single(vmSession1.CustomSnippets);
            Assert.True(System.IO.File.Exists(tempFile));

            // Act Session 2: Simulate application restart by creating a brand new CodeBlocks and CodeSnippetsViewModel reading from the same file
            var codeBlocksSession2 = new AMFormsCST.Core.Utils.CodeBlocks(enablePersistence: true, persistenceFilePath: tempFile);
            var supportToolSession2 = new Mock<ISupportTool>();
            supportToolSession2.SetupGet(st => st.CodeBlocks).Returns(codeBlocksSession2);
            var vmSession2 = new CodeSnippetsViewModel(supportToolSession2.Object);

            // Assert Session 2: Custom snippet persisted across reset, can be added to canvas, filled, and deconstructed!
            Assert.Single(vmSession2.CustomSnippets);
            var loadedSnippet = vmSession2.CustomSnippets[0];
            Assert.Equal("Format USD", loadedSnippet.Name);
            Assert.Equal("Formats a numeric field with USD suffix", loadedSnippet.Description);

            vmSession2.ClearCanvas();
            var snippetInstance = vmSession2.AddBlockToCanvas(loadedSnippet, new System.Windows.Point(0, 0));
            Assert.Single(snippetInstance.Inputs);
            snippetInstance.Inputs[0].BindableValue = "F200";
            Assert.Equal("TEXT( F200 ) + ' USD'", vmSession2.ActiveOutputCode);

            // Verify deconstruct works on the reloaded persisted snippet
            Assert.True(snippetInstance.CanDeconstruct);
            snippetInstance.DeconstructCommand.Execute(null);
            Assert.Equal("TEXT( F200 ) + ' USD'", vmSession2.ActiveOutputCode);

            // Act Session 2 Delete: Delete the custom snippet and verify persistence after another reset
            vmSession2.DeleteCustomSnippetCommand.Execute(loadedSnippet);
            Assert.Empty(vmSession2.CustomSnippets);

            AMFormsCST.Core.Interfaces.Utils.ICodeBlocks codeBlocksSession3 = new AMFormsCST.Core.Utils.CodeBlocks(enablePersistence: true, persistenceFilePath: tempFile);
            Assert.Empty(codeBlocksSession3.CustomBlocks);
        }
        finally
        {
            if (System.IO.File.Exists(tempFile))
            {
                System.IO.File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void PasteFormulaText_ValidExpression_SucceedsQuietlyAndCreatesSnippet()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        var codeBlocks = new AMFormsCST.Core.Utils.CodeBlocks(enablePersistence: false);
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocks);
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        // Act
        vm.PasteFormulaText("IF(F72 = 'N', 'NEW', 'USED')");

        // Assert
        Assert.False(vm.IsPasteAssistantVisible);
        Assert.Single(vm.Nodes);
        Assert.Contains("IS TRUE THEN:", vm.ExplainerPseudoCode);
        Assert.Contains("OTHERWISE", vm.ExplainerPseudoCode);
    }

    [Fact]
    public void PasteFormulaText_MalformedExpression_OpensAssistantWithDiagnosticAndFix()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        var codeBlocks = new AMFormsCST.Core.Utils.CodeBlocks(enablePersistence: false);
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocks);
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        // Act
        vm.PasteFormulaText("IF(F72 = \"N\", \"NEW\", \"USED\")");

        // Assert
        Assert.True(vm.IsPasteAssistantVisible);
        Assert.Equal(AMFormsCST.Core.Utils.FormgenParseStatus.MalformedSimilar, vm.PasteParseStatus);
        Assert.Contains("double quotes", vm.PasteDiagnosticMessage.ToLowerInvariant());
        Assert.Equal("IF( F72 = 'N', 'NEW', 'USED' )", vm.PasteSuggestedFix);

        // Apply fix and commit
        vm.ApplyPasteSuggestionCommand.Execute(null);
        Assert.Equal(AMFormsCST.Core.Utils.FormgenParseStatus.Valid, vm.PasteParseStatus);
        Assert.True(vm.CommitParsedPasteCommand.CanExecute(null));

        vm.CommitParsedPasteCommand.Execute(null);
        Assert.False(vm.IsPasteAssistantVisible);
        Assert.Single(vm.Nodes);
        Assert.Equal("IF( F72 = 'N', 'NEW', 'USED' )", vm.ActiveOutputCode);
    }

    [Fact]
    public void PasteFormulaText_UnrecognizedExpression_FailsLoudlyAndAlertsWhenEdited()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        var codeBlocks = new AMFormsCST.Core.Utils.CodeBlocks(enablePersistence: false);
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocks);
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        // Act
        vm.PasteFormulaText("Arbitrary text that is definitely not a function");

        // Assert
        Assert.True(vm.IsPasteAssistantVisible);
        Assert.Equal(AMFormsCST.Core.Utils.FormgenParseStatus.Unrecognized, vm.PasteParseStatus);
        Assert.False(vm.IsPasteRecognizedAlert);

        // User edits text in the assistant
        vm.PasteEditorText = "SEPLIST(', ', F1, F2)";
        Assert.Equal(AMFormsCST.Core.Utils.FormgenParseStatus.Valid, vm.PasteParseStatus);
        Assert.True(vm.IsPasteRecognizedAlert);

        vm.CommitParsedPasteCommand.Execute(null);
        Assert.False(vm.IsPasteAssistantVisible);
        Assert.Single(vm.Nodes);
        Assert.Equal("SEPLIST( ', ', F1, F2 )", vm.ActiveOutputCode);
    }

    [Fact]
    public void CodePathTree_WhenIfFormulaPlaced_GeneratesTreeOutcomesAndActivePath()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        var codeBlocks = new AMFormsCST.Core.Utils.CodeBlocks(enablePersistence: false);
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocks);
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();

        // Act - Paste IF formula
        vm.PasteFormulaText("IF(F72 = 'N', 'NEW', 'USED')");

        // Assert
        Assert.True(vm.HasCodePathTree);
        Assert.True(vm.HasCodePathForks);
        Assert.Equal(2, vm.CodePathOutcomeCount);
        Assert.Equal(1, vm.CodePathForkCount);
        Assert.Single(vm.CodePathTreeNodes);
        Assert.Equal(2, vm.CodePathOutcomes.Count);

        // Path 1 (True: NEW) & Path 2 (False: USED)
        Assert.Equal("'NEW'", vm.CodePathOutcomes[0].OutcomeCode);
        Assert.Equal("'USED'", vm.CodePathOutcomes[1].OutcomeCode);

        // Check active path with initial test variables
        var f72Var = vm.TestVariables.FirstOrDefault(v => v.Name == "F72");
        Assert.NotNull(f72Var);
        f72Var.SampleValue = "N"; // Triggers True branch
        Assert.True(vm.CodePathOutcomes[0].IsActive);
        Assert.False(vm.CodePathOutcomes[1].IsActive);

        f72Var.SampleValue = "U"; // Triggers False branch
        Assert.False(vm.CodePathOutcomes[0].IsActive);
        Assert.True(vm.CodePathOutcomes[1].IsActive);
    }

    [Fact]
    public void CodePathTree_ApplyPathToTesterCommand_PopulatesVariablesAndReEvaluates()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        var codeBlocks = new AMFormsCST.Core.Utils.CodeBlocks(enablePersistence: false);
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocks);
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.ClearCanvas();
        vm.PasteFormulaText("IF(F72 = 'N', 'NEW', 'USED')");

        var f72Var = vm.TestVariables.FirstOrDefault(v => v.Name == "F72");
        Assert.NotNull(f72Var);
        f72Var.SampleValue = "UNKNOWN";
        Assert.Equal("USED", vm.TestEvaluatedResult);

        // Act - Apply Path 1 ('NEW') to tester
        var path1 = vm.CodePathOutcomes[0];
        vm.ApplyPathToTesterCommand.Execute(path1);

        // Assert
        Assert.Equal("N", f72Var.SampleValue);
        Assert.Equal("NEW", vm.TestEvaluatedResult);
        Assert.True(vm.CodePathOutcomes[0].IsActive);
        Assert.False(vm.CodePathOutcomes[1].IsActive);
    }

    [Fact]
    public void CodePathTree_TabSwitching_TogglesTabSelectionAndExpandsPanel()
    {
        // Arrange
        var supportToolMock = new Mock<ISupportTool>();
        var codeBlocks = new AMFormsCST.Core.Utils.CodeBlocks(enablePersistence: false);
        supportToolMock.SetupGet(st => st.CodeBlocks).Returns(codeBlocks);
        var vm = new CodeSnippetsViewModel(supportToolMock.Object);
        vm.IsExplainerExpanded = false;

        // Act
        vm.SwitchToCodePathTabCommand.Execute(null);

        // Assert
        Assert.True(vm.IsExplainerExpanded);
        Assert.Equal(1, vm.SelectedExplainerTabIndex);
        Assert.True(vm.IsCodePathTabSelected);
        Assert.False(vm.IsGuideTabSelected);

        vm.SwitchToGuideTabCommand.Execute(null);
        Assert.Equal(0, vm.SelectedExplainerTabIndex);
        Assert.True(vm.IsGuideTabSelected);
        Assert.False(vm.IsCodePathTabSelected);
    }

    [Theory]
    [InlineData("ROUND(F3, 2)", "NUMERIC")]
    [InlineData("NUM(F72)", "NUMERIC")]
    [InlineData("F3 + 100", "NUMERIC")]
    [InlineData("F3 * 0.0825", "NUMERIC")]
    [InlineData("IF(F72 = 'N', F3, 0)", "NUMERIC")]
    [InlineData("CASE(F72, 'N', 100, 'U', 50, 0)", "NUMERIC")]
    [InlineData("F3", "NUMERIC")]
    [InlineData("TEXT(ROUND(F3, 2))", "TEXT")]
    [InlineData("SEPLIST(', ', F100, F101)", "TEXT")]
    [InlineData("IF(F72 = 'N', 'NEW', 'USED')", "TEXT")]
    [InlineData("IF(F72 = 'N', F3, 'N/A')", "TEXT")]
    [InlineData("F776", "TEXT")]
    [InlineData("UNKNOWN_VAR", "TEXT")]
    [InlineData("", "TEXT")]
    public void InferFormulaOutputType_ReturnsExpectedFieldTypeWithTextFallback(string expr, string expectedType)
    {
        Assert.Equal(expectedType, CodeSnippetsViewModel.InferFormulaOutputType(expr));
    }
}