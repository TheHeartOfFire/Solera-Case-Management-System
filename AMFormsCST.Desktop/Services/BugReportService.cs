using AMFormsCST.Core.Interfaces;
using AMFormsCST.Desktop.Models;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;

namespace AMFormsCST.Desktop.Services;


public class BugReportService(ILogService? logger, IDialogService dialogService) : IBugReportService
{
    private readonly ILogService? _logger = logger;
    private readonly IDialogService _dialogService = dialogService;
    private static readonly HttpClient _httpClient = CreateHttpClient();
    private readonly string _bugReportEndpoint = Properties.Resources.BugReportEndpointUrl;

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("AMFormsCST", GetAppVersion()));
        return client;
    }
    public async Task CreateBugReportAsync()
    {
        var (result, title, description) = _dialogService.ShowBugReportDialog();

        if (!result || string.IsNullOrWhiteSpace(title))
        {
            _logger?.LogInfo("Bug report creation cancelled by user.");
            return;
        }

        var confirmation = _dialogService.ShowMessageBox(
            "This will submit your bug report to GitHub. Are you sure you want to proceed?",
            "Confirm Bug Report",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmation != MessageBoxResult.Yes)
        {
            _logger?.LogInfo("Bug report submission cancelled by user at confirmation.");
            return;
        }

        try
        {
            var logContent = await GetLogContentAsync();

            var payload = new
            {
                title,
                description,
                logContent,
                appVersion = GetAppVersion(),
                osVersion = RuntimeInformation.OSDescription
            };

            var jsonPayload = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(_bugReportEndpoint, content);

            if (response.IsSuccessStatusCode)
            {
                _dialogService.ShowMessageBox("Bug report submitted successfully! It will appear on GitHub shortly.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                _logger?.LogInfo("Successfully submitted the bug report.");
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                // If it's 401 Unauthorized, offer a clear message
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || (int)response.StatusCode == 401)
                {
                    errorContent = "The system is unable to authenticate with the bug report service. The internal token may have expired. Please contact support.";
                }

                _dialogService.ShowMessageBox($"Failed to submit bug report. Status: {response.StatusCode}\n{errorContent}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                _logger?.LogError($"Failed to submit bug report. Status: {response.StatusCode}, Response: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessageBox($"An unexpected error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            _logger?.LogError("An exception occurred while creating a bug report.", ex);
        }
    }

    private async Task<string> GetLogContentAsync()
    {
        try
        {
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logDirectory)) return "Log directory not found.";

            var logFile = Directory.GetFiles(logDirectory, "app*.log")
                                   .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                                   .FirstOrDefault();

            if (logFile is null) return "No log files found.";

            using var fileStream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var streamReader = new StreamReader(fileStream);
            return await streamReader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogError("Failed to read log files.", ex);
            return $"Error reading logs: {ex.Message}";
        }
    }

    private static string GetAppVersion() => FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion ?? "1.0.0";
}