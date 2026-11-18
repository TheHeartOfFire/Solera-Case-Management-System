using AMFormsCST.Core.Converters;
using AMFormsCST.Core.Interfaces.Notebook;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Core.Types.Notebook;
using AMFormsCST.Core.Utils;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace AMFormsCST.Test.Core.Utils;

public class TemplateAssemblyServiceTests
{
    private readonly TemplateAssemblyService _service = new();

    private Note CreateSampleNote()
    {
        var note = new Note
        {
            CaseText = "Case Number: 00123456\r\nSubject: Need line adjustments"
        };

        var dealer = new Dealer
        {
            Name = "Apex Automotive Group",
            ServerCode = "SVR1"
        };
        note.Dealers.Add(dealer);

        var contact1 = new Contact
        {
            Name = "John Doe",
            Email = "jdoe@apex.com",
            Role = "Finance Director",
            Notes = "Off on Fridays",
            IsPrimary = true
        };
        var contact2 = new Contact
        {
            Name = "Sarah Smith",
            Email = "ssmith@apex.com",
            Role = "Asst Controller",
            Notes = "Friday POC",
            IsPrimary = false
        };
        note.Contacts.Add(contact1);
        note.Contacts.Add(contact2);

        note.LinkedCases.Add("00123457");
        note.LinkedCases.Add("00123480");

        var form = new Form
        {
            Name = "F102 - Buyers Order",
            Notable = true
        };
        note.Forms.Add(form);

        var entry1 = new CaseLogEntry
        {
            Action = CaseActionType.LineAdjustments,
            FormName = "F102 - Buyers Order",
            IsCustomCopy = true,
            Stores = [new() { CompanyCode = "07", StoreName = "Apex Nissan" }],
            Deals = [new() { DealNumber = "54321", Purpose = "Retail, Store 07" }, new() { DealNumber = "67890", Purpose = "Lease" }],
            Details = "Shifted lines 14-16 down 2pt",
            IncludeInOutput = true
        };

        var entry2 = new CaseLogEntry
        {
            Action = CaseActionType.NewFormPublish,
            FormName = "F205 - Odometer",
            IsCustomCopy = false,
            Stores = [new() { CompanyCode = "01", StoreName = "Honda" }, new() { CompanyCode = "02", StoreName = "Toyota" }],
            Details = "Added to DMS menu",
            IncludeInOutput = true
        };

        note.LogEntries.Add(entry1);
        note.LogEntries.Add(entry2);

        return note;
    }

    [Fact]
    public void AssembleInternalNote_ProducesCleanStructuredWorkLog()
    {
        var note = CreateSampleNote();
        var result = _service.AssembleInternalNote(note);

        Assert.Contains("=== SCMT Work Log ===", result);
        Assert.Contains("Case: #00123456", result);
        Assert.Contains("Server: SVR1", result);
        Assert.Contains("Dealer: Apex Automotive Group", result);
        Assert.Contains("Linked Cases: #00123457, #00123480", result);
        Assert.Contains("Contacts: John Doe (Finance Director) - Off on Fridays", result);
        Assert.Contains("• [Line Adjustments] F102 - Buyers Order [Custom Copy] -> Stores: 07 - Apex Nissan", result);
        Assert.Contains("Deals: 54321 (Retail, Store 07), 67890 (Lease)", result);
        Assert.Contains("Shifted lines 14-16 down 2pt", result);
        Assert.Contains("• [New form publish] F205 - Odometer -> Stores: 01 - Honda, 02 - Toyota", result);
        Assert.Contains("====================", result);
    }

    [Fact]
    public void AssembleDealerEmail_UsesPrimaryContactFirstNameAndStoreGrouping()
    {
        var note = CreateSampleNote();
        var email = _service.AssembleDealerEmail(note);

        Assert.StartsWith("Hello John,", email);
        Assert.Contains("Store(s) 07 - Apex Nissan: [Line Adjustments] F102 - Buyers Order (Custom Copy)", email);
        Assert.Contains("Tested on deal(s): 54321 (Retail, Store 07), 67890 (Lease)", email);
        Assert.Contains("Store(s) 01 - Honda, 02 - Toyota: [New form publish] F205 - Odometer", email);
        Assert.Contains("Thank You,", email);
    }

    [Fact]
    public void AssemblePublishNote_IncludesActionCaseAndStores()
    {
        var note = CreateSampleNote();
        var publishNote = _service.AssemblePublishNote(note);

        Assert.Contains("New form publish per Case #00123456", publishNote);
        Assert.Contains("Stores: 01 - Honda, 02 - Toyota", publishNote);
    }

    [Fact]
    public void AssembleClosureRecap_IncludesCorporateFraming()
    {
        var note = CreateSampleNote();
        var closure = _service.AssembleClosureRecap(note);

        Assert.Contains("Hi John!", closure);
        Assert.Contains("00123456", closure);
        Assert.Contains("survey and we didn’t earn a perfect score", closure);
    }

    [Fact]
    public void TextTemplateJsonConverter_RoundTripsKindTagsAndAction()
    {
        var template = new TextTemplate(
            "Test Partial",
            "A test partial block",
            "<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Paragraph>Test Text</Paragraph></FlowDocument>",
            TextTemplate.TemplateType.Email,
            TextTemplate.TemplateCategoryKind.ActionPartial,
            ["Email", "LineAdjustments", "Apex"],
            CaseActionType.LineAdjustments
        );

        var options = new JsonSerializerOptions();
        options.Converters.Add(new TextTemplateJsonConverter());

        var json = JsonSerializer.Serialize(template, options);
        var deserialized = JsonSerializer.Deserialize<TextTemplate>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal(template.Name, deserialized.Name);
        Assert.Equal(TextTemplate.TemplateCategoryKind.ActionPartial, deserialized.Kind);
        Assert.Equal(3, deserialized.Tags.Count);
        Assert.Contains("LineAdjustments", deserialized.Tags);
        Assert.Equal(CaseActionType.LineAdjustments, deserialized.AssociatedAction);
    }
}

