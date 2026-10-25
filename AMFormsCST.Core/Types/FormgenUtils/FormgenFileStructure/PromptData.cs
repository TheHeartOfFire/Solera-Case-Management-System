using AMFormsCST.Core.Attributes;
using AMFormsCST.Core.Interfaces.Attributes;
using System.Xml;

namespace AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure
{
    public partial class PromptData : IEquatable<PromptData>, INotifyPropertyChanged
    {
        [NotifyPropertyChanged]
        private PromptDataSettings? _settings;
        [NotifyPropertyChanged]
        private string? _message;
        [NotifyPropertyChanged]
        private List<string> _choices = [];

        public PromptData() 
        { 
            Settings = new PromptDataSettings(); 
            Settings.PropertyChanged += (s, e) => OnPropertyChanged(); 
        }

        public PromptData(XmlNode node)
        {
            if (node.Attributes != null) Settings = new PromptDataSettings(node.Attributes);

            if (node.FirstChild is { HasChildNodes: true })
                Message = node.FirstChild.FirstChild?.InnerText;

            foreach (XmlNode child in node.ChildNodes)
                if (child.Name == "choices")
                    Choices.Add(child.InnerText);
            if(Settings is not null) Settings.PropertyChanged += (s, e) => OnPropertyChanged();
        }

        internal void GenerateXml(XmlWriter xml)
        {
            xml.WriteStartElement("promptData");
            Settings?.GenerateXml(xml);

            xml.WriteStartElement("promptMessage");
            xml.WriteString(Message);
            xml.WriteEndElement();

            foreach (var choice in Choices)
            {
                xml.WriteStartElement("choices");
                xml.WriteString(choice);
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
        }

        public bool Equals(PromptData? other) => 
            other is not null &&
            Settings?.Equals(other.Settings) == true &&
            (Message ?? string.Empty).Equals(other.Message ?? string.Empty, StringComparison.Ordinal) &&
            Choices.Count == other.Choices.Count &&
            Choices.SequenceEqual(other.Choices);

        public override bool Equals(object? obj) => Equals(obj as PromptData);
        public override int GetHashCode() => HashCode.Combine(Settings, Message, Choices);
    }
}
