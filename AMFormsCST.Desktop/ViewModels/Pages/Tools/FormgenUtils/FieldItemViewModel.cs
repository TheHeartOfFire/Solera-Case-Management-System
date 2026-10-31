using AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure;
using CommunityToolkit.Mvvm.ComponentModel;
using static AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormField;
using static AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure.FormFieldSettings;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.FormgenUtils;

public partial class FieldItemViewModel : ObservableObject
{
    private readonly Action? _onChanged;
    private readonly Action? _onSelectionChanged;

    public FormField Field { get; }
    public FormPage Page { get; }

    [ObservableProperty]
    private bool _isSelected;

    partial void OnIsSelectedChanged(bool value)
    {
        _onSelectionChanged?.Invoke();
    }

    public FieldItemViewModel(FormField field, FormPage page, Action? onChanged = null, Action? onSelectionChanged = null)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Page = page ?? throw new ArgumentNullException(nameof(page));
        _onChanged = onChanged;
        _onSelectionChanged = onSelectionChanged;
    }

    public int ID => Field.Settings?.ID ?? 0;

    public int PageNumber => Page.Settings?.PageNumber ?? 0;

    public string Expression
    {
        get => Field.Expression ?? string.Empty;
        set
        {
            if (Field.Expression != value)
            {
                Field.Expression = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public string SampleData
    {
        get => Field.SampleData ?? string.Empty;
        set
        {
            if (Field.SampleData != value)
            {
                Field.SampleData = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public FieldType Type
    {
        get => Field.Settings?.Type ?? FieldType.TEXT;
        set
        {
            if (Field.Settings != null && Field.Settings.Type != value)
            {
                Field.Settings.Type = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public int FontSize
    {
        get => Field.Settings?.FontSize ?? 10;
        set
        {
            if (Field.Settings != null && Field.Settings.FontSize != value)
            {
                Field.Settings.FontSize = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public bool Bold
    {
        get => Field.Settings?.Bold ?? false;
        set
        {
            if (Field.Settings != null && Field.Settings.Bold != value)
            {
                Field.Settings.Bold = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public Alignment FontAlignment
    {
        get => Field.Settings?.FontAlignment ?? Alignment.LEFT;
        set
        {
            if (Field.Settings != null && Field.Settings.FontAlignment != value)
            {
                Field.Settings.FontAlignment = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public FormatOption FormattingOption
    {
        get => Field.FormattingOption;
        set
        {
            if (Field.FormattingOption != value)
            {
                Field.FormattingOption = value;
                OnPropertyChanged();
                _onChanged?.Invoke();
            }
        }
    }

    public string LaserCoordsDisplay => $"({LaserRectX}, {LaserRectY}, {LaserRectWidth}, {LaserRectHeight})";

    public string ImpactCoordsDisplay => $"({ImpactCol}, {ImpactLine})";

    public int LaserRectX
    {
        get => Field.Settings?.LaserRect.X ?? 0;
        set
        {
            if (Field.Settings != null && Field.Settings.LaserRect.X != value)
            {
                var r = Field.Settings.LaserRect;
                r.X = value;
                Field.Settings.LaserRect = r;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LaserCoordsDisplay));
                _onChanged?.Invoke();
            }
        }
    }

    public int LaserRectY
    {
        get => Field.Settings?.LaserRect.Y ?? 0;
        set
        {
            if (Field.Settings != null && Field.Settings.LaserRect.Y != value)
            {
                var r = Field.Settings.LaserRect;
                r.Y = value;
                Field.Settings.LaserRect = r;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LaserCoordsDisplay));
                _onChanged?.Invoke();
            }
        }
    }

    public int LaserRectWidth
    {
        get => Field.Settings?.LaserRect.Width ?? 0;
        set
        {
            if (Field.Settings != null && Field.Settings.LaserRect.Width != value)
            {
                var r = Field.Settings.LaserRect;
                r.Width = value;
                Field.Settings.LaserRect = r;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LaserCoordsDisplay));
                _onChanged?.Invoke();
            }
        }
    }

    public int LaserRectHeight
    {
        get => Field.Settings?.LaserRect.Height ?? 0;
        set
        {
            if (Field.Settings != null && Field.Settings.LaserRect.Height != value)
            {
                var r = Field.Settings.LaserRect;
                r.Height = value;
                Field.Settings.LaserRect = r;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LaserCoordsDisplay));
                _onChanged?.Invoke();
            }
        }
    }

    public int ImpactCol
    {
        get => Field.Settings?.ImpactPosition.X ?? 0;
        set
        {
            if (Field.Settings != null && Field.Settings.ImpactPosition.X != value)
            {
                var pos = Field.Settings.ImpactPosition;
                pos.X = value;
                Field.Settings.ImpactPosition = pos;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ImpactCoordsDisplay));
                _onChanged?.Invoke();
            }
        }
    }

    public int ImpactLine
    {
        get => Field.Settings?.ImpactPosition.Y ?? 0;
        set
        {
            if (Field.Settings != null && Field.Settings.ImpactPosition.Y != value)
            {
                var pos = Field.Settings.ImpactPosition;
                pos.Y = value;
                Field.Settings.ImpactPosition = pos;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ImpactCoordsDisplay));
                _onChanged?.Invoke();
            }
        }
    }

    public void RefreshProperties()
    {
        OnPropertyChanged(nameof(ID));
        OnPropertyChanged(nameof(PageNumber));
        OnPropertyChanged(nameof(Expression));
        OnPropertyChanged(nameof(SampleData));
        OnPropertyChanged(nameof(Type));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(Bold));
        OnPropertyChanged(nameof(FontAlignment));
        OnPropertyChanged(nameof(FormattingOption));
        OnPropertyChanged(nameof(LaserRectX));
        OnPropertyChanged(nameof(LaserRectY));
        OnPropertyChanged(nameof(LaserRectWidth));
        OnPropertyChanged(nameof(LaserRectHeight));
        OnPropertyChanged(nameof(LaserCoordsDisplay));
        OnPropertyChanged(nameof(ImpactCol));
        OnPropertyChanged(nameof(ImpactLine));
        OnPropertyChanged(nameof(ImpactCoordsDisplay));
    }
}

