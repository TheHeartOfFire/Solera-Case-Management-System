using AMFormsCST.Core.Interfaces.BestPractices;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Types.BestPractices.TextTemplates.Models;
using AMFormsCST.Core.Interfaces;

namespace AMFormsCST.Core.Utils;
public class BestPracticeEnforcer : IBestPracticeEnforcer
{
    private readonly ITemplateRepository _templateRepository;
    private readonly ILogService? _logger;

    public static readonly IReadOnlyList<string> StateCodes = ["AK", "AZ", "AR", "CA", "CO", "CT", "DE", "DC", "FL", "GA", "HI", "ID", "IL", "IN", "IA", "KS", "KY", "LA", "ME", "MD", "MA", "MI", "MN", "MS", "MO", "MT", "NE", "NV", "NH", "NJ", "NM", "NY", "NC", "ND", "OH", "OK", "OR", "PA", "PR", "RI", "SC", "SD", "TN", "TX", "UT", "VT", "VA", "VI", "WA", "WV", "WI", "WY"];
    public IFormNameBestPractice FormNameBestPractice { get; private set; }
    public string GetFormName() => FormNameBestPractice.Generate();
    public List<TextTemplate> Templates { get; }

    public BestPracticeEnforcer(IFormNameBestPractice formNameBestPractice, ITemplateRepository templateRepository, ILogService? logger = null)
    {
        FormNameBestPractice = formNameBestPractice;
        _templateRepository = templateRepository;
        _logger = logger;
        Templates = _templateRepository.LoadTemplates();
        _logger?.LogInfo($"BestPracticeEnforcer initialized with {Templates.Count} templates.");
    }

    public void AddTemplate(TextTemplate template)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(template);
            if (string.IsNullOrWhiteSpace(TextTemplate.GetFlowDocumentPlainText(template.Text))) throw new ArgumentException("Template text cannot be null or whitespace.", nameof(template));
            if (Templates.Contains(template)) throw new ArgumentException("Template already exists.", nameof(template));

            Templates.Add(template);
            _templateRepository.SaveTemplates(Templates);
            _logger?.LogInfo($"Template added: {template.Name} ({template.Id})");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to add template: {template.Name} ({template.Id})", ex);
            throw;
        }
    }

    public void RemoveTemplate(TextTemplate template)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(template);
            if (string.IsNullOrWhiteSpace(TextTemplate.GetFlowDocumentPlainText(template.Text))) throw new ArgumentException("Template text cannot be null or whitespace.", nameof(template));
            if (!Templates.Contains(template)) throw new ArgumentException("Template does not exist.", nameof(template));

            Templates.Remove(template);
            _templateRepository.SaveTemplates(Templates);
            _logger?.LogInfo($"Template removed: {template.Name} ({template.Id})");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to remove template: {template.Name} ({template.Id})", ex);
            throw;
        }
    }

    public void UpdateTemplate(TextTemplate updatedTemplate)
    {
        try
        {
            var existingTemplate = Templates.FirstOrDefault(t => t.Id == updatedTemplate.Id);

            if (existingTemplate is null) 
            { 
                _logger?.LogWarning($"UpdateTemplate called but template not found: {updatedTemplate.Id}");
                return;
            }

            existingTemplate.Name = updatedTemplate.Name;
            existingTemplate.Description = updatedTemplate.Description;
            existingTemplate.Text = updatedTemplate.Text;

            _templateRepository.SaveTemplates(Templates);
            _logger?.LogInfo($"Template updated: {updatedTemplate.Name} ({updatedTemplate.Id})");

        }
        catch (Exception ex)
        {
            _logger?.LogError($"Failed to update template: {updatedTemplate.Name} ({updatedTemplate.Id})", ex);
            throw;
        }
    }
}
