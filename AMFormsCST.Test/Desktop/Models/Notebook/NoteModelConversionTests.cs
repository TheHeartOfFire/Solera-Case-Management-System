using AMFormsCST.Desktop.Models;
using System;
using System.Linq;
using CoreNote = AMFormsCST.Core.Types.Notebook.Note;
using Assert = Xunit.Assert;
using System.Windows.Documents;
using System.Windows.Markup;

namespace AMFormsCST.Test.Desktop.Models.Notebook;

public class NoteModelConversionTests
{
    private const string TestExtSeparator = "x";

    [Fact]
    public void ImplicitConversion_CorrectlyMapsAllPropertiesAndCollections()
    {
        // Arrange
        var noteModel = new NoteModel(TestExtSeparator)
        {
            CaseNumber = "CS12345",
            NotesXaml = XamlWriter.Save(new FlowDocument(new Paragraph(new Run("This is a test note.")))),
        };

        // Populate a dealer and its company
        var dealer = new Dealer
        {
            ServerCode = "SVR1",
            Name = "Test Dealership"
        };
        var company = new Company { CompanyCode = "C1", Name = "Company One" };
        dealer.Companies.Add(company);
        noteModel.Dealers.Add(dealer);

        // Populate a contact
        var contact = new Contact(TestExtSeparator)
        {
            Name = "John Doe",
            Email = "john.doe@example.com",
            Phone = "555-1234",
            PhoneExtension = "101"
        };
        noteModel.Contacts.Add(contact);

        // Populate a form and its deal
        var form = new Form(noteModel) { Name = "MyForm.frp" };
        var testDeal = new TestDeal { DealNumber = "DEAL99" };
        form.TestDeals.Add(testDeal);
        noteModel.Forms.Add(form);

        // Act
        CoreNote coreNote = noteModel;

        // Assert
        Assert.NotNull(coreNote);
        Assert.Equal("CS12345", coreNote.CaseText);
        Assert.Equal("This is a test note.", coreNote.NotesText.Trim());

        // Assert Dealer and Company (expecting 2 items: 1 blank, 1 with data)
        Assert.Equal(2, coreNote.Dealers.Count);
        var coreDealer = coreNote.Dealers.FirstOrDefault(d => !string.IsNullOrEmpty(d.Name));
        Assert.NotNull(coreDealer);
        Assert.Equal("SVR1", coreDealer.ServerCode);
        Assert.Equal("Test Dealership", coreDealer.Name);
        Assert.Equal(2, coreDealer.Companies.Count);
        var coreCompany = coreDealer.Companies.FirstOrDefault(c => !string.IsNullOrEmpty(c.Name));
        Assert.NotNull(coreCompany);
        Assert.Equal("C1", coreCompany.CompanyCode);
        Assert.Equal("Company One", coreCompany.Name);

        // Assert Contact (expecting 2 items: 1 blank, 1 with data)
        Assert.Equal(2, coreNote.Contacts.Count);
        var coreContact = coreNote.Contacts.FirstOrDefault(c => !string.IsNullOrEmpty(c.Name));
        Assert.NotNull(coreContact);
        Assert.Equal("John Doe", coreContact.Name);
        Assert.Equal("john.doe@example.com", coreContact.Email);
        Assert.Equal("555-1234", coreContact.Phone);
        Assert.Equal("101", coreContact.PhoneExtension);

        // Assert Form and TestDeal (expecting 2 items: 1 blank, 1 with data)
        Assert.Equal(2, coreNote.Forms.Count);
        var coreForm = coreNote.Forms.FirstOrDefault(f => !string.IsNullOrEmpty(f.Name));
        Assert.NotNull(coreForm);
        Assert.Equal("MyForm.frp", coreForm.Name);
        Assert.Equal(2, coreForm.TestDeals.Count);
        var coreTestDeal = coreForm.TestDeals.FirstOrDefault(td => !string.IsNullOrEmpty(td.DealNumber));
        Assert.NotNull(coreTestDeal);
        Assert.Equal("DEAL99", coreTestDeal.DealNumber);
    }

    [Fact]
    public void ImplicitConversion_WithNullNoteModel_ReturnsNewCoreNote()
    {
        // Arrange
        NoteModel? noteModel = null;

        // Act
        CoreNote coreNote = noteModel;

        // Assert
        Assert.NotNull(coreNote);
        Assert.NotEqual(Guid.Empty, coreNote.Id);
        Assert.Equal(string.Empty, coreNote.CaseText);
        Assert.Empty(coreNote.Dealers);
        Assert.Empty(coreNote.Contacts);
        Assert.Empty(coreNote.Forms);
    }

    [Fact]
    public void ImplicitConversion_WithBlankNoteModel_MapsToCoreNoteWithBlankItems()
    {
        // Arrange
        // A new NoteModel is blank by default, but contains blank child items in its collections.
        // The conversion should include these blank children.
        var noteModel = new NoteModel(TestExtSeparator);

        // Act
        CoreNote coreNote = noteModel;

        // Assert
        Assert.NotNull(coreNote);
        Assert.Equal(string.Empty, coreNote.CaseText);
        Assert.Equal(string.Empty, coreNote.NotesText);

        // Assert that each collection contains exactly one item, which is the blank one.
        Assert.Single(coreNote.Dealers);
        Assert.True(string.IsNullOrEmpty(coreNote.Dealers.First().Name));

        Assert.Single(coreNote.Contacts);
        Assert.True(string.IsNullOrEmpty(coreNote.Contacts.First().Name));

        Assert.Single(coreNote.Forms);
        Assert.True(string.IsNullOrEmpty(coreNote.Forms.First().Name));
    }
}