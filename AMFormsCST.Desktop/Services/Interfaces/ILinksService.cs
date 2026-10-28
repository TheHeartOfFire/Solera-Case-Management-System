using AMFormsCST.Desktop.Models;

namespace AMFormsCST.Desktop.Services
{
    public interface ILinksService
{
    void OpenLink(string linkKey);
    bool TryOpenLink(string target);
    List<LinkModel> LoadLinks();
    void SaveLinks(IEnumerable<LinkModel> links);
    List<LinkModel> GetDefaultLinks();
    string ResolvePath(string linkKey);
}
}