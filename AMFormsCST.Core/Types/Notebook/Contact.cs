using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Interfaces;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json.Serialization;

namespace AMFormsCST.Core.Types.Notebook;
[JsonDerivedType(typeof(Contact), typeDiscriminator: "contact")]
public class Contact : IContact
{
    private readonly ILogService? _logger;

    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string PhoneExtension { get; set; } = string.Empty;
    public string PhoneExtensionDelimiter { get; set; } = " ";
    public string Role { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public Guid Id => _id;

    public Contact() : this(null) { }
    public Contact(ILogService? logger)
    {
        _logger = logger;
        _logger?.LogInfo($"Contact initialized. Id: {_id}");
    }
    public Contact(Guid id, ILogService? logger = null) : this(logger)
    {
        _id = id;
        _logger?.LogInfo($"Contact initialized with custom Id: {_id}");
    }

    #region Interface Implementation
    private readonly Guid _id = Guid.NewGuid();

    public string Dump()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Id: {Id}");
        sb.AppendLine($"Name: {Name}");
        sb.AppendLine($"Email: {Email}");
        sb.AppendLine($"Phone: {Phone}");
        sb.AppendLine($"Phone Extension: {PhoneExtension}");
        sb.AppendLine($"Phone Extension Delimiter: {PhoneExtensionDelimiter}");
        sb.AppendLine($"Role: {Role}");
        sb.AppendLine($"Notes: {Notes}");
        sb.AppendLine($"IsPrimary: {IsPrimary}");
        _logger?.LogDebug($"Contact Dump called for Id: {Id}");
        return sb.ToString();
    }

    public bool Equals(IContact? other)
    {
        if (other is null && this is null) return true;
        if (other is null || this is null) return false;
        return _id == other.Id;
    }

    public bool Equals(IContact? x, IContact? y)
    {
        if (x is null && y is null) return true;
        if (x is null || y is null) return false;
        return x.Equals(y);
    }

    public int GetHashCode([DisallowNull] IContact obj) => obj.Id.GetHashCode();

    public IContact Clone()
    {
        if (this is null)
        {
            var ex = new ArgumentNullException(nameof(IContact), "Cannot clone a null item.");
            _logger?.LogError("Attempted to clone a null Contact.", ex);
            throw ex;
        }
        var clone = new Contact(_logger)
        {
            Name = Name,
            Email = Email,
            Phone = Phone,
            PhoneExtension = PhoneExtension,
            PhoneExtensionDelimiter = PhoneExtensionDelimiter,
            Role = Role,
            Notes = Notes,
            IsPrimary = IsPrimary
        };
        _logger?.LogInfo($"Contact cloned. Original Id: {_id}, Clone Id: {clone.Id}");
        return clone;
    }
    #endregion
}
