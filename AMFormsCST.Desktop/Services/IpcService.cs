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
            ActiveCodeSnippetText = activeCodeSnippetText
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

            default:
                return IpcResponse.Fail($"Unknown field '{field}'.");
        }

        var updatedVars = GetLiveVariables();
        _ = Task.Run(() => FormGenLauncherHelper.SyncStateToAgent(updatedVars, Environment.ProcessPath, _logger));

        return IpcResponse.Ok($"Dashboard field '{field}' updated.");
    }

    private IpcResponse LoadCase(IpcMessage message)
    {
        var newNote = new NoteModel(" ", _logger);

        if (message.Payload.TryGetValue("caseNumber", out var caseNumber) && !string.IsNullOrWhiteSpace(caseNumber))
        {
            newNote.CaseNumber = caseNumber;
        }

        if (message.Payload.TryGetValue("notes", out var notes) && !string.IsNullOrWhiteSpace(notes))
        {
            var doc = new FlowDocument(new Paragraph(new Run(notes)));
            newNote.NotesXaml = XamlWriter.Save(doc);
        }

        message.Payload.TryGetValue("dealerName", out var dealerName);
        message.Payload.TryGetValue("serverCode", out var serverCode);

        if (!string.IsNullOrWhiteSpace(dealerName) || !string.IsNullOrWhiteSpace(serverCode))
        {
            var dealer = new Dealer(_logger)
            {
                Name = dealerName ?? string.Empty,
                ServerCode = serverCode ?? string.Empty
            };

            if (message.Payload.TryGetValue("companyNumber", out var companyNumber) && !string.IsNullOrWhiteSpace(companyNumber))
            {
                dealer.Companies.Add(new Company(_logger) { CompanyCode = companyNumber });
            }

            newNote.Dealers.Add(dealer);
            dealer.Select();
        }

        if (message.Payload.TryGetValue("contactName", out var contactName) && !string.IsNullOrWhiteSpace(contactName))
        {
            var contact = new Contact(" ", _logger)
            {
                Name = contactName,
                Email = message.Payload.GetValueOrDefault("contactEmail", string.Empty),
                Phone = message.Payload.GetValueOrDefault("contactPhone", string.Empty)
            };
            newNote.Contacts.Add(contact);
            contact.Select();
        }

        _dashboardViewModel.Notes.Add(newNote);
        newNote.Select();

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

    private IpcResponse ProcessCitadelBrainDump(IpcMessage message)
    {
        var liveSnapshot = GetLiveVariablesSnapshot();
        return IpcResponse.Ok("Citadel brain dump processed", liveSnapshot.Data);
    }
}
