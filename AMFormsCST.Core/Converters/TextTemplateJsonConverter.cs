using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Documents;
using System.Windows.Markup;

namespace AMFormsCST.Core.Converters;

public class TextTemplateJsonConverter : JsonConverter<TextTemplate>
{
    public override TextTemplate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected StartObject token");
        }

        Guid id = default;
        string name = string.Empty;
        string description = string.Empty;
        string textXaml = string.Empty;
        TextTemplate.TemplateType type = default;
        TextTemplate.TemplateCategoryKind kind = TextTemplate.TemplateCategoryKind.CompleteTemplate;
        List<string> tags = [];
        AMFormsCST.Core.Types.Notebook.CaseActionType? associatedAction = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return new TextTemplate(id, name, description, textXaml, type, kind, tags, associatedAction);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("Expected PropertyName token");
            }

            var propertyName = reader.GetString();
            reader.Read(); // Move to the property value

            switch (propertyName?.ToLowerInvariant())
            {
                case "id":
                    id = reader.GetGuid();
                    break;
                case "name":
                    name = reader.GetString() ?? string.Empty;
                    break;
                case "description":
                    description = reader.GetString() ?? string.Empty;
                    break;
                case "type":
                    // Handle both string and integer enum values
                    if (reader.TokenType == JsonTokenType.String)
                        Enum.TryParse(reader.GetString(), true, out type);
                    else if (reader.TokenType == JsonTokenType.Number)
                        type = (TextTemplate.TemplateType)reader.GetInt32();
                    break;
                case "kind":
                    if (reader.TokenType == JsonTokenType.String)
                        Enum.TryParse(reader.GetString(), true, out kind);
                    else if (reader.TokenType == JsonTokenType.Number)
                        kind = (TextTemplate.TemplateCategoryKind)reader.GetInt32();
                    break;
                case "tags":
                    if (reader.TokenType == JsonTokenType.StartArray)
                    {
                        tags = [];
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                var t = reader.GetString();
                                if (!string.IsNullOrWhiteSpace(t)) tags.Add(t);
                            }
                        }
                    }
                    break;
                case "associatedaction":
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        if (Enum.TryParse<AMFormsCST.Core.Types.Notebook.CaseActionType>(reader.GetString(), true, out var act))
                            associatedAction = act;
                    }
                    else if (reader.TokenType == JsonTokenType.Number)
                    {
                        associatedAction = (AMFormsCST.Core.Types.Notebook.CaseActionType)reader.GetInt32();
                    }
                    break;
                case "text":
                    var textValue = reader.GetString() ?? string.Empty;
                    try
                    {
                        // Validate XAML by parsing
                        System.Windows.Markup.XamlReader.Parse(textValue);
                        textXaml = textValue; // It's valid XAML
                    }
                    catch (XamlParseException)
                    {
                        // Fallback: convert plain text to valid XAML
                        var doc = new FlowDocument(new Paragraph(new Run(textValue)));
                        textXaml = System.Windows.Markup.XamlWriter.Save(doc);
                    }
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }
        
        // Construct using TextXaml string
        return new TextTemplate(id, name, description, textXaml, type, kind, tags, associatedAction);
    }

    public override void Write(Utf8JsonWriter writer, TextTemplate value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Id", value.Id.ToString()); // Use Id property directly; key casing?
        // Original code used "id" lowercase. Let's match original.
        writer.WriteString("id", value.Id.ToString());
        writer.WriteString("name", value.Name);
        writer.WriteString("description", value.Description);
        writer.WriteNumber("type", (int)value.Type);
        writer.WriteNumber("kind", (int)value.Kind);

        if (value.Tags != null && value.Tags.Count > 0)
        {
            writer.WriteStartArray("tags");
            foreach (var tag in value.Tags)
            {
                writer.WriteStringValue(tag);
            }
            writer.WriteEndArray();
        }

        if (value.AssociatedAction.HasValue)
        {
            writer.WriteNumber("associatedAction", (int)value.AssociatedAction.Value);
        }

        // Serialize stored XAML string directly
        writer.WriteString("text", value.TextXaml); 

        writer.WriteEndObject();
    }
}