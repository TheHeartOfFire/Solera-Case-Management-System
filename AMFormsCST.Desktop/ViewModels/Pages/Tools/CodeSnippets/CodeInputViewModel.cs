using AMFormsCST.Core.Types.CodeBlocks;
using AMFormsCST.Core.Utils;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Text;

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

    [ObservableProperty]
    private FormgenFieldInfo? _fieldInfo;

    public bool HasFieldInfo => FieldInfo is not null;
    public bool IsFieldVmSafe => FieldInfo?.IsVehicleMerchandisingSafe ?? false;
    public string? FieldBadgeText => FieldInfo != null
        ? $"{FieldInfo.Description} [{FieldInfo.VmStatusBadge}]"
        : null;

    public string SocketTooltip
    {
        get
        {
            var sb = new StringBuilder();
            var parentTitle = _parent switch
            {
                CodeSnippetViewModel cs => cs.Title ?? cs.PrefixBadge,
                CodeSnippetItemViewModel item => item.Name ?? "Snippet",
                _ => "Block"
            };
            sb.AppendLine($"Input Slot: '{Description}' (Argument #{Index + 1} of {parentTitle})");

            if (HasWarning && !string.IsNullOrWhiteSpace(WarningMessage))
            {
                sb.AppendLine();
                sb.AppendLine($"⚠ Why This Needs Attention:");
                sb.AppendLine(WarningMessage);
            }

            if (FieldInfo != null)
            {
                sb.AppendLine();
                sb.Append(FieldInfo.VerboseTooltip);
            }
            else if (!IsConnected && string.IsNullOrWhiteSpace(BindableValue))
            {
                sb.AppendLine();
                sb.Append("Tip: Type an F-field (e.g. F3, F776), a single-quoted literal (e.g. 'text'), a number, or drag a puzzle block into this socket.");
            }

            return sb.ToString().TrimEnd();
        }
    }

    public string Description => _codeInputModel.Description;
    public int Index => _codeInputModel.Index;

    public CodeInputViewModel(CodeInput codeInputModel, CodeSnippetItemViewModel parent)
    {
        _codeInputModel = codeInputModel;
        _bindableValue = _codeInputModel.Value is CodeBase ? string.Empty : _codeInputModel.Value?.ToString();
        _parent = parent;
        Title = Description;
        RefreshFieldInfo();
    }

    public CodeInputViewModel(CodeInput codeInputModel, CodeSnippetViewModel parent)
    {
        _codeInputModel = codeInputModel;
        _bindableValue = _codeInputModel.Value is CodeBase ? string.Empty : _codeInputModel.Value?.ToString();
        _parent = parent;
        Title = Description;
        RefreshFieldInfo();
    }

    partial void OnWarningMessageChanged(string? value)
    {
        OnPropertyChanged(nameof(SocketTooltip));
    }

    partial void OnHasWarningChanged(bool value)
    {
        OnPropertyChanged(nameof(SocketTooltip));
    }

    private void RefreshFieldInfo()
    {
        string? rawCandidate = null;
        if (ConnectedNode is UserInputViewModel userNode)
        {
            rawCandidate = userNode.Value;
        }
        else if (ConnectedNode == null)
        {
            rawCandidate = BindableValue;
        }

        FieldInfo = !string.IsNullOrWhiteSpace(rawCandidate) && CodeBase.IsLikelyFieldReference(rawCandidate.Trim())
            ? FormgenReferenceCatalog.TryGetField(rawCandidate.Trim())
            : null;

        OnPropertyChanged(nameof(HasFieldInfo));
        OnPropertyChanged(nameof(IsFieldVmSafe));
        OnPropertyChanged(nameof(FieldBadgeText));
        OnPropertyChanged(nameof(SocketTooltip));
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

        RefreshFieldInfo();
        NotifyParentInputChanged();
    }

    public void OnConnectedNodeValueChanged()
    {
        if (ConnectedNode != null)
        {
            ConnectedNodeTitle = ConnectedNode.Title;
            SyncConnectedNodeToModel(ConnectedNode);
            RefreshFieldInfo();
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
            RefreshFieldInfo();
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

