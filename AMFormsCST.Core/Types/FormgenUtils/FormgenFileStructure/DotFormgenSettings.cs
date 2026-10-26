using AMFormsCST.Core.Attributes;
using AMFormsCST.Core.Interfaces.Attributes;
using System.Xml;

namespace AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure
{
    public partial class DotFormgenSettings : IEquatable<DotFormgenSettings>, INotifyPropertyChanged
    {
        [NotifyPropertyChanged]
        private int _version;
        [NotifyPropertyChanged]
        private string _uUID;
        [NotifyPropertyChanged]
        private bool _legacyImport;
        [NotifyPropertyChanged]
        private int _totalPages;
        [NotifyPropertyChanged]
        private int _defaultFontSize;
        [NotifyPropertyChanged]
        private bool _missingSourceJpeg;
        [NotifyPropertyChanged]
        private bool _duplex;
        [NotifyPropertyChanged]
        private int _maxAccessoryLines;
        [NotifyPropertyChanged]
        private bool _preprintedLaserForm;

        public DotFormgenSettings() { }

        public DotFormgenSettings(XmlAttributeCollection attributes)
        {
            if (int.TryParse(attributes[0].Value, out int parsedInt))
                Version = parsedInt;

            UUID = attributes[1].Value;

            if (bool.TryParse(attributes[2].Value, out bool parsedBool))
                LegacyImport = parsedBool;

            if (int.TryParse(attributes[3].Value, out parsedInt))
                TotalPages = parsedInt;

            if (int.TryParse(attributes[4].Value, out parsedInt))
                DefaultFontSize = parsedInt;

            if (bool.TryParse(attributes[5].Value, out parsedBool))
                MissingSourceJpeg = parsedBool;

            if (bool.TryParse(attributes[6].Value, out parsedBool))
                Duplex = parsedBool;

            if (int.TryParse(attributes[7].Value, out parsedInt))
                MaxAccessoryLines = parsedInt;

            if (bool.TryParse(attributes[8].Value, out parsedBool))
                PreprintedLaserForm = parsedBool;
        }
        public void GenerateXML(XmlWriter xml)
        {
            xml.WriteAttributeString("version", Version.ToString());
            xml.WriteAttributeString("publishedUUID", UUID);
            xml.WriteAttributeString("legacyImport", LegacyImport.ToString().ToLowerInvariant());
            xml.WriteAttributeString("totalPages", TotalPages.ToString());
            xml.WriteAttributeString("defaultPoints", DefaultFontSize.ToString());
            xml.WriteAttributeString("missingSourceJpeg", MissingSourceJpeg.ToString().ToLowerInvariant());
            xml.WriteAttributeString("duplex", Duplex.ToString().ToLowerInvariant());
            xml.WriteAttributeString("maxAccessoryLines", MaxAccessoryLines.ToString());
            xml.WriteAttributeString("prePrintedLaserForm", PreprintedLaserForm.ToString().ToLowerInvariant());
        }

        public bool Equals(DotFormgenSettings? other) => 
            other is not null &&
            Version == other.Version &&
            (UUID ?? string.Empty).Equals(other.UUID ?? string.Empty, StringComparison.Ordinal) &&
            LegacyImport == other.LegacyImport &&
            TotalPages == other.TotalPages &&
            DefaultFontSize == other.DefaultFontSize &&
            MissingSourceJpeg == other.MissingSourceJpeg &&
            Duplex == other.Duplex &&
            MaxAccessoryLines == other.MaxAccessoryLines &&
            PreprintedLaserForm == other.PreprintedLaserForm;
        public override bool Equals(object? obj) => Equals(obj as DotFormgenSettings);
        public override int GetHashCode() => HashCode.Combine(Version, UUID, LegacyImport, TotalPages, 
            DefaultFontSize, MissingSourceJpeg, Duplex, HashCode.Combine(MaxAccessoryLines, PreprintedLaserForm));
    }

}
