using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.BestPractices;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Desktop.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Documents;
using System.Windows.Markup;
using AMFormsCST.Core.Types.Notebook;
using static AMFormsCST.Core.Types.BestPractices.TextTemplates.Models.TextTemplate;

namespace AMFormsCST.Desktop.ViewModels.Pages.Tools.Templates;

public partial class TemplateItemViewModel : ObservableObject, ISelectable
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Output))]
    private TextTemplate _template;

    public TemplateCategoryKind Kind
    {
        get => Template.Kind;
        set
        {
            if (Template.Kind != value)
            {
                Template.Kind = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(KindDisplayName));
            }
        }
    }

    public string KindDisplayName => Kind switch
    {
        TemplateCategoryKind.CompleteTemplate => "Complete Template",
        TemplateCategoryKind.HeaderPartial => "Header Partial",
        TemplateCategoryKind.ActionPartial => "Action Partial",
        TemplateCategoryKind.FooterPartial => "Footer Partial",
        _ => Kind.ToString()
    };

    public CaseActionType? AssociatedAction
    {
        get => Template.AssociatedAction;
        set
        {
            if (Template.AssociatedAction != value)
            {
                Template.AssociatedAction = value;
                OnPropertyChanged();
            }
        }
    }

    public ObservableCollection<string> Tags { get; } = [];

    public string TagsDisplay => Tags.Count > 0 ? string.Join(", ", Tags) : "No Tags";

    public void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        var formatted = tag.Trim();
        if (!formatted.StartsWith("#")) formatted = "#" + formatted;
        if (!Template.Tags.Contains(formatted))
        {
            Template.Tags.Add(formatted);
            Tags.Add(formatted);
            OnPropertyChanged(nameof(TagsDisplay));
        }
    }

    public void RemoveTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        if (Template.Tags.Remove(tag))
        {
            Tags.Remove(tag);
            OnPropertyChanged(nameof(TagsDisplay));
        }
    }

    private ObservableCollection<TemplateVariableViewModel> _variables;
    public ObservableCollection<TemplateVariableViewModel> Variables
    {
        get => _variables;
        set
        {
            if (SetProperty(ref _variables, value))
            {
                if (_variables != null)
                {
                    foreach (var variable in _variables)
                    {
                        variable.PropertyChanged -= OnVariablePropertyChanged;
                    }
                }

                _variables = value;

                if (_variables != null)
                {
                    foreach (var variable in _variables)
                    {
                        variable.PropertyChanged += OnVariablePropertyChanged;
                    }
                    _variables.CollectionChanged += OnVariablesCollectionChanged;
                }
                OnPropertyChanged(nameof(Output));
            }
        }
    }

    private readonly ISupportTool _supportTool;
    private readonly ILogService? _logger;

    [ObservableProperty]
    private int _usageCount = 0;

    public TemplateItemViewModel(TextTemplate template, ISupportTool supportTool, ILogService? logger = null)
    {
        _supportTool = supportTool ?? throw new ArgumentNullException(nameof(supportTool));
        _template = template;
        _logger = logger;
        foreach (var tag in template.Tags)
        {
            Tags.Add(tag);
        }
        _variables = new ObservableCollection<TemplateVariableViewModel>(
            template.GetVariables(_supportTool)
                    .Select(variable => new TemplateVariableViewModel(variable, _logger) { Variable = variable }));

        foreach (var variable in Variables)
        {
            variable.PropertyChanged += OnVariablePropertyChanged;
        }
        Variables.CollectionChanged += OnVariablesCollectionChanged;
        _logger?.LogInfo($"TemplateItemViewModel initialized: Template='{template.Name}'");
    }

    public FlowDocument Output
    {
        get
        {
            // Create a deep copy of the template document to avoid modifying the original.
            var xaml = XamlWriter.Save(Template.Text);
            var doc = (FlowDocument)XamlReader.Parse(xaml);

            var templateVariables = Template.GetVariables(_supportTool);
            var variableValues = Variables.Select(v => v.Value).ToList();

            for (int i = 0; i < templateVariables.Count; i++)
            {
                var variable = templateVariables[i];
                var value = (i < variableValues.Count && !string.IsNullOrEmpty(variableValues[i]))
                            ? variableValues[i]
                            : variable.GetValue();

                // Replace all occurrences of the variable's placeholder in the document.
                ReplaceInFlowDocument(doc, variable.ProperName, value);
            }

            _logger?.LogDebug($"TemplateItemViewModel Output generated for '{Template.Name}'");
            return doc;
        }
    }

    private static void ReplaceInFlowDocument(FlowDocument doc, string placeholder, string value)
    {
        var textRange = new TextRange(doc.ContentStart, doc.ContentEnd);
        var current = textRange.Start;

        while (current != null && current.CompareTo(textRange.End) < 0)
        {
            if (current.GetPointerContext(LogicalDirection.Forward) == TextPointerContext.Text)
            {
                var textRun = current.GetTextInRun(LogicalDirection.Forward);
                var index = textRun.IndexOf(placeholder, StringComparison.OrdinalIgnoreCase);

                if (index != -1)
                {
                    var start = current.GetPositionAtOffset(index);
                    var end = current.GetPositionAtOffset(index + placeholder.Length);
                    new TextRange(start, end).Text = value;
                }
            }
            current = current.GetNextContextPosition(LogicalDirection.Forward);
        }
    }

    private readonly Guid _id = Guid.NewGuid();

    public Guid Id => _id;

    [ObservableProperty]
    private bool _isSelected;

    private void OnVariablePropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TemplateVariableViewModel.Value))
        {
            OnPropertyChanged(nameof(Output));
            _logger?.LogDebug($"Variable value changed in TemplateItemViewModel '{Template.Name}'. Output updated.");
        }
    }

    private void OnVariablesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (TemplateVariableViewModel variable in e.OldItems)
            {
                variable.PropertyChanged -= OnVariablePropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (TemplateVariableViewModel variable in e.NewItems)
            {
                variable.PropertyChanged += OnVariablePropertyChanged;
            }
        }

        OnPropertyChanged(nameof(Output));
        _logger?.LogDebug($"Variables collection changed in TemplateItemViewModel '{Template.Name}'. Output updated.");
    }

    private (string processedText, List<(int position, ITextTemplateVariable variable, string alias)> variables) PreProcessTemplate(string templateText)
    {
        if (string.IsNullOrEmpty(templateText))
        {
            return (string.Empty, []);
        }

        int counter = 0;
        var processedText = templateText;
        var variables = new List<(int position, ITextTemplateVariable variable, string alias)>();

        while (TextTemplate.ContainsVariable(processedText, _supportTool))
        {
            var variable = TextTemplate.GetFirstVariable(processedText, _supportTool);

            if (variable.variable == null) break;

            variables.Add((variable.position, variable.variable, variable.alias));

            processedText = processedText.Replace(variable.alias, $"{{{counter}}}");
            counter++;
        }

        return (processedText, variables);
    }
    public void RefreshTemplateData()
    {
        OnPropertyChanged(nameof(Template));

        //var preprocessedTemplate = PreProcessTemplate(TextTemplate.GetFlowDocumentPlainText(Template.Text));

        //var newVariables = new ObservableCollection<TemplateVariableViewModel>(
        //    preprocessedTemplate.variables
        //        .Select(variable => new TemplateVariableViewModel(variable.variable, _logger) { Variable = variable.variable }));

        //Variables.Clear();
        //foreach (var v in newVariables)
        //{
        //    Variables.Add(v);
        //}

        OnPropertyChanged(nameof(Output));
        _logger?.LogInfo($"TemplateItemViewModel '{Template.Name}' data refreshed.");
    }

    public void Select()
    {
        IsSelected = true;
        _logger?.LogDebug($"TemplateItemViewModel '{Template.Name}' selected.");
    }

    public void Deselect()
    {
        IsSelected = false;
        _logger?.LogDebug($"TemplateItemViewModel '{Template.Name}' deselected.");
    }
}

