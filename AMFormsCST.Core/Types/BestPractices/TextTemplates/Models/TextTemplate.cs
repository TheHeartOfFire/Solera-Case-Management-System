using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.BestPractices;
using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.IO;
using System.Text;

namespace AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
public class TextTemplate : IEquatable<TextTemplate>
{
    [JsonInclude]
    public Guid Id { get; private set; }// Default to empty GUID for new templates
    public string Name { get; set; }
    public string Description { get; set; }
    public string TextXaml { get; set; }

    [JsonIgnore]
    public FlowDocument Text 
    { 
        get
        {
             if (string.IsNullOrWhiteSpace(TextXaml)) return new FlowDocument();
             try 
             {
                 using var stream = new MemoryStream(Encoding.UTF8.GetBytes(TextXaml));
                 return System.Windows.Markup.XamlReader.Load(stream) as FlowDocument ?? new FlowDocument();
             } 
             catch { return new FlowDocument(); }
        }
        set
        {
             if (value == null) TextXaml = string.Empty;
             else TextXaml = System.Windows.Markup.XamlWriter.Save(value);
        }
    }

    public TemplateType Type { get; set; } 
    public enum TemplateType
    {
        PublishComments,
        InternalComments,
        ClosureComments,
        Email,
        Other
    }
    public TextTemplate(string name, string description, string textXaml, TemplateType type)
    {
        Id = Guid.NewGuid(); 
        Name = name;
        Description = description;
        TextXaml = textXaml;
        Type = type;
    }
    
    // Kept for compatibility if needed, or update consumers
    public TextTemplate(string name, string description, FlowDocument text, TemplateType type)
    {
        Id = Guid.NewGuid();
        Name = name;
        Description = description;
        Text = text;
        TextXaml = System.Windows.Markup.XamlWriter.Save(text);
        Type = type;
    }

    [JsonConstructor]
    public TextTemplate(Guid id, string name, string description, string textXaml, TemplateType type)
    {
        Id = id;
        Name = name;
        Description = description;
        TextXaml = textXaml;
        Type = type;
    }

    public List<ITextTemplateVariable> GetVariables(ISupportTool supportTool)
    {
        var variables = new List<ITextTemplateVariable>();
        if (supportTool?.Settings?.UserSettings?.Organization?.Variables is null)
        {
            return variables;
        }
        foreach (var variable in supportTool.Settings.UserSettings.Organization.Variables)
        {
            if (Text is not null && 
                (GetFlowDocumentPlainText(Text).Contains(variable.ProperName, StringComparison.InvariantCultureIgnoreCase) ||
                GetFlowDocumentPlainText(Text).Contains(variable.Prefix + variable.Name, StringComparison.InvariantCultureIgnoreCase)))
            {
                variables.Add(variable);
            }
        }
        return variables;
    }

    public static string Process(string processedText, List<ITextTemplateVariable> orderedListOfVariables, List<string> overrides) 
    {
        var variableValues = orderedListOfVariables.Select(x => x.GetValue()).ToList();

        for (int i = 0; i < overrides.Count; i++)
        {
            if (!overrides[i].Equals(string.Empty) || orderedListOfVariables[i].ProperName.Equals("User:Input"))
            {
                    variableValues[i] = overrides[i];
            }
        }



        return string.Format(processedText, [.. variableValues]);
    }

    public bool Equals(TextTemplate? other)
    {
        return other is not null && other.Id.Equals(Id);
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as TextTemplate);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id);
    }

    public static bool ContainsVariable(string text, ISupportTool supportTool)
    {
        var variables = supportTool.Settings.UserSettings.Organization.Variables;
        foreach (var variable in variables)
        {
            if (text.Contains(variable.ProperName, StringComparison.InvariantCultureIgnoreCase) ||
                text.Contains(variable.Prefix + variable.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                return true;
            }
            foreach (var alias in variable.Aliases)
            {
                if (text.Contains(variable.Prefix + alias, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static (int position, ITextTemplateVariable? variable, string alias) GetFirstVariable(string text, ISupportTool supportTool)
    {
        var variables = supportTool.Settings.UserSettings.Organization.Variables;

        (int position, ITextTemplateVariable? variable, string alias) lowestIndex = (-1, null, string.Empty);

        if(!ContainsVariable(text, supportTool))
            return lowestIndex;

        foreach (var variable in variables)
        {
            
            int indexProperName = text.IndexOf(variable.ProperName, StringComparison.InvariantCultureIgnoreCase);

            if (indexProperName != -1 && (lowestIndex.variable == null || indexProperName < lowestIndex.position))
                lowestIndex = (indexProperName, variable, variable.ProperName);

                
            int indexPrefixName = text.IndexOf(variable.Prefix + variable.Name, StringComparison.InvariantCultureIgnoreCase);

            if (indexPrefixName != -1 && (lowestIndex.variable == null || indexPrefixName < lowestIndex.position))
                lowestIndex = (indexPrefixName, variable, variable.Prefix + variable.Name);


            foreach (var alias in variable.Aliases)
            {
                int indexAlias = text.IndexOf(variable.Prefix + alias, StringComparison.InvariantCultureIgnoreCase);

                if (indexAlias != -1 && (lowestIndex.variable == null || indexAlias < lowestIndex.position))
                        lowestIndex = (indexAlias, variable, variable.Prefix + alias);
            }
        }

        return lowestIndex;
    }
    public static string GetFlowDocumentPlainText(FlowDocument document)
    {
        // Create a TextRange from the beginning (ContentStart) to the end (ContentEnd) of the document.
        TextRange textRange = new(
            document.ContentStart,
            document.ContentEnd
        );

        // The Text property of the TextRange object returns the plain text content as a string.
        return textRange.Text;
    }
}

