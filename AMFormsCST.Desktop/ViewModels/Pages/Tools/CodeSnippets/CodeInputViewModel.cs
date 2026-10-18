using AMFormsCST.Core.Types.CodeBlocks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.CodeSnippets;

public partial class CodeInputViewModel : ObservableObject
{
    private readonly CodeInput _codeInputModel;
    private readonly object _parent;

    public CodeInput Model => _codeInputModel;
    public object Parent => _parent;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private string? _bindableValue;

    [ObservableProperty]
    private string? _connectedNodeTitle;

    [ObservableProperty]
    private INodeViewModel? _connectedNode;

    [ObservableProperty]
    private bool _hasWarning;

    [ObservableProperty]
    private string? _warningMessage;

    public string Description => _codeInputModel.Description;
    public int Index => _codeInputModel.Index;

    public CodeInputViewModel(CodeInput codeInputModel, CodeSnippetItemViewModel parent)
    {
        _codeInputModel = codeInputModel;
        _bindableValue = _codeInputModel.Value is CodeBase ? string.Empty : _codeInputModel.Value?.ToString();
        _parent = parent;
        Title = Description;
    }

    public CodeInputViewModel(CodeInput codeInputModel, CodeSnippetViewModel parent)
    {
        _codeInputModel = codeInputModel;
        _bindableValue = _codeInputModel.Value is CodeBase ? string.Empty : _codeInputModel.Value?.ToString();
        _parent = parent;
        Title = Description;
    }

    partial void OnConnectedNodeChanging(INodeViewModel? oldValue, INodeViewModel? newValue)
    {
        if (oldValue != null && ReferenceEquals(oldValue.ParentSlot, this))
        {
            oldValue.ParentSlot = null;
        }
    }

    partial void OnConnectedNodeChanged(INodeViewModel? value)
    {
        if (value != null)
        {
            value.ParentSlot = this;
            IsConnected = true;
            ConnectedNodeTitle = value.Title;
            SyncConnectedNodeToModel(value);
        }
        else
        {
            IsConnected = false;
            ConnectedNodeTitle = null;
            _codeInputModel.SetValue(BindableValue ?? string.Empty);
        }

        NotifyParentInputChanged();
    }

    public void OnConnectedNodeValueChanged()
    {
        if (ConnectedNode != null)
        {
            ConnectedNodeTitle = ConnectedNode.Title;
            SyncConnectedNodeToModel(ConnectedNode);
            NotifyParentInputChanged();
        }
    }

    private void SyncConnectedNodeToModel(INodeViewModel node)
    {
        if (node is CodeSnippetViewModel snippetNode && snippetNode.CodeBase is CodeBase cb)
        {
            _codeInputModel.SetValue(cb);
        }
        else if (node is UserInputViewModel userNode)
        {
            _codeInputModel.SetValue(userNode.GetCode());
        }
        else
        {
            _codeInputModel.SetValue(node.GetCode());
        }
    }

    public void SetConnectedNode(CodeSnippetViewModel? node)
    {
        ConnectedNode = node;
    }

    public void SetConnectedNodeTitle(string title)
    {
        ConnectedNodeTitle = title;
        NotifyParentInputChanged();
    }

    partial void OnBindableValueChanged(string? value)
    {
        if (ConnectedNode == null)
        {
            _codeInputModel.SetValue(value ?? string.Empty);
            NotifyParentInputChanged();
        }
    }

    private void NotifyParentInputChanged()
    {
        if (_parent is CodeSnippetItemViewModel itemVM)
        {
            itemVM.InputChanged();
        }
        else if (_parent is CodeSnippetViewModel nodeVM)
        {
            nodeVM.InputChanged();
        }
    }

    [RelayCommand]
    private void UndockConnectedNode()
    {
        if (ConnectedNode != null && _parent is CodeSnippetViewModel parentBlock)
        {
            parentBlock.Workspace?.UndockBlockToCanvas(ConnectedNode);
        }
        else
        {
            ConnectedNode = null;
        }
    }
}

