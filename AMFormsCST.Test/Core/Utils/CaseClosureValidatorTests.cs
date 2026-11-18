using AMFormsCST.Core.Types.Notebook;
using AMFormsCST.Core.Utils;
using Xunit;

namespace AMFormsCST.Test.Core.Utils;

public class CaseClosureValidatorTests
{
    private static Note CreateValidNote(string? contactEmail = "john@dealer.com", string? notesText = "")
    {
        var note = new Note
        {
            CaseText = "00123456",
            NotesText = notesText ?? string.Empty
        };
        note.Dealers.Add(new Dealer { Name = "Apex Honda", ServerCode = "SVR1" });
        note.Contacts.Add(new Contact { Name = "John Doe", Email = contactEmail ?? string.Empty, IsPrimary = true });
        note.LogEntries.Add(new CaseLogEntry
        {
            Action = CaseActionType.LineAdjustments,
            FormName = "F102",
            Details = "Adjusted lines",
            IncludeInOutput = true
        });
        return note;
    }

    [Fact]
    public void Validate_AllFieldsPresent_ReturnsIsValidTrue()
    {
        var result = CaseClosureValidator.Validate(new CaseValidationRequest
        {
            Note = CreateValidNote(),
            TargetStatus = CaseTargetStatus.Resolved,
            ParentProduct = "Auto/Mate Forms",
            CaseReason = "Form Modification",
            SubCategory = "Line Adjustments",
            SupportCategory = "F&I Forms",
            ClosureComment = "Completed and resolved."
        });

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Validate_ResolvedWithoutContactEmail_FindsFallbackEmailInDescription()
    {
        var result = CaseClosureValidator.Validate(new CaseValidationRequest
        {
            Note = CreateValidNote(contactEmail: "", notesText: "Please email copy to jane@apex.com when done."),
            TargetStatus = CaseTargetStatus.Resolved,
            ParentProduct = "Auto/Mate Forms",
            CaseReason = "Form Modification",
            SubCategory = "Line Adjustments",
            SupportCategory = "F&I Forms",
            ClosureComment = "Resolved."
        });

        Assert.False(result.IsValid);
        Assert.Equal("jane@apex.com", result.FallbackEmailFromDescription);
        Assert.Contains(result.Issues, i => i.FieldName == "Contact Email");
    }

    [Fact]
    public void Validate_ClosedWithoutContactEmail_DoesNotRequireEmail()
    {
        var result = CaseClosureValidator.Validate(new CaseValidationRequest
        {
            Note = CreateValidNote(contactEmail: ""),
            TargetStatus = CaseTargetStatus.Closed,
            ParentProduct = "Auto/Mate Forms",
            CaseReason = "Form Modification",
            SubCategory = "Line Adjustments",
            SupportCategory = "F&I Forms",
            ClosureComment = "Closed."
        });

        Assert.True(result.IsValid);
    }
}

