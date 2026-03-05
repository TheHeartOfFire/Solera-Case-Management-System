using AMFormsCST.Core.Helpers;
using AMFormsCST.Core.Types.Notebook;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Interfaces.Notebook;
[JsonDerivedType(typeof(Note), typeDiscriminator: "note")]
public interface INote : INotebookItem<INote>
{
    string CaseText { get; set; }
    string NotesText { get; set; }
    string NotesXaml { get; set; }
    SelectableList<IDealer> Dealers { get; set; }
    SelectableList<IContact> Contacts { get; set; }
    SelectableList<IForm> Forms { get; set; }
}