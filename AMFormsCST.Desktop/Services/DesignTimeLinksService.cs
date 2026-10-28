using AMFormsCST.Desktop.Models;
using System.Diagnostics;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop.Services
{
    public class DesignTimeLinksService : ILinksService
{
    public void OpenLink(string linkKey)
    {
        Debug.WriteLine($"[DESIGN TIME] Attempted to open link with key: {linkKey}");
    }

    public bool TryOpenLink(string target)
    {
        Debug.WriteLine($"[DESIGN TIME] Attempted to open target: {target}");
        return true;
    }

    public List<LinkModel> LoadLinks() => GetDefaultLinks();

    public void SaveLinks(IEnumerable<LinkModel> links)
    {
        Debug.WriteLine($"[DESIGN TIME] Attempted to save {links.Count()} links");
    }

    public List<LinkModel> GetDefaultLinks() =>
    [
        new LinkModel { DisplayName = "Salesforce", Description = "AM Salesforce Case Management", Category = "Portals", Key = "SalesForce", TargetUrl = "https://am.my.salesforce.com", Icon = SymbolRegular.Cloud24, IsBuiltIn = true },
        new LinkModel { DisplayName = "Workday", Description = "Solera HR & Time", Category = "Portals", Key = "Workday", TargetUrl = "https://wd5.myworkday.com", Icon = SymbolRegular.PersonAccounts24, IsBuiltIn = true },
        new LinkModel { DisplayName = "ADP", Description = "ADP Payroll & Pay Stubs", Category = "Portals", Key = "ADP", TargetUrl = "https://my.adp.com", Icon = SymbolRegular.Money24, IsBuiltIn = true },
        new LinkModel { DisplayName = "FormGen", Description = "FormGen Form Designer", Category = "Tools & Utilities", Key = "Formgen", TargetUrl = "C:\\NewFormGen.lnk", Icon = SymbolRegular.DocumentBulletList24, IsBuiltIn = true }
    ];

    public string ResolvePath(string linkKey) => linkKey;
}
}