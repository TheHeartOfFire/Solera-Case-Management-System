using AMFormsCST.Core.Interfaces;
using System.Drawing;
using AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure;
using AMFormsCST.Desktop.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AMFormsCST.Desktop.Models.FormgenUtilities;

public partial class FieldProperties : ObservableObject, IFormgenFileProperties
{
    private readonly FormField _coreField;
    private readonly ILogService? _logger;

    public IFormgenFileSettings Settings { get; set; }

    public string? Expression
    {
        get => _coreField.Expression;
        set
        {
            SetProperty(_coreField.Expression, value, _coreField, (f, v) => f.Expression = v);
            _logger?.LogInfo($"Field Expression changed: {value}");
        }
    }

    public string? SampleData
    {
        get => _coreField.SampleData;
        set
        {
            SetProperty(_coreField.SampleData, value, _coreField, (f, v) => f.SampleData = v);
            _logger?.LogInfo($"Field SampleData changed: {value}");
        }
    }

    public FormField.FormatOption FormattingOption
    {
        get => _coreField.FormattingOption;
        set
        {
            SetProperty(_coreField.FormattingOption, value, _coreField, (f, v) => f.FormattingOption = v);
            _logger?.LogInfo($"Field FormattingOption changed: {value}");
        }
    }

    public int LaserRectX
    {
        get => _coreField.Settings.LaserRect.X;
        set
        {
            if (_coreField.Settings.LaserRect.X != value)
            {
                var rect = _coreField.Settings.LaserRect;
                rect.X = value;
                _coreField.Settings.LaserRect = rect;
                if (Settings is FieldSettings fs) fs.LaserRect = rect;
                OnPropertyChanged();
                _logger?.LogInfo($"Field LaserRectX changed: {value}");
            }
        }
    }

    public int LaserRectY
    {
        get => _coreField.Settings.LaserRect.Y;
        set
        {
            if (_coreField.Settings.LaserRect.Y != value)
            {
                var rect = _coreField.Settings.LaserRect;
                rect.Y = value;
                _coreField.Settings.LaserRect = rect;
                if (Settings is FieldSettings fs) fs.LaserRect = rect;
                OnPropertyChanged();
                _logger?.LogInfo($"Field LaserRectY changed: {value}");
            }
        }
    }

    public int LaserRectWidth
    {
        get => _coreField.Settings.LaserRect.Width;
        set
        {
            if (_coreField.Settings.LaserRect.Width != value)
            {
                var rect = _coreField.Settings.LaserRect;
                rect.Width = value;
                _coreField.Settings.LaserRect = rect;
                if (Settings is FieldSettings fs) fs.LaserRect = rect;
                OnPropertyChanged();
                _logger?.LogInfo($"Field LaserRectWidth changed: {value}");
            }
        }
    }

    public int LaserRectHeight
    {
        get => _coreField.Settings.LaserRect.Height;
        set
        {
            if (_coreField.Settings.LaserRect.Height != value)
            {
                var rect = _coreField.Settings.LaserRect;
                rect.Height = value;
                _coreField.Settings.LaserRect = rect;
                if (Settings is FieldSettings fs) fs.LaserRect = rect;
                OnPropertyChanged();
                _logger?.LogInfo($"Field LaserRectHeight changed: {value}");
            }
        }
    }

    public FieldProperties(FormField field, ILogService? logger = null)
    {
        _coreField = field;
        _logger = logger;
        Settings = new FieldSettings(field.Settings, _logger);
        _logger?.LogInfo("FieldProperties initialized.");
    }

    public IEnumerable<DisplayProperty> GetDisplayProperties()
    {
        var fieldType = typeof(FormField);
        var exprProp = fieldType.GetProperty(nameof(FormField.Expression));
        if (exprProp != null)
            yield return new DisplayProperty(_coreField, exprProp, false, _logger);

        var sampleDataProp = fieldType.GetProperty(nameof(FormField.SampleData));
        if (sampleDataProp != null)
            yield return new DisplayProperty(_coreField, sampleDataProp, false, _logger);

        var formatOptionProp = fieldType.GetProperty(nameof(FormField.FormattingOption));
        if (formatOptionProp != null)
            yield return new DisplayProperty(_coreField, formatOptionProp, false, _logger);

        var selfType = typeof(FieldProperties);

        var laserRectXProp = selfType.GetProperty(nameof(LaserRectX));
        if (laserRectXProp != null)
            yield return new DisplayProperty(this, laserRectXProp, false, _logger);

        var laserRectYProp = selfType.GetProperty(nameof(LaserRectY));
        if (laserRectYProp != null)
            yield return new DisplayProperty(this, laserRectYProp, false, _logger);

        var laserRectWidthProp = selfType.GetProperty(nameof(LaserRectWidth));
        if (laserRectWidthProp != null)
            yield return new DisplayProperty(this, laserRectWidthProp, false, _logger);

        var laserRectHeightProp = selfType.GetProperty(nameof(LaserRectHeight));
        if (laserRectHeightProp != null)
            yield return new DisplayProperty(this, laserRectHeightProp, false, _logger);

        if (Settings is FieldSettings fieldSettings)
        {
            var settingsType = typeof(FieldSettings);

            var idProp = settingsType.GetProperty(nameof(FieldSettings.ID));
            if (idProp != null)
                yield return new DisplayProperty(fieldSettings, idProp, true, _logger); 

            var typeProp = settingsType.GetProperty(nameof(FieldSettings.Type));
            if (typeProp != null)
                yield return new DisplayProperty(fieldSettings, typeProp, false, _logger);

            var fontSizeProp = settingsType.GetProperty(nameof(FieldSettings.FontSize));
            if (fontSizeProp != null)
                yield return new DisplayProperty(fieldSettings, fontSizeProp, false, _logger);

            var fontAlignmentProp = settingsType.GetProperty(nameof(FieldSettings.FontAlignment));
            if (fontAlignmentProp != null)
                yield return new DisplayProperty(fieldSettings, fontAlignmentProp, false, _logger);

            var boldProp = settingsType.GetProperty(nameof(FieldSettings.Bold));
            if (boldProp != null)
                yield return new DisplayProperty(fieldSettings, boldProp, false, _logger);

            var shrinkToFitProp = settingsType.GetProperty(nameof(FieldSettings.ShrinkToFit));
            if (shrinkToFitProp != null)
                yield return new DisplayProperty(fieldSettings, shrinkToFitProp, false, _logger);
        }
    }
}
