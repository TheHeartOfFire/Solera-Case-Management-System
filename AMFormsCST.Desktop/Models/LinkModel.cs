using CommunityToolkit.Mvvm.ComponentModel;
using System.IO;
using System.Text.Json.Serialization;
using Wpf.Ui.Controls;

namespace AMFormsCST.Desktop.Models
{
    public partial class LinkModel : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string _category = "General";

    [ObservableProperty]
    private string _key = string.Empty;

    [ObservableProperty]
    private string? _targetUrl;

    [ObservableProperty]
    private SymbolRegular _icon = SymbolRegular.Globe24;

    [ObservableProperty]
    private bool _isBuiltIn;

    [JsonIgnore]
    public string ResolvedPath => !string.IsNullOrWhiteSpace(TargetUrl) ? TargetUrl : Key;

    [JsonIgnore]
    public bool IsWebLink => ResolvedPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                             ResolvedPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    [JsonIgnore]
    public string DisplayHost
    {
        get
        {
            var path = ResolvedPath;
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;

            if (Uri.TryCreate(path, UriKind.Absolute, out var uri) &&
                (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                return uri.Host;
            }

            try
            {
                if (Path.IsPathRooted(path))
                {
                    return Path.GetFileName(path);
                }
            }
            catch { }

            return path;
        }
    }
}
}