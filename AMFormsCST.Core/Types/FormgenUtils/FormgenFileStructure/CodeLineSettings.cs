using AMFormsCST.Core.Attributes;
using AMFormsCST.Core.Interfaces.Attributes;
using System.Xml;

namespace AMFormsCST.Core.Types.FormgenUtils.FormgenFileStructure
{
    public partial class CodeLineSettings : IEquatable<CodeLineSettings>, INotifyPropertyChanged
    {
        [NotifyPropertyChanged]
        private int _order;
        [NotifyPropertyChanged]
        private CodeType _type = CodeType.PROMPT;
        [NotifyPropertyChanged]
        private string? _variable;
        public enum CodeType
        {
            INIT,
            PROMPT,
            POST
        }
        public CodeLineSettings(XmlAttributeCollection attributes)
        {
            if (int.TryParse(attributes[0].Value, out int parsedInt))
                Order = parsedInt;

            Type = GetCodeType(attributes[1].Value);

            Variable = attributes[2].Value;
        }
        public CodeLineSettings(CodeLineSettings settings, string? newName, int newIndex)
        {
            Order = newIndex;
            Type = settings.Type;
            Variable = newName;
        }
        public CodeLineSettings() { }


        public static CodeType GetCodeType(string type) => type switch
        {
            "INIT" => CodeType.INIT,
            "PROMPT" => CodeType.PROMPT,
            "POST" => CodeType.POST,
            _ => CodeType.PROMPT,
        };
        public static string GetCodeType(CodeType type) => type switch
        {
            CodeType.INIT => "INIT",
            CodeType.PROMPT => "PROMPT",
            CodeType.POST => "POST",
            _ => "PROMPT",
        };

        internal void GenerateXml(XmlWriter xml)
        {
            xml.WriteAttributeString("order", Order.ToString());
            xml.WriteAttributeString("type", GetCodeType(Type));
            xml.WriteAttributeString("destVariable", Variable);
        }

        public bool Equals(CodeLineSettings? other) => 
            other is not null &&
            Order == other.Order &&
            Type == other.Type &&
            (Variable ?? string.Empty).Equals(other.Variable ?? string.Empty, StringComparison.Ordinal);


        public override bool Equals(object? obj) => Equals(obj as CodeLineSettings);
        

        public override int GetHashCode() => HashCode.Combine(Order, Type, Variable);
    }
}
