using AMFormsCST.Core.Attributes;
using AMFormsCST.Core.Interfaces.Attributes;
using System.Xml;

namespace AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure
{
    public partial class FormField : IEquatable<FormField>, INotifyPropertyChanged
    {
        [NotifyPropertyChanged]
        private FormFieldSettings? _settings;
        [NotifyPropertyChanged]
        private string? _expression;
        [NotifyPropertyChanged]
        private string? _sampleData;
        [NotifyPropertyChanged]
        private FormatOption _formattingOption;

        public enum FormatOption
        {
            None,
            EmptyZeroPrintsNothing,
            EmptyFieldPrints0,
            NumbersAsWords,
            NAIfBlank
        }

        public FormField()
        {
            Settings = new FormFieldSettings();
            Settings.PropertyChanged += (s, e) => OnPropertyChanged();
        }

        public FormField(FormField other, int newId = 0)
        {
            Settings = other.Settings != null ? new FormFieldSettings(other.Settings, newId) : new FormFieldSettings { ID = newId };
            Expression = other.Expression;
            SampleData = other.SampleData;
            FormattingOption = other.FormattingOption;
            if (Settings is not null)
                Settings.PropertyChanged += (s, e) => OnPropertyChanged();
        }

        public FormField(XmlNode node)
        {
            var xmlAttributeCollection = node.ChildNodes[1]?.Attributes;
            if (xmlAttributeCollection != null)
                Settings = new FormFieldSettings(xmlAttributeCollection);


            var innerText = node.ChildNodes[1]?.ChildNodes[0]?.InnerText;
            if (innerText != null)
                Expression = innerText;

            var sampleData = node.ChildNodes[1]?.ChildNodes[1]?.InnerText;
            if (sampleData != null)
                SampleData = sampleData;

            var option = node.ChildNodes[1]?.ChildNodes[2]?.InnerText;
            if (option != null)
                FormattingOption = GetFormatOption(option);

            if (Settings is not null)
                Settings.PropertyChanged += (s, e) => OnPropertyChanged();
        }

        public static FormatOption GetFormatOption(string option) => option switch
        {
            "None" => FormatOption.None,
            "EmptyZeroPrintsNothing" => FormatOption.EmptyZeroPrintsNothing,
            "BlankPrintsZero" => FormatOption.EmptyFieldPrints0,
            "NumberAsWords" => FormatOption.NumbersAsWords,
            "NAIfBlank" => FormatOption.NAIfBlank,
            _ => FormatOption.None,
        };

        public static string GetFormatOption(FormatOption option) => option switch
        {
            FormatOption.None => "None",
            FormatOption.EmptyZeroPrintsNothing => "EmptyZeroPrintsNothing",
            FormatOption.EmptyFieldPrints0 => "BlankPrintsZero",
            FormatOption.NumbersAsWords => "NumberAsWords",
            FormatOption.NAIfBlank => "NAIfBlank",
            _ => "None",
        };

        public void GenerateXml(XmlWriter xml)
        {
            xml.WriteStartElement("key");
            xml.WriteString(Settings?.ID.ToString());
            xml.WriteEndElement();

            xml.WriteStartElement("value");
            Settings?.GenerateXml(xml);

            xml.WriteStartElement("expression");
            xml.WriteString(Expression);
            xml.WriteEndElement();

            xml.WriteStartElement("sampleData");
            xml.WriteString(SampleData);
            xml.WriteEndElement();

            xml.WriteStartElement("formatOption");
            xml.WriteString(GetFormatOption(FormattingOption));
            xml.WriteEndElement();

            xml.WriteEndElement();

        }

        public bool Equals(FormField? other) => 
            other is not null &&
            Settings?.Equals(other.Settings) == true &&
            (Expression ?? string.Empty).Equals(other.Expression ?? string.Empty, StringComparison.Ordinal) &&
            (SampleData ?? string.Empty).Equals(other.SampleData ?? string.Empty, StringComparison.Ordinal) &&
            FormattingOption == other.FormattingOption;

        public override bool Equals(object? obj) => Equals(obj as FormField);
        public override int GetHashCode() => HashCode.Combine(Settings, Expression, SampleData, FormattingOption);
    }
}
