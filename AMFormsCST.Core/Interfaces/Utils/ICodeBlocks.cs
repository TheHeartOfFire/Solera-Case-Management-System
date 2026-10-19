using AMFormsCST.Core.Interfaces.CodeBlocks;

namespace AMFormsCST.Core.Interfaces.Utils;
public interface ICodeBlocks
{
    IList<ICodeBase> CustomBlocks { get; set; }

    IReadOnlyCollection<ICodeBase> GetBlocks();
    void SaveCustomBlocks() { }
    void LoadCustomBlocks() { }
}