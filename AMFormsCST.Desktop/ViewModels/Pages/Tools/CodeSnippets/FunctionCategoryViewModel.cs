using AMFormsCST.Core.Interfaces.CodeBlocks;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;

public partial class FunctionCategoryViewModel : ObservableObject
{
    public string Name { get; }
    public string Header => $"{Name} ({Functions.Count})";

    [ObservableProperty]
    private bool _isExpanded;

    public ObservableCollection<ICodeBase> Functions { get; }

    public Brush ItemBackgroundBrush { get; }
    public Brush ItemBorderBrush { get; }
    public Brush BadgeBackgroundBrush { get; }

    public FunctionCategoryViewModel(
        string name,
        IEnumerable<ICodeBase> functions,
        string backgroundHex,
        string borderHex,
        string badgeHex,
        bool isExpanded = true)
    {
        Name = name;
        Functions = new ObservableCollection<ICodeBase>(functions);
        _isExpanded = isExpanded;
        ItemBackgroundBrush = TestVariableViewModel.CreateFrozenBrush(backgroundHex);
        ItemBorderBrush = TestVariableViewModel.CreateFrozenBrush(borderHex);
        BadgeBackgroundBrush = TestVariableViewModel.CreateFrozenBrush(badgeHex);
    }
}

