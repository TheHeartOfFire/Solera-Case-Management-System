using AMFormsCST.Core;
using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.Models;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop.Services
{
    public class LinksService(ILogService? logger = null) : ILinksService
{
    private readonly ILogService? _logger = logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public void OpenLink(string linkKey)
    {
        TryOpenLink(linkKey);
    }

    public bool TryOpenLink(string target)
    {
        try
        {
            string path = ResolvePath(target);
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger?.LogWarning($"Target link path is empty for: '{target}'.");
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });

            _logger?.LogInfo($"Quick link opened: '{path}'.");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to open quick link for: '{target}'.", ex);
            return false;
        }
    }

    public string ResolvePath(string linkKey)
    {
        if (string.IsNullOrWhiteSpace(linkKey)) return string.Empty;

        string path = linkKey switch
        {
            "ADP" => "https://my.adp.com",
            "SalesForce" => "https://am.my.salesforce.com",
            "MyApps" => "https://myapps.microsoft.com",
            "ClientInfoReport" => "https://americamp.sharepoint.com/:x:/r/sites/Compliance-Public/Shared%20Documents/Call%20Center/Client%20Information%20Report.xlsm?d=w2f7b6a8f3c9c4d6a9b7f8e7d0b4a9a6b&csf=1&web=1&e=VvWw9T",
            "FormsTracker" => "https://americamp.sharepoint.com/sites/Compliance-Public/Lists/Formgen%20Request%20Tracker/AllItems.aspx",
            "Workday" => "https://wd5.myworkday.com/solera/d/home.htmld",
            "CST" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AmpsSupport", "CSTLoader.exe"),
            "Formgen" => ResolveFormgenPath(),
            _ => linkKey
        };

        try
        {
            return Environment.ExpandEnvironmentVariables(path);
        }
        catch
        {
            return path;
        }
    }

    public static string ResolveFormgenPath()
    {
        var desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "NewFormGen.lnk");
        if (File.Exists(desktopLnk)) return desktopLnk;

        var localBat = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NewFormGen\NewFormGen.bat");
        if (File.Exists(localBat)) return localBat;

        var appDataBat = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"NewFormGen\NewFormGen.bat");
        if (File.Exists(appDataBat)) return appDataBat;

        return desktopLnk;
    }

    public List<LinkModel> LoadLinks()
    {
        try
        {
            var json = IO.LoadQuickLinks();
            if (!string.IsNullOrWhiteSpace(json))
            {
                var links = JsonSerializer.Deserialize<List<LinkModel>>(json, JsonOptions);
                if (links is { Count: > 0 })
                {
                    _logger?.LogInfo($"Loaded {links.Count} quick links from disk.");
                    return links;
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error deserializing quick links from disk. Falling back to defaults.", ex);
        }

        var defaults = GetDefaultLinks();
        SaveLinks(defaults);
        return defaults;
    }

    public void SaveLinks(IEnumerable<LinkModel> links)
    {
        try
        {
            var list = links.ToList();
            var json = JsonSerializer.Serialize(list, JsonOptions);
            IO.SaveQuickLinks(json);
            _logger?.LogInfo($"Saved {list.Count} quick links to disk.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error saving quick links to disk.", ex);
        }
    }

    public List<LinkModel> GetDefaultLinks()
    {
        return
        [
            new LinkModel
            {
                Id = "salesforce",
                DisplayName = "Salesforce",
                Description = "AM Salesforce Case Management & Support portal",
                Category = "Portals",
                Key = "SalesForce",
                TargetUrl = "https://am.my.salesforce.com",
                Icon = SymbolRegular.Cloud24,
                IsBuiltIn = true
            },
            new LinkModel
            {
                Id = "workday",
                DisplayName = "Workday",
                Description = "Solera HR, payroll, benefits, & time tracking",
                Category = "Portals",
                Key = "Workday",
                TargetUrl = "https://wd5.myworkday.com/solera/d/home.htmld",
                Icon = SymbolRegular.PersonAccounts24,
                IsBuiltIn = true
            },
            new LinkModel
            {
                Id = "adp",
                DisplayName = "ADP",
                Description = "ADP portal for earnings, pay stubs, & tax forms",
                Category = "Portals",
                Key = "ADP",
                TargetUrl = "https://my.adp.com",
                Icon = SymbolRegular.Money24,
                IsBuiltIn = true
            },
            new LinkModel
            {
                Id = "myapps",
                DisplayName = "MyApps",
                Description = "Microsoft 365 & enterprise application launcher",
                Category = "Portals",
                Key = "MyApps",
                TargetUrl = "https://myapps.microsoft.com",
                Icon = SymbolRegular.AppGeneric24,
                IsBuiltIn = true
            },
            new LinkModel
            {
                Id = "formstracker",
                DisplayName = "Forms Tracker",
                Description = "FormGen Request Tracker SharePoint list",
                Category = "Forms & Compliance",
                Key = "FormsTracker",
                TargetUrl = "https://americamp.sharepoint.com/sites/Compliance-Public/Lists/Formgen%20Request%20Tracker/AllItems.aspx",
                Icon = SymbolRegular.TaskListLtr24,
                IsBuiltIn = true
            },
            new LinkModel
            {
                Id = "clientinforeport",
                DisplayName = "Client Info Report",
                Description = "Excel Call Center Client Information Report",
                Category = "Forms & Compliance",
                Key = "ClientInfoReport",
                TargetUrl = "https://americamp.sharepoint.com/:x:/r/sites/Compliance-Public/Shared%20Documents/Call%20Center/Client%20Information%20Report.xlsm?d=w2f7b6a8f3c9c4d6a9b7f8e7d0b4a9a6b&csf=1&web=1&e=VvWw9T",
                Icon = SymbolRegular.DocumentTable24,
                IsBuiltIn = true
            },
            new LinkModel
            {
                Id = "formgen",
                DisplayName = "FormGen",
                Description = "Automate FormGen forms designer executable",
                Category = "Tools & Utilities",
                Key = "Formgen",
                TargetUrl = ResolveFormgenPath(),
                Icon = SymbolRegular.DocumentBulletList24,
                IsBuiltIn = true
            },
            new LinkModel
            {
                Id = "cst",
                DisplayName = "CST",
                Description = "Customer Support Tool (CST Loader)",
                Category = "Tools & Utilities",
                Key = "CST",
                TargetUrl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AmpsSupport", "CSTLoader.exe"),
                Icon = SymbolRegular.Wrench24,
                IsBuiltIn = true
            }
        ];
    }
}
}