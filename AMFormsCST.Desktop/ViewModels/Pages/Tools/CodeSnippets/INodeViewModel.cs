using System.ComponentModel;
using System.Windows;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;

public interface INodeViewModel : INotifyPropertyChanged
{
    Point Location { get; set; }
    string? Title { get; set; }
    bool IsSelected { get; set; }
    string Description { get; }
    int Index { get; }
    string Category { get; }
    CodeInputViewModel? ParentSlot { get; set; }
    bool IsDocked { get; }
    string GetCode();
}