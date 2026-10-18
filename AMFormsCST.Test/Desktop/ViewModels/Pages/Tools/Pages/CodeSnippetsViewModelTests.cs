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
}