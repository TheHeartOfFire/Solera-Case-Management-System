using AMFormsCST.Core;
using AMFormsCST.Core.Interfaces;
using AMFormsCST.Core.Interfaces.BestPractices;
using AMFormsCST.Core.Interfaces.UserSettings;
using AMFormsCST.Core.Interfaces.Utils;
using AMFormsCST.Core.Services;
using AMFormsCST.Core.Types;
using AMFormsCST.Core.Types.BestPractices.Models;
using AMFormsCST.Core.Types.BestPractices.Practices;
using AMFormsCST.Core.Types.UserSettings;
using AMFormsCST.Core.Utils;
using AMFormsCST.Desktop.DependencyModel;
using AMFormsCST.Desktop.Interfaces;
using AMFormsCST.Desktop.Models.UserSettings;
using AMFormsCST.Desktop.Resources;
using AMFormsCST.Desktop.Services;
using AMFormsCST.Desktop.Services.Interfaces;
using AMFormsCST.Desktop.ViewModels;
using AMFormsCST.Desktop.ViewModels.Dialogs;
using AMFormsCST.Desktop.ViewModels.Pages;
using AMFormsCST.Desktop.ViewModels.Pages.Links;
using AMFormsCST.Desktop.ViewModels.Pages.Tools;
using AMFormsCST.Desktop.Views.Dialogs;
using AMFormsCST.Desktop.Views.Pages;
using AMFormsCST.Desktop.Views.Pages.Links;
using AMFormsCST.Desktop.Views.Pages.Tools;
using Lepo.i18n.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Compact;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Windows;
using System.Windows.Threading;
using Velopack;
using Wpf.Ui;
using Wpf.Ui.DependencyInjection;
using System.Threading;
using AMFormsCST.Core.Services.IPC;
using AMFormsCST.Core.Types.IPC;

namespace AMFormsCST.Desktop;
/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly IHost _host = BuildHost();

    private static IHost BuildHost()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { AddPolymorphicTypes }
            }
        };

        IO.ConfigureJson(jsonOptions);

        return Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(c =>
            {
                _ = c.SetBasePath(AppContext.BaseDirectory);
            })
            .ConfigureServices((_1, services) =>
            {
                _ = services.AddNavigationViewPageProvider();
                _ = services.AddSingleton<ILogService>(sp =>
                {
                    var logDir = System.IO.Path.Combine(AppContext.BaseDirectory, "logs");
                    System.IO.Directory.CreateDirectory(logDir);
                    var logPath = System.IO.Path.Combine(logDir, "app.log");

                    var logger = new LoggerConfiguration()
                        .MinimumLevel.Debug()
                        .WriteTo.Console()
                        .WriteTo.File(
                        path: logPath, 
                        formatter: new CompactJsonFormatter(), 
                        rollingInterval: RollingInterval.Day,
                        shared: true)
                        .Enrich.FromLogContext()
                        .CreateLogger();

                    return new SerilogService(logger);
                });
                _ = services.AddTransient<IDebounceService, DebounceService>();

                _ = services.AddHostedService<ApplicationHostService>();

                _ = services.AddSingleton<IpcService>();
                _ = services.AddHostedService(sp => sp.GetRequiredService<IpcService>());

                _ = services.AddSingleton<FormGenHookService>();
                _ = services.AddHostedService(sp => sp.GetRequiredService<FormGenHookService>());

                _ = services.AddSingleton<IWindow, MainWindow>();
                _ = services.AddSingleton<MainWindowViewModel>();
                _ = services.AddSingleton<INavigationService, NavigationService>();
                _ = services.AddSingleton<ISnackbarService, SnackbarService>();
                _ = services.AddSingleton<IContentDialogService, ContentDialogService>();
                _ = services.AddSingleton<WindowsProviderService>();
                _ = services.AddSingleton<IBugReportService, BugReportService>();
                _ = services.AddSingleton<IWindowsExplorerContextMenuService, WindowsExplorerContextMenuService>();

                _ = services.AddSingleton<DashboardPage>();
                _ = services.AddSingleton<DashboardViewModel>();
                _ = services.AddSingleton<ToolsPage>();
                _ = services.AddSingleton<ToolsViewModel>();
                _ = services.AddSingleton<TemplatesPage>();
                _ = services.AddSingleton<TemplatesViewModel>();
                _ = services.AddSingleton<FormgenUtilitiesPage>();
                _ = services.AddSingleton<FormgenUtilitiesViewModel>();
                _ = services.AddSingleton<SettingsPage>();
                _ = services.AddSingleton<SettingsViewModel>();
                _ = services.AddSingleton<LinksPage>();
                _ = services.AddSingleton<LinksViewModel>();

                _ = services.AddTransientFromNamespace("AMFormsCST.Desktop.Views", GalleryAssembly.Assembly);
                _ = services.AddTransientFromNamespace(
                    "AMFormsCST.Desktop.ViewModels",
                    GalleryAssembly.Assembly
                );

                _ = services.AddTransient<IDialogService, DialogService>();
                _ = services.AddSingleton<IFileSystem, FileSystem>();
                _ = services.AddTransient<IFormgenUtils, FormgenUtils>();
                _ = services.AddTransient<FormgenUtilsProperties>();

                _ = services.AddSingleton<AutoMateFormModel>();
                _ = services.AddSingleton<IFormNameBestPractice, AutoMateFormNameBestPractices>();
                _ = services.AddSingleton<ITemplateRepository, TemplateRepository>();
                _ = services.AddSingleton<IBestPracticeEnforcer, BestPracticeEnforcer>();
                _ = services.AddSingleton<INotebook, Notebook>();
                _ = services.AddSingleton<ISupportTool, SupportTool>();
                _ = services.AddSingleton<IOrgVariables>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogService>();
                    var orgVars = new AutomateFormsOrgVariables(() => sp.GetRequiredService<ISupportTool>(), logger);

                    return orgVars;
                });
                _ = services.AddSingleton<IUpdateManagerService, UpdateManagerService>();
                _ = services.AddSingleton<IUiSettings, UiSettings>();
                _ = services.AddSingleton<IUserSettings, UserSettings>();
                _ = services.AddSingleton<ISettings, Settings>();
                _ = services.AddSingleton<Services.ILinksService, LinksService>();

                _ = services.AddTransient<AddLinkDialogPage>();
                _ = services.AddTransient<AddLinkDialogViewModel>();
                _ = services.AddTransient<BugReportDialog>();
                _ = services.AddTransient<BugReportDialogViewModel>();

                _ = services.AddStringLocalizer(b =>
                {
                    b.FromResource<Translations>(new("pl-PL"));
                });
            })
            .Build();
    }
    private static void AddPolymorphicTypes(JsonTypeInfo jsonTypeInfo)
    {
        if (jsonTypeInfo.Type == typeof(ISettings))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                DerivedTypes = { new(typeof(Settings), "settings") }
            };
        }

        if (jsonTypeInfo.Type == typeof(IUserSettings))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                DerivedTypes = { new(typeof(UserSettings), "usersettings") }
            };
        }

        if (jsonTypeInfo.Type == typeof(IUiSettings))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                DerivedTypes = { new(typeof(UiSettings), "uisettings") }
            };
        }

        if (jsonTypeInfo.Type == typeof(ISetting))
        {
            jsonTypeInfo.PolymorphismOptions = new JsonPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "$type",
                UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
                DerivedTypes =
                {
                    new(typeof(UserSettings), "usersettings"),
                    new(typeof(UiSettings), "uisettings"),
                    new(typeof(AutomateFormsOrgVariables), "orgvars"),

                    new(typeof(ThemeSetting), "themesetting"),
                    new(typeof(AlwaysOnTopSetting), "alwaysontopsetting"),
                    new(typeof(NewTemplateSetting), "newtemplatesetting"),
                    new(typeof(CapitalizeFormCodeSetting), "capitalizeformcodesetting")
                }
            };
        }
    }

    /// <summary>
    /// Gets registered service.
    /// </summary>
    /// <typeparam name="T">Type of the service to get.</typeparam>
    /// <returns>Instance of the service or <see langword="null"/>.</returns>
    public static T GetRequiredService<T>()
        where T : class
    {
        return _host.Services.GetRequiredService<T>();
    }

    private const string AppMutexName = @"Local\SoleraCMS_SingleInstance_Mutex";
    private static Mutex? _instanceMutex;

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        if (e.Args.Any(a => a.Contains("--native-messaging", StringComparison.OrdinalIgnoreCase) || a.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase)))
        {
            NativeMessagingBridge.RunAsync().GetAwaiter().GetResult();
            Environment.Exit(0);
            return;
        }

        _instanceMutex = new Mutex(true, AppMutexName, out bool isNewInstance);
        if (!isNewInstance)
        {
            HandleSecondInstanceLaunch(e.Args);
            _instanceMutex.Dispose();
            _instanceMutex = null;
            Environment.Exit(0);
            return;
        }

        var velopackLogger = new VelopackSerilogLogger(GetRequiredService<ILogService>());
        VelopackApp.Build().SetLogger(velopackLogger).Run();
        _host.Start();

        try
        {
            var supportTool = GetRequiredService<ISupportTool>();
            var contextMenuService = GetRequiredService<IWindowsExplorerContextMenuService>();
            if (supportTool.Settings.UserSettings.EnableExplorerContextMenu)
            {
                contextMenuService.RegisterContextMenu(supportTool.Settings.UserSettings.FormGenExecutablePath);
            }
        }
        catch { }

        // Check if a formgen file was passed on cold startup
        if (e.Args != null && e.Args.Length > 0)
        {
            string? coldFilePath = null;
            for (int i = 0; i < e.Args.Length; i++)
            {
                if (string.Equals(e.Args[i], "--open-formgen", StringComparison.OrdinalIgnoreCase) && i + 1 < e.Args.Length)
                {
                    coldFilePath = e.Args[i + 1];
                    break;
                }
                if (e.Args[i].EndsWith(".formgen", StringComparison.OrdinalIgnoreCase) ||
                    e.Args[i].EndsWith(".fga", StringComparison.OrdinalIgnoreCase))
                {
                    coldFilePath = e.Args[i];
                    break;
                }
            }

            if (!string.IsNullOrWhiteSpace(coldFilePath))
            {
                coldFilePath = coldFilePath.Trim('\"', ' ');
                var ipcService = GetRequiredService<IpcService>();
                Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, () =>
                {
                    ipcService.OpenFormgenUtilsDialogWithFile(coldFilePath);
                });
            }
        }
    }

    private static void HandleSecondInstanceLaunch(string[]? args)
    {
        try
        {
            if (args != null && args.Length > 0)
            {
                if (args.Any(a => a.Contains("--native-messaging", StringComparison.OrdinalIgnoreCase) || a.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase)))
                {
                    NativeMessagingBridge.RunAsync().GetAwaiter().GetResult();
                    return;
                }

                string? targetFilePath = null;
                for (int i = 0; i < args.Length; i++)
                {
                    if (string.Equals(args[i], "--open-formgen", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    {
                        targetFilePath = args[i + 1];
                        break;
                    }
                    if (args[i].EndsWith(".formgen", StringComparison.OrdinalIgnoreCase) ||
                        args[i].EndsWith(".fga", StringComparison.OrdinalIgnoreCase))
                    {
                        targetFilePath = args[i];
                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(targetFilePath))
                {
                    targetFilePath = targetFilePath.Trim('\"', ' ');
                    var msg = new IpcMessage
                    {
                        Target = "SoleraCMS",
                        Action = IpcActions.OpenFormgen,
                        Payload = { ["filePath"] = targetFilePath }
                    };
                    var response = NamedPipeIpcClient.SendMessageAsync(msg, timeoutMs: 3000).GetAwaiter().GetResult();
                    if (response.Success)
                    {
                        return;
                    }
                }
            }

            var bringFrontMsg = new IpcMessage
            {
                Target = "SoleraCMS",
                Action = IpcActions.BringToFront
            };
            NamedPipeIpcClient.SendMessageAsync(bringFrontMsg, timeoutMs: 3000).GetAwaiter().GetResult();
        }
        catch { }
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private void OnExit(object sender, ExitEventArgs e)
    {
        try
        {
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
                _instanceMutex = null;
            }
        }
        catch { }

        try
        {
            var supportTool = GetRequiredService<ISupportTool>();
            supportTool.SaveAllSettings();
        }
        catch { }

        try
        {
            _host.StopAsync().Wait(TimeSpan.FromSeconds(2));
            _host.Dispose();
        }
        catch { }

        Environment.Exit(0);
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) { }
}

