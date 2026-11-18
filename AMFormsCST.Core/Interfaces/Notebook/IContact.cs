using AMFormsCST.Core.Types.Notebook;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Interfaces.Notebook;
[JsonDerivedType(typeof(Contact), typeDiscriminator: "contact")]
public interface IContact : INotebookItem<IContact>
{
    string Name { get; set; }
    string Email { get; set; } 
    string Phone { get; set; } 
    string PhoneExtension { get; set; }
    string PhoneExtensionDelimiter { get; set; }
    string Role { get; set; }
    string Notes { get; set; }
    bool IsPrimary { get; set; }
}
