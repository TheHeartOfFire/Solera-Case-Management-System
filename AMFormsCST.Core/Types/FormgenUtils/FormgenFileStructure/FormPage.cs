using AMFormsCST.Core.Attributes;
using AMFormsCST.Core.Interfaces.Attributes;
using System.Xml;

namespace AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure
{
    public partial class FormPage : IEquatable<FormPage>, INotifyPropertyChanged
    {
        [NotifyPropertyChanged]
        private FormPageSettings? _settings;
        [NotifyPropertyChanged]
        private List<FormField> _fields = new();

        public FormPage()
        {
            Settings = new FormPageSettings();
            Settings.PropertyChanged += (s, e) => OnPropertyChanged();
        }

        public FormPage(XmlNode node)
        {
            if (node.Attributes != null) Settings = new FormPageSettings(node.Attributes);
            if (Settings is not null) Settings.PropertyChanged += (s, e) => OnPropertyChanged();

            if (node.FirstChild == null) return;
            foreach (XmlNode child in node.FirstChild)
            {
                var field = new FormField(child);
                field.PropertyChanged += (s, e) => OnPropertyChanged();
                Fields.Add(field);
            }
        }

        public void GenerateXml(XmlWriter xml)
        {

            xml.WriteStartElement("pages");
            Settings?.GenerateXml(xml);

            xml.WriteStartElement("fields");
            foreach (var field in Fields)
            {
                xml.WriteStartElement("entry");
                field.GenerateXml(xml);
                xml.WriteEndElement();
            }

            xml.WriteEndElement();
            xml.WriteEndElement();
        }

        public bool Equals(FormPage? other) => 
            other is not null &&
            Settings?.Equals(other.Settings) == true &&
            Fields.Count == other.Fields.Count &&
            Fields.SequenceEqual(other.Fields);

        public override bool Equals(object? obj) => Equals(obj as FormPage);
        public override int GetHashCode() => HashCode.Combine(Settings, Fields);
    }
}
