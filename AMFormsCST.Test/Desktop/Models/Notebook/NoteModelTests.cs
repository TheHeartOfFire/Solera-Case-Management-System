using AMFormsCST.Desktop.Interfaces;
using AMFormsCST.Desktop.Models;
using System.Linq;
using System.Windows.Documents;
using System.Windows.Markup;
using Xunit;
using CoreNote = AMFormsCST.Core.Types.Notebook.Note;

namespace AMFormsCST.Test.Desktop.Models.Notebook;

public class NoteModelTests
{
    private const string TestExtSeparator = "x";

    [Fact]
    public void Constructor_InitializesCollections_WithOneBlankItemEach()
    {
        // Arrange & Act
        var note = new NoteModel(TestExtSeparator, null);

        // Assert
        Assert.Single(note.Dealers);
        Assert.True(note.Dealers[0].IsBlank);

        Assert.Single(note.Contacts);
        Assert.True(note.Contacts[0].IsBlank);

        Assert.Single(note.Forms);
        Assert.True(note.Forms[0].IsBlank);
    }

    [Fact]
    public void Constructor_SelectsDefaultBlankItems()
    {
        // Arrange & Act
        var note = new NoteModel(TestExtSeparator, null);

        // Assert
        Assert.NotNull(note.SelectedDealer);
        Assert.Same(note.Dealers[0], note.SelectedDealer);
        Assert.Equal(IManagedObservableCollectionItem.CollectionMemberState.Selected, note.SelectedDealer.State);

        Assert.NotNull(note.SelectedContact);
        Assert.Same(note.Contacts[0], note.SelectedContact);
        Assert.Equal(IManagedObservableCollectionItem.CollectionMemberState.Selected, note.SelectedContact.State);

        Assert.NotNull(note.SelectedForm);
        Assert.Same(note.Forms[0], note.SelectedForm);
        Assert.Equal(IManagedObservableCollectionItem.CollectionMemberState.Selected, note.SelectedForm.State);
    }

    [Fact]
    public void IsBlank_IsTrue_ForNewModel()
    {
        // Arrange
        var note = new NoteModel(TestExtSeparator, null);

        // Assert
        Assert.True(note.IsBlank);
    }

    [Theory]
    [InlineData("12345", null)]
    [InlineData(null, "Some notes")]
    public void IsBlank_IsFalse_WhenTopLevelPropertiesAreSet(string? caseNumber, string? notes)
    {
        // Arrange
        var note = new NoteModel(TestExtSeparator, null);

        // Act
        note.CaseNumber = caseNumber;
        if (notes != null)
            note.NotesXaml = XamlWriter.Save(new FlowDocument(new Paragraph(new Run(notes))));

        // Assert
        Assert.False(note.IsBlank);
    }

    [Fact]
    public void IsBlank_IsFalse_WhenChildItemBecomesNonBlank()
    {
        // Arrange
        var note = new NoteModel(TestExtSeparator, null);
        Assert.True(note.IsBlank); // Pre-condition check

        // Act
        note.Dealers[0].Name = "Test Dealer";

        // Assert
        Assert.False(note.IsBlank);
    }

    [Fact]
    public void SelectingAnItem_UpdatesSelectedProperty_AndItemState()
    {
        // Arrange
        var note = new NoteModel(TestExtSeparator, null);
        var dealer1 = note.Dealers[0];
        dealer1.Name = "Dealer 1"; // Makes it non-blank, collection adds a new blank item
        var dealer2 = note.Dealers.Last(d => d.IsBlank);

        // Act
        dealer2.Select();

        // Assert
        Assert.Same(dealer2, note.SelectedDealer);
        Assert.Equal(IManagedObservableCollectionItem.CollectionMemberState.Selected, dealer2.State);
        Assert.Equal(IManagedObservableCollectionItem.CollectionMemberState.NotSelected, dealer1.State);
    }

    [Fact]
    public void ImplicitConversion_ToCoreNote_MapsPropertiesCorrectly()
    {
        // Arrange
        var noteModel = new NoteModel(TestExtSeparator, null)
        {
            CaseNumber = "98765",
            NotesXaml = XamlWriter.Save(new FlowDocument(new Paragraph(new Run("Test case notes."))))
        };

        var dealer = new Dealer(null) { Name = "Test Dealer", ServerCode = "SVR1" };
        var company = new Company(null) { Name = "Company 1", CompanyCode = "C1" };
        dealer.Companies.Add(company);
        noteModel.Dealers.Add(dealer);

        var contact = new Contact(TestExtSeparator, null) { Name = "John Doe", Email = "john.doe@email.com" };
        noteModel.Contacts.Add(contact);

        var form = new Form(null) { Name = "MyForm.frp" };
        var testDeal = new TestDeal(null) { DealNumber = "Deal123" };
        form.TestDeals.Add(testDeal);
        noteModel.Forms.Add(form);

        // Act
        CoreNote coreNote = noteModel;

        // Assert
        Assert.Equal("98765", coreNote.CaseText);
        Assert.Equal("Test case notes.", coreNote.NotesText.Trim());

        Assert.Equal(2, coreNote.Dealers.Count);
        var coreDealer = coreNote.Dealers.FirstOrDefault(d => !string.IsNullOrEmpty(d.Name));
        Assert.NotNull(coreDealer);
        Assert.Equal("Test Dealer", coreDealer.Name);
        Assert.Equal("SVR1", coreDealer.ServerCode);
        Assert.Equal(2, coreDealer.Companies.Count);
        var coreCompany = coreDealer.Companies.FirstOrDefault(c => !string.IsNullOrEmpty(c.Name));
        Assert.NotNull(coreCompany);
        Assert.Equal("Company 1", coreCompany.Name);

        Assert.Equal(2, coreNote.Contacts.Count);
        var coreContact = coreNote.Contacts.FirstOrDefault(c => !string.IsNullOrEmpty(c.Name));
        Assert.NotNull(coreContact);
        Assert.Equal("John Doe", coreContact.Name);

        Assert.Equal(2, coreNote.Forms.Count);
        var coreForm = coreNote.Forms.FirstOrDefault(f => !string.IsNullOrEmpty(f.Name));
        Assert.NotNull(coreForm);
        Assert.Equal("MyForm.frp", coreForm.Name);
        Assert.Equal(2, coreForm.TestDeals.Count);
        var coreTestDeal = coreForm.TestDeals.FirstOrDefault(td => !string.IsNullOrEmpty(td.DealNumber));
        Assert.NotNull(coreTestDeal);
        Assert.Equal("Deal123", coreTestDeal.DealNumber);
    }

    [Fact]
    public void UpdateCore_UpdatesCoreType_WhenPropertiesChange()
    {
        // Arrange
        var noteModel = new NoteModel(TestExtSeparator, null);
        var coreNote = new CoreNote();
        noteModel.CoreType = coreNote;

        // Act
        noteModel.CaseNumber = "CS123";
        noteModel.NotesXaml = XamlWriter.Save(new FlowDocument(new Paragraph(new Run("Test Notes"))));

        // Assert
        Assert.Equal("CS123", coreNote.CaseText);
        Assert.Equal("Test Notes", coreNote.NotesText.Trim());
    }
}