using AMFormsCST.Core.Attributes;
using AMFormsCST.Core.Interfaces.Attributes;
using System.Xml;

namespace AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure
{
    public partial class CodeLine : IEquatable<CodeLine>, INotifyPropertyChanged
    {
        [NotifyPropertyChanged]
        private CodeLineSettings? _settings;
        [NotifyPropertyChanged]
        private string? _expression;
        [NotifyPropertyChanged]
        private PromptData? _promptData;
        public CodeLine(XmlNode node)
        {
            if (node.Attributes is not null) Settings = new CodeLineSettings(node.Attributes);

            if (Settings is not null && Settings.Type != CodeLineSettings.CodeType.PROMPT)
            {
                if (node.FirstChild is not null) Expression = node.FirstChild.InnerText;
            }
            else if (node.FirstChild is not null) PromptData = new PromptData(node.FirstChild);

            if (Settings is not null) Settings.PropertyChanged += (s, e) => OnPropertyChanged();
            if (PromptData is not null) PromptData.PropertyChanged += (s, e) => OnPropertyChanged();
        }
        public CodeLine(CodeLine codeLine, string? newName, int newIndex)
        {
            if (codeLine.Settings is not null) Settings = new CodeLineSettings(codeLine.Settings, newName, newIndex);
            Expression = codeLine.Expression;
            PromptData = codeLine.PromptData;
            if(Settings is not null) Settings.PropertyChanged += (s, e) => OnPropertyChanged();
            if (PromptData is not null) PromptData.PropertyChanged += (s, e) => OnPropertyChanged();
        }
        public CodeLine()
        {
            Settings = new CodeLineSettings();
            PromptData = new PromptData();
            Settings.PropertyChanged += (s, e) => OnPropertyChanged();
            PromptData.PropertyChanged += (s, e) => OnPropertyChanged();
        }

        public void GenerateXml(XmlWriter xml)
        {
            xml.WriteStartElement("codeLines");
            if (Settings is null) return;
            Settings.GenerateXml(xml);

            if (Settings.Type is not CodeLineSettings.CodeType.PROMPT)
            {
                xml.WriteStartElement("expression");
                xml.WriteString(Expression);
                xml.WriteEndElement();
            }
            else
            {
                var promptData = PromptData;
                promptData?.GenerateXml(xml);
            }

            xml.WriteEndElement();
        }

        public bool Equals(CodeLine? other) => 
            other is not null &&
            Settings?.Equals(other.Settings) == true &&
            (Expression ?? string.Empty).Equals(other.Expression ?? string.Empty, StringComparison.Ordinal) &&
            ((PromptData is null && other.PromptData is null) ||
            (PromptData is not null && PromptData.Equals(other.PromptData)));

        public override bool Equals(object? obj) => Equals(obj as CodeLine);
        

        public override int GetHashCode() =>  HashCode.Combine(Settings, Expression, PromptData);
    }
}
