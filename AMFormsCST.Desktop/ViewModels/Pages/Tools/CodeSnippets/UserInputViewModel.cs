using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Windows;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;

public partial class UserInputViewModel : ObservableObject, INodeViewModel
{
    public const string TypeFieldRaw = "Field / Raw";
    public const string TypeStringQuoted = "String (single-quoted)";
    public const string TypeNumber = "Number";
    public const string TypeBoolean = "Boolean";

    [ObservableProperty]
    private Point _location;

    [ObservableProperty]
    private string? _title = "Value / Field";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    private string _value = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedValue))]
    private string _selectedType = TypeFieldRaw;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDocked))]
    private CodeInputViewModel? _parentSlot;

    public bool IsDocked => ParentSlot is not null;

    public CodeSnippetsViewModel? Workspace { get; set; }

    public ObservableCollection<string> AvailableTypes { get; } =
    [
        TypeFieldRaw,
        TypeStringQuoted,
        TypeNumber,
        TypeBoolean
    ];

    public string Description => Title ?? "Value / Field";
    public int Index => 0;
    public string Category => "Value";

    public string FormattedValue => GetCode();

    public string GetCode()
    {
        if (SelectedType == TypeStringQuoted)
        {
            var raw = Value ?? string.Empty;
            if (raw.Length >= 2 && raw.StartsWith('\'') && raw.EndsWith('\''))
            {
                return raw;
            }
            return $"'{raw}'";
        }

        if (string.IsNullOrEmpty(Value))
        {
            return string.Empty;
        }

        return Value;
    }

    partial void OnValueChanged(string value)
    {
        ParentSlot?.OnConnectedNodeValueChanged();
        Workspace?.NotifyGraphUpdated(this);
    }

    partial void OnSelectedTypeChanged(string value)
    {
        ParentSlot?.OnConnectedNodeValueChanged();
        Workspace?.NotifyGraphUpdated(this);
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (value)
        {
            Workspace?.OnNodeSelected(this);
        }
    }

    [RelayCommand]
    private void Undock()
    {
        Workspace?.UndockBlockToCanvas(this);
    }

    [RelayCommand]
    private void Remove()
    {
        Workspace?.RemoveSpecificNode(this);
    }
}