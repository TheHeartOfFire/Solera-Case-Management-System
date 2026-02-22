using AMFormsCST.Core.Helpers;
using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Types.Notebook;
[JsonDerivedType(typeof(Note), typeDiscriminator: "note")]
public class Note : INote
{
    private readonly ILogService? _logger;

    public string CaseText { get; set; } = string.Empty;
    public string NotesText { get; set; } = string.Empty;
    public string NotesXaml { get; set; } = string.Empty;

    public SelectableList<IDealer> Dealers { get; set; }
    public SelectableList<IContact> Contacts { get; set; }
    public SelectableList<IForm> Forms { get; set; }

    public Guid Id => _id;

    public Note() : this(null) { }

    public Note(ILogService? logger)
    {
        _logger = logger;
        Dealers = new SelectableList<IDealer>(_logger);
        Contacts = new SelectableList<IContact>(_logger);
        Forms = new SelectableList<IForm>(_logger);
        _logger?.LogInfo($"Note initialized. Id: {_id}");
    }

    public Note(Guid id) : this(null)
    {
        _id = id;
        _logger?.LogInfo($"Note initialized with custom Id: {_id}");
    }

    #region Interface Implementation
    private readonly Guid _id = Guid.NewGuid();

    public INote Clone()
    {
        var clone = new Note(_logger)
        {
            CaseText = CaseText,
            NotesText = NotesText,
            NotesXaml = NotesXaml,
            Dealers = new SelectableList<IDealer>(Dealers.Select(d => d.Clone()), _logger),
            Contacts = new SelectableList<IContact>(Contacts.Select(c => c.Clone()), _logger),
            Forms = new SelectableList<IForm>(Forms.Select(f => f.Clone()), _logger)
        };
        _logger?.LogInfo($"Note cloned. Original Id: {_id}, Clone Id: {clone.Id}");
        return clone;
    }

    public bool Equals(INote? other)
    {
        if (other == null) return false;
        return _id == other.Id;
    }
    public override bool Equals(object? obj)
    {
        if (obj is INote note)
            return Equals(note);
        return false;
    }
    public override int GetHashCode()
    {
        return _id.GetHashCode();
    }

    public bool Equals(INote? x, INote? y)
    {
        if (x is null && y is null) return true;
        if (x is null || y is null) return false;
        return x.Id == y.Id;
    }

    public int GetHashCode([DisallowNull] INote obj) => obj.Id.GetHashCode();

    public string Dump() 
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Note Dump:");
        sb.AppendLine($"Id: {_id}");
        sb.AppendLine($"CaseText: {CaseText}");
        sb.AppendLine($"NotesText: {NotesText}");
        sb.AppendLine($"Dealers: {string.Join(", ", Dealers.Select(d => d.Dump()))}");
        sb.AppendLine($"Contacts: {string.Join(", ", Contacts.Select(c => c.Dump()))}");
        sb.AppendLine($"Forms: {string.Join(", ", Forms.Select(f => f.Dump()))}");
        _logger?.LogDebug($"Note Dump called for Id: {_id}");
        return sb.ToString();
    }
    #endregion

}
