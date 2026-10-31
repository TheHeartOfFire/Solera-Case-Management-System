using AMFormsCST.Core.Attributes;
using AMFormsCST.Core.Interfaces.Attributes;
using System.Drawing;
using System.Xml;

namespace AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure
{
    public partial class FormFieldSettings : IEquatable<FormFieldSettings>, INotifyPropertyChanged
    {
        [NotifyPropertyChanged]
        private int _iD;
        [NotifyPropertyChanged]
        private FieldType _type;
        [NotifyPropertyChanged]
        private Point _impactPosition;
        [NotifyPropertyChanged]
        private Rectangle _laserRect;
        [NotifyPropertyChanged]
        private bool _manualSize;
        [NotifyPropertyChanged]
        private int _fontSize;
        [NotifyPropertyChanged]
        private bool _bold;
        [NotifyPropertyChanged]
        private bool _shrinkToFit;
        [NotifyPropertyChanged]
        private int _length;
        [NotifyPropertyChanged]
        private int _decimalPlaces;
        [NotifyPropertyChanged]
        private bool _displayPartial;
        [NotifyPropertyChanged]
        private int _startIndex;
        [NotifyPropertyChanged]
        private int _endIndex;
        [NotifyPropertyChanged]
        private int _kearning;
        [NotifyPropertyChanged]
        private Alignment _fontAlignment;

        public enum FieldType
        {
            TEXT,
            NUMERIC,
            SIGNATURE,
            INITIALS
        }

        public enum Alignment
        {
            LEFT,
            CENTER,
            RIGHT
        }

        public FormFieldSettings() { }

        public FormFieldSettings(FormFieldSettings other, int newId = 0)
        {
            ID = newId != 0 ? newId : other.ID;
            Type = other.Type;
            ImpactPosition = other.ImpactPosition;
            LaserRect = other.LaserRect;
            ManualSize = other.ManualSize;
            FontSize = other.FontSize;
            Bold = other.Bold;
            ShrinkToFit = other.ShrinkToFit;
            Length = other.Length;
            DecimalPlaces = other.DecimalPlaces;
            DisplayPartial = other.DisplayPartial;
            StartIndex = other.StartIndex;
            EndIndex = other.EndIndex;
            Kearning = other.Kearning;
            FontAlignment = other.FontAlignment;
        }

        public FormFieldSettings(XmlAttributeCollection attributes)
        {
            if (int.TryParse(attributes[0].Value, out int parsedInt))
                ID = parsedInt;

            Type = GetFieldType(attributes[1].Value);

            var pos = new Point();

            if (int.TryParse(attributes[2].Value, out parsedInt))
                pos.X = parsedInt;

            if (int.TryParse(attributes[3].Value, out parsedInt))
                pos.Y = parsedInt;

            ImpactPosition = pos;

            var rect = new Rectangle();

            if (int.TryParse(attributes[4].Value, out parsedInt))
                rect.X = parsedInt;

            if (int.TryParse(attributes[5].Value, out parsedInt))
                rect.Y = parsedInt;

            if (int.TryParse(attributes[6].Value, out parsedInt))
                rect.Width = parsedInt;

            if (int.TryParse(attributes[7].Value, out parsedInt))
                rect.Height = parsedInt;

            LaserRect = rect;

            if (bool.TryParse(attributes[8].Value, out bool parsedBool))
                ManualSize = parsedBool;

            if (int.TryParse(attributes[9].Value, out parsedInt))
                FontSize = parsedInt;

            if (bool.TryParse(attributes[10].Value, out parsedBool))
                Bold = parsedBool;

            if (bool.TryParse(attributes[11].Value, out parsedBool))
                ShrinkToFit = parsedBool;

            if (int.TryParse(attributes[12].Value, out parsedInt))
                Length = parsedInt;

            if (int.TryParse(attributes[13].Value, out parsedInt))
                DecimalPlaces = parsedInt;

            if (bool.TryParse(attributes[14].Value, out parsedBool))
                DisplayPartial = parsedBool;

            if (int.TryParse(attributes[15].Value, out parsedInt))
                StartIndex = parsedInt;

            if (int.TryParse(attributes[16].Value, out parsedInt))
                EndIndex = parsedInt;

            if (int.TryParse(attributes[17].Value, out parsedInt))
                Kearning = parsedInt;

            FontAlignment = GetAlignment(attributes[18].Value);
        }

        public static FieldType GetFieldType(string type) => type switch
        {
            "TEXT" => FieldType.TEXT,
            "NUMERIC" => FieldType.NUMERIC,
            "SIGNATURE" => FieldType.SIGNATURE,
            "Initials" => FieldType.INITIALS,
            _ => FieldType.TEXT,
        };
        public static string GetFieldType(FieldType type) => type switch
        {
            FieldType.TEXT => "TEXT",
            FieldType.NUMERIC => "NUMERIC",
            FieldType.SIGNATURE => "SIGNATURE",
            FieldType.INITIALS => "Initials",
            _ => "TEXT",
        };

        public static Alignment GetAlignment(string alignment) => alignment switch
        {
            "Left" => Alignment.LEFT,
            "Center" => Alignment.CENTER,
            "Right" => Alignment.RIGHT,
            _ => Alignment.LEFT,
        };

        public static string GetAlignment(Alignment alignment) => alignment switch
        {
            Alignment.LEFT => "Left",
            Alignment.CENTER => "Center",
            Alignment.RIGHT => "Right",
            _ => "Left",
        };

        public void GenerateXml(XmlWriter xml)
        {
            xml.WriteAttributeString("uniqueId", ID.ToString());
            xml.WriteAttributeString("formFieldType", GetFieldType(Type));
            xml.WriteAttributeString("legacyCol", ImpactPosition.X.ToString());
            xml.WriteAttributeString("legacyLine", ImpactPosition.Y.ToString());
            xml.WriteAttributeString("x", LaserRect.X.ToString());
            xml.WriteAttributeString("y", LaserRect.Y.ToString());
            xml.WriteAttributeString("w", LaserRect.Width.ToString());
            xml.WriteAttributeString("h", LaserRect.Height.ToString());
            xml.WriteAttributeString("manualSize", ManualSize.ToString().ToLowerInvariant());
            xml.WriteAttributeString("fontPoints", FontSize.ToString());
            xml.WriteAttributeString("boldFont", Bold.ToString().ToLowerInvariant());
            xml.WriteAttributeString("shrinkFontToFit", ShrinkToFit.ToString().ToLowerInvariant());
            xml.WriteAttributeString("pictureLeft", Length.ToString());
            xml.WriteAttributeString("pictureRight", DecimalPlaces.ToString());
            xml.WriteAttributeString("displayPartialField", DisplayPartial.ToString().ToLowerInvariant());
            xml.WriteAttributeString("startChar", StartIndex.ToString());
            xml.WriteAttributeString("endChar", EndIndex.ToString());
            xml.WriteAttributeString("perCharDeltaPts", Kearning.ToString());
            xml.WriteAttributeString("alignment", GetAlignment(FontAlignment));

        }

        public bool Equals(FormFieldSettings? other) => other is not null &&
            ID == other.ID &&
            Type == other.Type &&
            ImpactPosition.Equals(other.ImpactPosition) &&
            LaserRect.Equals(other.LaserRect) &&
            ManualSize == other.ManualSize &&
            FontSize == other.FontSize &&
            Bold == other.Bold &&
            ShrinkToFit == other.ShrinkToFit &&
            Length == other.Length &&
            DecimalPlaces == other.DecimalPlaces &&
            DisplayPartial == other.DisplayPartial &&
            StartIndex == other.StartIndex &&
            EndIndex == other.EndIndex &&
            Kearning == other.Kearning &&
            FontAlignment == other.FontAlignment;

        public override bool Equals(object? obj) => Equals(obj as FormFieldSettings);
        public override int GetHashCode() => HashCode.Combine(ID, Type, ImpactPosition, LaserRect, ManualSize, FontSize, Bold, 
            HashCode.Combine(ShrinkToFit, Length, DecimalPlaces, DisplayPartial, StartIndex,  EndIndex, Kearning, FontAlignment));
    }
}
