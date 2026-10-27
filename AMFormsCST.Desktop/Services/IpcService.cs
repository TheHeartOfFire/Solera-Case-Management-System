using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Services.IPC;
using AMFormsCST.Core.Types.IPC;
using AMFormsCST.Desktop.Models;
using AMFormsCST.Desktop.ViewModels.Pages;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using AMFormsCST.Desktop.Views.Dialogs;
using AMFormsCST.Desktop.Views.Pages.Tools;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using Wpf.Ui;

namespace AMFormsCST.Desktop.Services;

/// <summary>
/// Background hosted service running the Named Pipe IPC server.
/// Mediates real-time bidirectional communication between Solera CMS, FormGen, Salesforce browser extension, and Project Citadel.
/// </summary>
public class IpcService : IHostedService, IAsyncDisposable
{
    private readonly DashboardViewModel _dashboardViewModel;
    private readonly TemplatesViewModel _templatesViewModel;
    private readonly FormgenUtilitiesViewModel _formgenViewModel;
    private readonly INavigationService _navigationService;
    private readonly ILogService _logger;

    private NamedPipeIpcServer? _server;
    private string? _lastLoadedCaseKey;
    private DateTime _lastLoadedCaseTime = DateTime.MinValue;

    public IpcService(
        DashboardViewModel dashboardViewModel,
        TemplatesViewModel templatesViewModel,
        FormgenUtilitiesViewModel formgenViewModel,
        INavigationService navigationService,
        ILogService logger)
    {
        _dashboardViewModel = dashboardViewModel ?? throw new ArgumentNullException(nameof(dashboardViewModel));
        _templatesViewModel = templatesViewModel ?? throw new ArgumentNullException(nameof(templatesViewModel));
        _formgenViewModel = formgenViewModel ?? throw new ArgumentNullException(nameof(formgenViewModel));
        _navigationService = navigationService ?? throw new ArgumentNullException(nameof(navigationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _server = new NamedPipeIpcServer(HandleIpcMessageAsync);
            _server.Start();
            _logger.LogInfo("Solera CMS Named Pipe IPC Server started on " + NamedPipeIpcServer.DefaultPipeName);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to start Named Pipe IPC server.", ex);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_server != null)
        {
            await _server.StopAsync();
            _logger.LogInfo("Named Pipe IPC Server stopped.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_server != null)
        {
            await _server.DisposeAsync();
            _server = null;
        }
    }

    private async Task<IpcResponse> HandleIpcMessageAsync(IpcMessage message)
    {
        _logger.LogInfo($"IPC message received: Action='{message.Action}', Target='{message.Target}'");

        switch (message.Action)
        {
            case IpcActions.Ping:
                return IpcResponse.Ok("Pong", new Dictionary<string, string>
                {
                    ["App"] = "SoleraCMS",
                    ["Version"] = typeof(App).Assembly.GetName().Version?.ToString() ?? "1.0.0"
                });

            case IpcActions.BringToFront:
                await Application.Current.Dispatcher.InvokeAsync(BringWindowToFront);
                return IpcResponse.Ok("Window activated.");

            case IpcActions.GetLiveVariables:
                return await Application.Current.Dispatcher.InvokeAsync(GetLiveVariablesSnapshot);

            case IpcActions.SetDashboardField:
                return await Application.Current.Dispatcher.InvokeAsync(() => SetDashboardField(message));

            case IpcActions.LoadCase:
                return await Application.Current.Dispatcher.InvokeAsync(() => LoadCase(message));

            case IpcActions.OpenFormgen:
                return await Application.Current.Dispatcher.InvokeAsync(() => OpenFormgen(message));

            case IpcActions.ParseCodeBlock:
                return await Application.Current.Dispatcher.InvokeAsync(() => HandleParseCodeBlock(message));

            case IpcActions.CitadelBrainDump:
                return await Application.Current.Dispatcher.InvokeAsync(() => ProcessCitadelBrainDump(message));

            default:
                return IpcResponse.Fail($"Unsupported IPC action: '{message.Action}'");
        }
    }

    private void BringWindowToFront()
    {
        var window = Application.Current.MainWindow;
        if (window == null) return;

        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
        window.Focus();
    }

    public IpcLiveVariables GetLiveVariables()
    {
        if (Application.Current?.Dispatcher != null && !Application.Current.Dispatcher.CheckAccess())
        {
            return Application.Current.Dispatcher.Invoke(() => GetLiveVariables());
        }

        var note = _dashboardViewModel.SelectedNote;
        var selectedDealer = note?.Dealers.SelectedItem;
        var selectedCompany = selectedDealer?.Companies.SelectedItem;
        var selectedContact = note?.Contacts.SelectedItem;
        var selectedForm = note?.Forms.SelectedItem;
        var selectedTestDeal = selectedForm?.TestDeals.SelectedItem;

        var notableCompanyNumbers = selectedDealer?.Companies
            .Where(c => c.Notable && !string.IsNullOrWhiteSpace(c.CompanyCode))
            .Select(c => c.CompanyCode!.Trim())
            .Distinct()
            .ToList() ?? [];

        var allCompanyNumbers = selectedDealer?.Companies
            .Where(c => !string.IsNullOrWhiteSpace(c.CompanyCode))
            .Select(c => c.CompanyCode!.Trim())
            .Distinct()
            .ToList() ?? [];

        string primaryCompanyNumber = notableCompanyNumbers.Count > 0
            ? string.Join(",", notableCompanyNumbers)
            : (selectedCompany?.CompanyCode?.Trim() ?? (allCompanyNumbers.FirstOrDefault() ?? string.Empty));

        string notesText = string.Empty;
        if (!string.IsNullOrWhiteSpace(note?.NotesXaml))
        {
            try
            {
                using var stream = new MemoryStream(Encoding.UTF8.GetBytes(note.NotesXaml));
                if (XamlReader.Load(stream) is FlowDocument doc)
                {
                    var range = new TextRange(doc.ContentStart, doc.ContentEnd);
                    notesText = range.Text.Trim();
                }
            }
            catch { }
        }

        var activeTemplatesVm = _dashboardViewModel.ActiveTemplatesViewModel ?? _templatesViewModel;
        var selectedTemplate = activeTemplatesVm?.SelectedTemplate ?? _templatesViewModel.SelectedTemplate;
        string activeTemplateName = selectedTemplate?.Template.Name ?? string.Empty;
        string activeTemplateText = GetFlowDocumentText(selectedTemplate?.Output);
        string activeCodeSnippetText = CodeSnippetsViewModel.GetActiveFormulaText();
        string activeCodeSnippetOutputType = CodeSnippetsViewModel.InferFormulaOutputType(activeCodeSnippetText);

        var userSettings = App.GetService<ISupportTool>()?.Settings?.UserSettings;
        if (userSettings != null)
        {
            FormGenLauncherHelper.WriteAgentConfig(userSettings, _logger);
        }

        return new IpcLiveVariables
        {
            ServerId = selectedDealer?.ServerCode ?? string.Empty,
            DealershipName = selectedDealer?.Name ?? string.Empty,
            CaseNumber = note?.CaseNumber ?? string.Empty,
            PrimaryCompanyNumber = primaryCompanyNumber,
            NotableCompanyNumbers = string.Join(",", notableCompanyNumbers),
            CompanyNumbers = allCompanyNumbers,
            ContactName = selectedContact?.Name ?? string.Empty,
            ContactEmail = selectedContact?.Email ?? string.Empty,
            ContactPhone = string.IsNullOrWhiteSpace(selectedContact?.PhoneExtension)
                ? (selectedContact?.Phone ?? string.Empty)
                : $"{selectedContact.Phone} x{selectedContact.PhoneExtension}",
            FormName = selectedForm?.Name ?? string.Empty,
            DealNumber = selectedTestDeal?.DealNumber ?? string.Empty,
            Notes = notesText,
            ActiveTemplateName = activeTemplateName,
            ActiveTemplateText = activeTemplateText,
            ActiveCodeSnippetText = activeCodeSnippetText,
            ActiveCodeSnippetOutputType = activeCodeSnippetOutputType,
            FormGenPreserveUdfTab = (userSettings?.FormGenPreserveUdfTab ?? true).ToString().ToLowerInvariant(),
            FormGenCanvasPasteField = (userSettings?.FormGenCanvasPasteField ?? true).ToString().ToLowerInvariant(),
            FormGenAlreadyOpenToast = (userSettings?.FormGenAlreadyOpenToast ?? true).ToString().ToLowerInvariant(),
            FormGenContextMenus = (userSettings?.FormGenContextMenus ?? true).ToString().ToLowerInvariant(),
            FormGenEditorUndoRedo = (userSettings?.FormGenEditorUndoRedo ?? true).ToString().ToLowerInvariant(),
            FormGenDialogAutoFill = (userSettings?.FormGenDialogAutoFill ?? true).ToString().ToLowerInvariant(),
            FormGenAutoRestoreForms = (userSettings?.FormGenAutoRestoreForms ?? true).ToString().ToLowerInvariant()
        };
    }

    private IpcResponse GetLiveVariablesSnapshot()
    {
        var vars = GetLiveVariables();
        return IpcResponse.Ok("Variables retrieved", vars.ToDictionary());
    }

    private static string GetFlowDocumentText(FlowDocument? doc)
    {
        if (doc == null) return string.Empty;
        try
        {
            return new TextRange(doc.ContentStart, doc.ContentEnd).Text.Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private IpcResponse SetDashboardField(IpcMessage message)
    {
        var note = _dashboardViewModel.SelectedNote;
        if (note == null)
        {
            return IpcResponse.Fail("No active note selected in Dashboard.");
        }

        message.Payload.TryGetValue("field", out var field);
        message.Payload.TryGetValue("value", out var value);

        if (string.IsNullOrWhiteSpace(field))
        {
            return IpcResponse.Fail("Missing 'field' argument in payload.");
        }

        value ??= string.Empty;

        switch (field.ToLowerInvariant())
        {
            case "casenumber":
            case "case":
                note.CaseNumber = value;
                break;

            case "serverid":
            case "servercode":
            case "server":
                var dealer = note.Dealers.SelectedItem ?? note.Dealers.FirstOrDefault();
                if (dealer == null)
                {
                    dealer = new Dealer(_logger);
                    note.Dealers.Add(dealer);
                    dealer.Select();
                }
                dealer.ServerCode = value;
                break;

            case "dealership":
            case "dealer":
            case "dealershipname":
                var d = note.Dealers.SelectedItem ?? note.Dealers.FirstOrDefault();
                if (d == null)
                {
                    d = new Dealer(_logger);
                    note.Dealers.Add(d);
                    d.Select();
                }
                d.Name = value;
                break;

            case "companynumber":
            case "companycode":
            case "company":
                var currentDealer = note.Dealers.SelectedItem ?? note.Dealers.FirstOrDefault();
                if (currentDealer != null)
                {
                    var comp = currentDealer.Companies.SelectedItem ?? currentDealer.Companies.FirstOrDefault();
                    if (comp == null)
                    {
                        comp = new Company(_logger);
                        currentDealer.Companies.Add(comp);
                        comp.Select();
                    }
                    comp.CompanyCode = value;
                }
                break;

            case "contactname":
            case "contact":
                var contact = note.Contacts.SelectedItem ?? note.Contacts.FirstOrDefault();
                if (contact == null)
                {
                    contact = new Contact(" ", _logger);
                    note.Contacts.Add(contact);
                    contact.Select();
                }
                contact.Name = value;
                break;

            case "contactemail":
            case "email":
                var cEmail = note.Contacts.SelectedItem ?? note.Contacts.FirstOrDefault();
                if (cEmail != null) cEmail.Email = value;
                break;

            case "contactphone":
            case "phone":
                var cPhone = note.Contacts.SelectedItem ?? note.Contacts.FirstOrDefault();
                if (cPhone != null) cPhone.Phone = value;
                break;

            case "notes":
                var doc = new FlowDocument(new Paragraph(new Run(value)));
                note.NotesXaml = XamlWriter.Save(doc);
                break;

            case "formtitle":
            case "formname":
            case "form":
                var activeForm = note.Forms.SelectedItem ?? note.Forms.FirstOrDefault();
                if (activeForm != null && string.IsNullOrWhiteSpace(activeForm.Name))
                {
                    activeForm.Name = value.Trim();
                    activeForm.Select();
                }
                else if (activeForm != null && activeForm.Name.Trim().Equals(value.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    activeForm.Select();
                }
                else
                {
                    var newForm = new Form(note, _logger) { Name = value.Trim() };
                    note.Forms.Add(newForm);
                    newForm.Select();
                }
                break;

            default:
                return IpcResponse.Fail($"Unknown field '{field}'.");
        }

        var updatedVars = GetLiveVariables();
        _ = Task.Run(() => FormGenLauncherHelper.SyncStateToAgent(updatedVars, Environment.ProcessPath, _logger));

        return IpcResponse.Ok($"Dashboard field '{field}' updated.");
    }

    private IpcResponse LoadCase(IpcMessage message)
    {
        message.Payload.TryGetValue("caseNumber", out var incomingCaseNum);
        string dedupeKey = !string.IsNullOrWhiteSpace(incomingCaseNum)
            ? incomingCaseNum.Trim()
            : (!string.IsNullOrWhiteSpace(message.RawData) ? message.RawData.Trim() : message.Action);

        if (string.Equals(_lastLoadedCaseKey, dedupeKey, StringComparison.OrdinalIgnoreCase) &&
            (DateTime.UtcNow - _lastLoadedCaseTime).TotalSeconds < 4.0)
        {
            _logger.LogInfo($"Duplicate LoadCase request ignored for '{dedupeKey}'.");
            BringWindowToFront();
            return IpcResponse.Ok("Duplicate case request ignored.");
        }

        _lastLoadedCaseKey = dedupeKey;
        _lastLoadedCaseTime = DateTime.UtcNow;

        NoteModel newNote;
        if (!string.IsNullOrWhiteSpace(message.RawData))
        {
            newNote = _dashboardViewModel.ParseCaseText(message.RawData);
        }
        else
        {
            newNote = new NoteModel(" ", _logger);
        }

        if (DashboardViewModel.IsMatchingLabel(newNote.CaseNumber, "Case Number", "Case #", "CaseNumber"))
        {
            newNote.CaseNumber = string.Empty;
        }

        if (message.Payload.TryGetValue("caseNumber", out var caseNumber) && !string.IsNullOrWhiteSpace(caseNumber) && !DashboardViewModel.IsMatchingLabel(caseNumber, "Case Number", "Case #", "CaseNumber"))
        {
            newNote.CaseNumber = caseNumber.Trim();
        }

        message.Payload.TryGetValue("subject", out var subject);
        if (DashboardViewModel.IsMatchingLabel(subject, "Subject")) subject = null;
        if (!message.Payload.TryGetValue("notes", out var notes) || string.IsNullOrWhiteSpace(notes) || DashboardViewModel.IsMatchingLabel(notes, "Description", "Notes"))
        {
            message.Payload.TryGetValue("description", out notes);
            if (DashboardViewModel.IsMatchingLabel(notes, "Description", "Notes")) notes = null;
        }

        if (string.IsNullOrWhiteSpace(message.RawData) &&
            (!string.IsNullOrWhiteSpace(subject) || !string.IsNullOrWhiteSpace(notes)))
        {
            var combinedText = $"{subject}{Environment.NewLine}{notes}".Trim();
            var doc = new FlowDocument(new Paragraph(new Run(combinedText)));
            newNote.NotesXaml = XamlWriter.Save(doc);
        }

        if (!message.Payload.TryGetValue("dealerName", out var dealerName) || string.IsNullOrWhiteSpace(dealerName) || DashboardViewModel.IsMatchingLabel(dealerName, "Account Name", "Company Name", "Account"))
        {
            message.Payload.TryGetValue("dealership", out dealerName);
            if (DashboardViewModel.IsMatchingLabel(dealerName, "Account Name", "Company Name", "Account")) dealerName = null;
        }
        message.Payload.TryGetValue("serverCode", out var serverCode);
        if (DashboardViewModel.IsMatchingLabel(serverCode, "Server ID", "Server Code", "Server", "Automate Server Code")) serverCode = null;
        message.Payload.TryGetValue("companyNumber", out var companyNumber);
        if (DashboardViewModel.IsMatchingLabel(companyNumber, "Company Number", "Company Code")) companyNumber = null;

        var existingDealer = newNote.Dealers.FirstOrDefault();
        if (existingDealer != null)
        {
            if (DashboardViewModel.IsMatchingLabel(existingDealer.Name, "Account Name", "Company Name")) existingDealer.Name = string.Empty;
            if (DashboardViewModel.IsMatchingLabel(existingDealer.ServerCode, "Server ID", "Server Code", "Server", "Automate Server Code")) existingDealer.ServerCode = string.Empty;
            var existingComp = existingDealer.Companies.FirstOrDefault();
            if (existingComp != null)
            {
                if (DashboardViewModel.IsMatchingLabel(existingComp.Name, "Account Name", "Company Name")) existingComp.Name = string.Empty;
                if (DashboardViewModel.IsMatchingLabel(existingComp.CompanyCode, "Company Number", "Company Code")) existingComp.CompanyCode = string.Empty;
            }
        }

        if (!string.IsNullOrWhiteSpace(dealerName) || !string.IsNullOrWhiteSpace(serverCode) || !string.IsNullOrWhiteSpace(companyNumber))
        {
            var dealer = newNote.Dealers.FirstOrDefault();
            if (dealer == null)
            {
                dealer = new Dealer(_logger);
                newNote.Dealers.Add(dealer);
            }

            if (!string.IsNullOrWhiteSpace(dealerName) && string.IsNullOrWhiteSpace(dealer.Name))
            {
                dealer.Name = dealerName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(serverCode) && string.IsNullOrWhiteSpace(dealer.ServerCode))
            {
                dealer.ServerCode = serverCode.Trim();
            }

            var company = dealer.Companies.FirstOrDefault();
            if (company == null)
            {
                company = new Company(_logger);
                dealer.Companies.Add(company);
            }

            if (!string.IsNullOrWhiteSpace(dealerName) && string.IsNullOrWhiteSpace(company.Name))
            {
                company.Name = dealerName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(companyNumber) && string.IsNullOrWhiteSpace(company.CompanyCode))
            {
                company.CompanyCode = companyNumber.Trim();
            }

            dealer.Select();
        }

        message.Payload.TryGetValue("contactName", out var contactName);
        if (DashboardViewModel.IsMatchingLabel(contactName, "Contact Name", "Contact", "Name")) contactName = null;
        message.Payload.TryGetValue("contactEmail", out var contactEmail);
        if (DashboardViewModel.IsMatchingLabel(contactEmail, "Contact Email", "Email")) contactEmail = null;
        message.Payload.TryGetValue("contactPhone", out var contactPhone);
        if (DashboardViewModel.IsMatchingLabel(contactPhone, "Contact Phone", "Phone", "Mobile")) contactPhone = null;

        var existingContact = newNote.Contacts.FirstOrDefault();
        if (existingContact != null)
        {
            if (DashboardViewModel.IsMatchingLabel(existingContact.Name, "Contact Name", "Contact", "Name")) existingContact.Name = string.Empty;
            if (DashboardViewModel.IsMatchingLabel(existingContact.Email, "Contact Email", "Email")) existingContact.Email = string.Empty;
            if (DashboardViewModel.IsMatchingLabel(existingContact.Phone, "Contact Phone", "Phone", "Mobile")) existingContact.Phone = string.Empty;
            if (DashboardViewModel.IsMatchingLabel(existingContact.PhoneExtension, "Ext", "Extension")) existingContact.PhoneExtension = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(contactName) || !string.IsNullOrWhiteSpace(contactEmail) || !string.IsNullOrWhiteSpace(contactPhone))
        {
            var contact = newNote.Contacts.FirstOrDefault();
            if (contact == null)
            {
                contact = new Contact(" ", _logger);
                newNote.Contacts.Add(contact);
            }

            if (!string.IsNullOrWhiteSpace(contactName) && string.IsNullOrWhiteSpace(contact.Name))
            {
                contact.Name = contactName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(contactEmail) && string.IsNullOrWhiteSpace(contact.Email))
            {
                contact.Email = contactEmail.Trim();
            }
            if (!string.IsNullOrWhiteSpace(contactPhone) && string.IsNullOrWhiteSpace(contact.Phone))
            {
                var phoneMatch = System.Text.RegularExpressions.Regex.Match(contactPhone.Trim(), @"^(\S+)\s*(?:ext\.?|x)?\s*(\d+)?$");
                if (phoneMatch.Success)
                {
                    contact.Phone = phoneMatch.Groups[1].Value;
                    if (string.IsNullOrWhiteSpace(contact.PhoneExtension) && phoneMatch.Groups.Count > 2)
                    {
                        contact.PhoneExtension = phoneMatch.Groups[2].Value;
                    }
                }
                else
                {
                    contact.Phone = contactPhone.Trim();
                }
            }

            contact.Select();
        }

        _dashboardViewModel.LoadCaseFromNote(newNote);

        var loadedVars = GetLiveVariables();
        _ = Task.Run(() => FormGenLauncherHelper.SyncStateToAgent(loadedVars, Environment.ProcessPath, _logger));

        BringWindowToFront();
        return IpcResponse.Ok("New case loaded into Dashboard successfully.");
    }

    private IpcResponse OpenFormgen(IpcMessage message)
    {
        if (!message.Payload.TryGetValue("filePath", out var filePath) || string.IsNullOrWhiteSpace(filePath))
        {
            return IpcResponse.Fail("Missing filePath argument in payload.");
        }

        OpenFormgenUtilsDialogWithFile(filePath);
        return IpcResponse.Ok($"Opened {filePath} in Formgen Utilities.");
    }

    public void OpenFormgenUtilsDialogWithFile(string? filePath = null)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                filePath = filePath.Trim('\"', ' ');
            }

            var supportTool = App.GetRequiredService<ISupportTool>();
            var dialogService = App.GetRequiredService<IDialogService>();
            var fileSystem = App.GetRequiredService<IFileSystem>();
            var navigationService = App.GetRequiredService<INavigationService>();

            var vm = new FormgenUtilitiesViewModel(supportTool, dialogService, fileSystem, _logger);
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                vm.LoadFormgenFile(filePath);
            }

            BringWindowToFront();

            var page = new FormgenUtilitiesPage(vm, navigationService, _logger);
            var dialog = new PageHostDialog(page, "Formgen Utilities");

            if (Application.Current?.MainWindow is Window mainWindow && mainWindow.IsVisible)
            {
                dialog.Owner = mainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            dialog.Show();
            dialog.Activate();
            dialog.Focus();

            _logger?.LogInfo($"Opened Formgen Utilities Dialog with file: '{filePath}'.");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error opening Formgen Utilities Dialog.", ex);
        }
    }

    private IpcResponse HandleParseCodeBlock(IpcMessage message)
    {
        message.Payload.TryGetValue("code", out var code);
        if (string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(message.RawData))
        {
            code = message.RawData;
        }

        OpenCodeSnippetsDialogWithCode(code);
        return IpcResponse.Ok("Code parsed into Code Snippets.");
    }

    public void OpenCodeSnippetsDialogWithCode(string? codeText = null)
    {
        try
        {
            var supportTool = App.GetRequiredService<ISupportTool>();
            var vm = new CodeSnippetsViewModel(supportTool, _logger);
            if (!string.IsNullOrWhiteSpace(codeText))
            {
                vm.ClearCanvas();
                vm.PasteFormulaText(codeText.Trim());
            }

            BringWindowToFront();

            var page = new CodeSnippetsPage(vm, _logger);
            var dialog = new PageHostDialog(page, "Code Snippets");

            if (Application.Current?.MainWindow is Window mainWindow && mainWindow.IsVisible)
            {
                dialog.Owner = mainWindow;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            dialog.Show();
            dialog.Activate();
            dialog.Focus();

            _logger?.LogInfo($"Opened Code Snippets Dialog with code text (length: {codeText?.Length ?? 0}).");
        }
        catch (Exception ex)
        {
            _logger?.LogError("Error opening Code Snippets Dialog.", ex);
        }
    }

    public void SetFormTitle(string formTitle)
    {
        if (string.IsNullOrWhiteSpace(formTitle)) return;

        var message = new IpcMessage
        {
            Action = IpcActions.SetDashboardField,
            Payload = new Dictionary<string, string>
            {
                { "field", "formtitle" },
                { "value", formTitle.Trim() }
            }
        };

        if (Application.Current?.Dispatcher?.CheckAccess() == true)
        {
            SetDashboardField(message);
        }
        else
        {
            Application.Current?.Dispatcher?.Invoke(() => SetDashboardField(message));
        }
    }

    private IpcResponse ProcessCitadelBrainDump(IpcMessage message)
    {
        var liveSnapshot = GetLiveVariablesSnapshot();
        return IpcResponse.Ok("Citadel brain dump processed", liveSnapshot.Data);
    }
}
