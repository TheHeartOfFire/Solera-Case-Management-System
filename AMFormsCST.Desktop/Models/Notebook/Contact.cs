using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Desktop.BaseClasses;
using CommunityToolkit.Mvvm.ComponentModel;
using Serilog.Context;
using System;
using System.Globalization;

namespace AMFormsCST.Desktop.Models;
public partial class Contact : ManagedObservableCollectionItem
{
    private bool _isInitializing;

    [ObservableProperty]
    private string _name = string.Empty;
    [ObservableProperty]
    private string _email = string.Empty;
    [ObservableProperty]
    private string _phone = string.Empty;
    [ObservableProperty]
    private string _phoneExtension = string.Empty;
    [ObservableProperty]
    private string _phoneExtensionDelimiter = " ";
    public override bool IsBlank { get { return string.IsNullOrEmpty(Name) &&
                                 string.IsNullOrEmpty(Email) &&
                                 string.IsNullOrEmpty(Phone) &&
                                 string.IsNullOrEmpty(PhoneExtension); } }
    public override Guid Id { get; } = Guid.NewGuid();
    internal IContact? CoreType { get; set; }
    internal NoteModel? Parent { get; set; }

    public Contact(string extensionDelimiter, ILogService? logger = null) : base(logger)
    {
        _isInitializing = true;
        PhoneExtensionDelimiter = extensionDelimiter;
        _logger?.LogInfo("Contact initialized.");
        _isInitializing = false;
    }
    public Contact(IContact contact, ILogService? logger = null) : base(logger)
    {
        _isInitializing = true;
        CoreType = contact;
        Name = contact.Name ?? string.Empty;
        Email = contact.Email ?? string.Empty;
        Phone = contact.Phone ?? string.Empty;
        PhoneExtension = contact.PhoneExtension ?? string.Empty;
        PhoneExtensionDelimiter = contact.PhoneExtensionDelimiter;
        _logger?.LogInfo("Contact loaded from core type.");
        _isInitializing = false;
        UpdateCore();
    }

    public string FullPhone
    {
        get
        {
            if (string.IsNullOrEmpty(Phone))
                return string.Empty;
            if (string.IsNullOrEmpty(PhoneExtension))
                return Phone;
            return $"{Phone}{PhoneExtensionDelimiter}{PhoneExtension}";
        }
    }

    public void ParsePhone(string phone)
    { 
        if (string.IsNullOrEmpty(phone))
            return;
        var parts = phone.Split(' ');
        Phone = parts[0];
        if (parts.Length > 1)
            PhoneExtension = parts[1];
        _logger?.LogInfo($"Phone parsed: {Phone}, Extension: {PhoneExtension}");
    }
    partial void OnNameChanged(string value)
    {
        Name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value).Equals(value) ? value : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(value);
        OnPropertyChanged(nameof(IsBlank));
        UpdateCore();
        using (LogContext.PushProperty("ContactId", Id))
        using (LogContext.PushProperty("Name", Name))
        using (LogContext.PushProperty("Email", Email))
        using (LogContext.PushProperty("Phone", Phone))
        using (LogContext.PushProperty("PhoneExtension", PhoneExtension))
        {
            _logger?.LogInfo($"Contact name changed: {Name}");
        }
    }
    partial void OnEmailChanged(string value)
    {
        OnPropertyChanged(nameof(IsBlank));
        UpdateCore();
        using (LogContext.PushProperty("ContactId", Id))
        using (LogContext.PushProperty("Name", Name))
        using (LogContext.PushProperty("Email", value))
        using (LogContext.PushProperty("Phone", Phone))
        using (LogContext.PushProperty("PhoneExtension", PhoneExtension))
        {
            _logger?.LogInfo($"Contact email changed: {value}");
        }
    }
    partial void OnPhoneChanged(string value)
    {
        OnPropertyChanged(nameof(IsBlank));
        OnPropertyChanged(nameof(FullPhone));
        UpdateCore();
        using (LogContext.PushProperty("ContactId", Id))
        using (LogContext.PushProperty("Name", Name))
        using (LogContext.PushProperty("Email", Email))
        using (LogContext.PushProperty("Phone", value))
        using (LogContext.PushProperty("PhoneExtension", PhoneExtension))
        {
            _logger?.LogInfo($"Contact phone changed: {value}");
        }
    } 
    partial void OnPhoneExtensionChanged(string value)
    {
        OnPropertyChanged(nameof(IsBlank));
        OnPropertyChanged(nameof(FullPhone));
        UpdateCore();
        using (LogContext.PushProperty("ContactId", Id))
        using (LogContext.PushProperty("Name", Name))
        using (LogContext.PushProperty("Email", Email))
        using (LogContext.PushProperty("Phone", Phone))
        using (LogContext.PushProperty("PhoneExtension", value))
        {
            _logger?.LogInfo($"Contact phone extension changed: {value}");
        }
    }
    partial void OnPhoneExtensionDelimiterChanged(string value)
    {
        OnPropertyChanged(nameof(FullPhone));
        UpdateCore();
        using (LogContext.PushProperty("ContactId", Id))
        using (LogContext.PushProperty("Name", Name))
        using (LogContext.PushProperty("Email", Email))
        using (LogContext.PushProperty("Phone", Phone))
        using (LogContext.PushProperty("PhoneExtension", PhoneExtension))
        using (LogContext.PushProperty("PhoneExtensionDelimiter", value))
        {
            _logger?.LogInfo($"Contact phone extension delimiter changed: {value}");
        }
    }

    internal void UpdateCore()
    {
        if (_isInitializing) return;

        if (CoreType == null && Parent?.CoreType != null)
            CoreType = Parent.CoreType.Contacts.FirstOrDefault(c => c.Id == Id);
        if (CoreType == null) return;
        CoreType.Name = Name ?? string.Empty;
        CoreType.Email = Email ?? string.Empty;
        CoreType.Phone = Phone ?? string.Empty;
        CoreType.PhoneExtension = PhoneExtension ?? string.Empty;
        CoreType.PhoneExtensionDelimiter = PhoneExtensionDelimiter ?? " ";
        Parent?.UpdateCore();
        _logger?.LogDebug("Contact core updated.");
    }

    public static implicit operator Core.Types.Notebook.Contact(Contact contact)
    {
        if (contact is null) return new Core.Types.Notebook.Contact();

        return new Core.Types.Notebook.Contact(contact.Id)
        {
            Name = contact.Name ?? string.Empty,
            Email = contact.Email ?? string.Empty,
            Phone = contact.Phone ?? string.Empty,
            PhoneExtension = contact.PhoneExtension ?? string.Empty,
            PhoneExtensionDelimiter = contact.PhoneExtensionDelimiter ?? " "
        };
    }
}
