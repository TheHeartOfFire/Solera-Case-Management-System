using AMFormsCST.Core.Types.Notebook;
using AMFormsCST.Core.Utils;
using Xunit;

namespace AMFormsCST.Test.Core.Utils;

public class CaseWorkLogParserTests
{
    [Fact]
    public void ParseWorkLogEntries_AccuratelyExtractsAllEntities()
    {
        var sampleText = @"=== SCMT Work Log ===
Case: #00123456 | Server: SVR1 | Dealer: Apex Automotive Group
Linked Cases: #00123457, #00123480
Contacts: John Doe (Finance Director) - Off on Fridays

• [Line Adjustments] F102 - Buyers Order [Custom Copy] -> Stores: 07 - Apex Nissan
  - Deals: 54321 (Retail, Store 07), 67890 (Lease)
  - Details: Shifted lines 14-16 down 2pt
• [Menu Addition] F205 - Odometer -> Stores: 01 - Honda, 02 - Toyota
  - Details: Added to DMS menu
====================";

        var entries = CaseWorkLogParser.ParseWorkLogEntries(sampleText, markAsHistorical: true);

        Assert.Equal(2, entries.Count);

        var first = entries[0];
        Assert.Equal(CaseActionType.LineAdjustments, first.Action);
        Assert.Equal("F102 - Buyers Order", first.FormName);
        Assert.True(first.IsCustomCopy);
        Assert.True(first.IsHistorical);
        Assert.False(first.IncludeInOutput);
        Assert.Single(first.Stores);
        Assert.Equal("07", first.Stores[0].CompanyCode);
        Assert.Equal("Apex Nissan", first.Stores[0].StoreName);
        Assert.Equal(2, first.Deals.Count);
        Assert.Equal("54321", first.Deals[0].DealNumber);
        Assert.Equal("Retail, Store 07", first.Deals[0].Purpose);
        Assert.Equal("67890", first.Deals[1].DealNumber);
        Assert.Equal("Lease", first.Deals[1].Purpose);
        Assert.Equal("Shifted lines 14-16 down 2pt", first.Details);

        var second = entries[1];
        Assert.Equal(CaseActionType.NewFormPublish, second.Action);
        Assert.Equal("F205 - Odometer", second.FormName);
        Assert.False(second.IsCustomCopy);
        Assert.Equal(2, second.Stores.Count);
        Assert.Equal("01", second.Stores[0].CompanyCode);
        Assert.Equal("Honda", second.Stores[0].StoreName);
        Assert.Equal("Added to DMS menu", second.Details);

        var linkedCases = CaseWorkLogParser.ParseLinkedCases(sampleText);
        Assert.Equal(2, linkedCases.Count);
        Assert.Contains("00123457", linkedCases);
        Assert.Contains("00123480", linkedCases);
    }
}

