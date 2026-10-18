namespace AMFormsCST.Core.Types.CodeBlocks
{
    public class CodeInput : IComparable<CodeInput>
    {
        public object Value { get; private set; }
        public string Description { get; set; }
        public int Index { get; set; }
        public bool IsExposedInput { get; set; }
        public string? ExposedGroupKey { get; set; }

        public CodeInput(string value, string description, int index)
        {
            Value = value;
            Description = description;
            Index = index;
        }
        public CodeInput(CodeBase value, string description, int index)
        {
            Value = value;
            Description = description;
            Index = index;
        }

        public void SetValue(string value) => Value = value;
        public void SetValue(CodeBase value) => Value = value;

        public int CompareTo(CodeInput? other)
        {
            return other == null ? 1 : Index.CompareTo(other.Index);
        }
    }
}
